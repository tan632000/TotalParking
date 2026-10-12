using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Chọn block đích: block gần ram dốc 1 nhất, đúng loại, còn chỗ, trên toàn bãi.
    // ZoneRouter chỉ còn quyết định xe có được điều hướng hay không.
    //
    // ===================== CHỌN Ở SERVER, KHÔNG CHỌN Ở TRÌNH DUYỆT =====================
    // Trước lớp này, Routing.cshtml tự lọc và tự chọn block trong JavaScript ở mỗi lần
    // poll. Hai hậu quả: màn hình và database nói hai chuyện khác nhau, và block hiển thị
    // đổi ngay trước mắt tài xế khi xe khác vào bãi. Quyết định được chốt một lần ở đây
    // rồi lưu lại; mọi bề mặt chỉ đọc lại con số đã lưu.
    //
    // ===================== CHỈ DỰA TRÊN CHỖ TRỐNG QUAN SÁT ĐƯỢC =====================
    // Không lọc theo hạng cân, chiều dài hay sức nâng pallet. `block.column_count` đang
    // NULL ở cả 112 block, nên mọi luật cân ở mức block sẽ được tính từ dữ liệu không tồn
    // tại. Lọc theo hạng cân vẫn nằm ở mức ZONE trong ZoneRouter, không chuyển xuống đây.
    public class BlockAllocator
    {
        // Cửa sổ trừ suất đã phát đi. CHỈ dùng cho việc đó.
        //
        // Trước đây hằng số này còn được endpoint route dùng để lọc quyết định nào
        // còn hiển thị được. Hai việc đã tách: màn hình tài xế giữ chỉ dẫn cho tới
        // khi có xe mới, còn phép trừ dưới đây vẫn cần một cửa sổ — không có nó thì
        // mọi quyết định từ trước tới nay đều bị trừ và mọi block đều trông như đầy.
        //
        // 90 giây là khoảng đủ để một xe đi từ barrier tới block được chỉ.
        public const int PendingDebitWindowSeconds = 90;

        // Một block được coi là "có người xác nhận" khi PLC đọc nó trong 5 phút gần đây.
        // Cùng ngưỡng mà BlockMapRepository đang dùng cho cột `fresh`.
        private const int PlcFreshMinutes = 5;

        // Thứ tự xếp hạng: xem mục "GẦN RAM DỐC 1 NHẤT" ngay trên PickSql.
        //
        // Chỗ trống = slot_count - số ô đang có xe - số suất đã phát đi trong cửa sổ.
        // Phần trừ cuối là để hai xe vào cách nhau vài giây không cùng bị đẩy vào một
        // block chỉ còn đúng một chỗ.
        // ===================== KHỐI ĐỖ NỀN ĐẾM KHÁC KHỐI CƠ KHÍ =====================
        //
        // `v_slot_taken` đọc `plc_slot_state`, mà bảng đó CHỈ gieo cho khối cơ khí
        // (17_plc_slot_state.sql lọc kind='Mechanical'). Với khối đỗ nền nó luôn trả
        // 0, nên phép trừ cũ luôn cho ra "còn nguyên sức chứa" bất kể ngoài bãi có
        // bao nhiêu xe. Đo ngày 28/09/2026: khối 906 được coi là còn 13 chỗ trong khi
        // cảm biến đếm được 6. Đã xảy ra thật — 9 lượt bị chỉ vào khối 901 ngày 24/09.
        //
        // Sửa bằng cách ĐỌC THẲNG `v_led_capacity_zone`, không tự tính lại. Đó là
        // cùng con số bảng LED đang hiện và cùng con số `v_zone_capacity.ground_free`
        // đang dùng — một công thức, một kết quả. Tự tính lại ở đây sẽ tái lập đúng
        // sự cố mà migration 50 vừa dọn: bảng LED đếm `trang_thai = 0` nên bỏ ô lỗi,
        // còn phép trừ sức chứa lại coi ô lỗi là trống, và hai màn hình nói hai điều.
        //
        // Nối qua `zone_id` là chính xác vì mỗi zone có ĐÚNG MỘT khối đỗ nền
        // (901..906, một khối mỗi zone), và `pgs_sensor_map` gắn cảm biến theo zone
        // chứ không theo khối.
        //
        // ===================== LỌC THEO HẠNG TẢI =====================
        //
        // Hạng tải quyết định LOẠI khối, không chỉ quyết định zone. Trước đây
        // `Allocate` chỉ nhận `zoneId` nên nó chọn khối nào xếp hạng cao nhất trong
        // zone, bất kể xe cần pallet cơ khí hay phải xuống nền. Hệ quả đo được ngày
        // 28/09/2026: **523 lượt** xe hạng THƯỜNG bị chỉ vào khối cơ khí, chỉ 7 lượt
        // vào khối đỗ nền.
        //
        // Hạng THƯỜNG nghĩa là "quá tải ở đỗ thường, không dùng pallet cơ khí" —
        // trong 1014 hồ sơ THƯỜNG, 878 xe quá RỘNG so với khoang, 153 trong số đó
        // vượt hơn 100 mm kể cả sau khi đã trừ biên gương. Chỉ họ tới khoang cơ khí
        // là chỉ tới chỗ xe không lọt.
        //
        // ĐÂY KHÔNG PHẢI VẤN ĐỀ AN TOÀN, đừng đọc nhầm. Mỗi khối cơ khí có rào chắn
        // riêng (bảng vật tư CL1 có cột "Motor hàng rào" cho từng khối), nên xe quá
        // khổ bị chặn ở đó bất kể màn hình nói gì. Thiệt hại là thời gian và niềm
        // tin: tài xế lái tới nơi, bị chặn, phải lùi ra rồi hỏi người trực.
        //
        // Một hệ quả âm thầm đáng biết: SCADA KHÔNG đọc trạng thái rào chắn, nên nó
        // không biết lượt chỉ đường vừa rồi đã bị từ chối ngoài hiện trường. Suất đã
        // phát vẫn bị trừ khỏi sức chứa khối đó trong 90 giây, và không dòng nào ghi
        // lại rằng chỉ dẫn đã hỏng. Vì thế 523 lượt sai kia trôi qua hoàn toàn vô
        // hình cho tới khi có người đi rà.
        //
        // Vì đã lọc `kind`, khối đỗ nền không còn tranh chỗ với khối cơ khí nữa, nên
        // `fresh_reads` của chúng được dùng `standard_fresh` cho đúng. Trước khi có
        // bộ lọc này thì KHÔNG được: thử cho chúng cạnh tranh sòng phẳng thì zone 2
        // và 5 lập tức đổi từ khối 80 và 19 sang 902 và 905.
        //
        // ===================== GẦN RAM DỐC 1 NHẤT, TRÊN TOÀN BÃI =====================
        //
        // Trước đây ZoneRouter chọn zone theo `zone.gate_rank` rồi lớp này chọn khối
        // TRONG zone đó theo "còn nhiều chỗ nhất, số nhỏ nhất". gate_rank đặt theo
        // cổng cũ, nên Z1 luôn đứng đầu: 3 ngày tới 12/10/2026 mọi lượt THƯỜNG vào
        // 901 và mọi lượt 2200KG vào 98/99/100, trong khi từ ram dốc 1 thì 902 và
        // các khối 64..68 của Z6 gần hơn nhiều.
        //
        // Giờ lấy ứng viên trên toàn bãi và xếp theo độ dài đường đi trên chính bản
        // đồ màn tài xế (DriverLaneMap). Màn hình vẽ đường nào thì quyết định đo theo
        // đường đó. Thứ tự:
        //   1. khối PLC/cảm biến vừa báo cáo đứng trước khối không được báo cáo
        //   2. khối có trên bản đồ tài xế đứng trước khối không có
        //   3. gần ram dốc hơn
        //   4. còn nhiều chỗ hơn, rồi block_no nhỏ hơn — để kết quả luôn xác định
        //
        // Bản đồ không nạp được thì lùi về chọn trong zone ZoneRouter đưa ra, như cũ:
        // xếp toàn bãi mà không có khoảng cách thì chỉ còn "số nhỏ nhất", tệ hơn.
        private const string PickSql =
            "SELECT c.block_no, c.zone_id, c.free_capacity, c.fresh_reads FROM ( " +
            "  SELECT b.block_no AS block_no, b.zone_id AS zone_id, " +
            "         (CASE WHEN b.kind = 'Ground' " +
            "               THEN COALESCE((SELECT g.free_standard FROM v_led_capacity_zone g " +
            "                               WHERE g.zone_id = b.zone_id), 0) " +
            "               ELSE b.slot_count " +
            "                    - (SELECT COUNT(*) FROM v_slot_taken t " +
            "                        WHERE t.block_id = b.block_id) " +
            "          END) " +
            "           - (SELECT COUNT(*) FROM vehicle_routing r " +
            "                WHERE r.block_no = b.block_no AND r.outcome = 'ROUTED' " +
            "                  AND r.decided_at > NOW(3) - INTERVAL @window SECOND " +
            "                  AND r.event_id <> @event_id) AS free_capacity, " +
            "         (CASE WHEN b.kind = 'Ground' " +
            "               THEN COALESCE((SELECT g.standard_fresh FROM v_led_capacity_zone g " +
            "                               WHERE g.zone_id = b.zone_id), 0) " +
            "               ELSE (SELECT COUNT(*) FROM plc_slot_state s " +
            "                      WHERE s.block_id = b.block_id " +
            "                        AND s.read_at >= NOW() - INTERVAL @plc_fresh MINUTE) " +
            "          END) AS fresh_reads " +
            "  FROM   block b " +
            "  WHERE  (@zone_id IS NULL OR b.zone_id = @zone_id) " +
            "    AND  b.is_active = 1 AND b.kind = @kind " +
            ") c " +
            "WHERE  c.free_capacity > 0";

        // Loại khối mà một hạng tải được phép vào.
        //
        // Ngả về phía hạn chế nhất, cùng quy ước với `ZoneRouter.FreeFor`: hạng lạ
        // hoặc thiếu đều xuống đỗ nền. Đưa một chiếc xe không rõ hạng lên pallet cơ
        // khí là rủi ro thật; đưa nhầm xuống bãi nền thì chỉ là bất tiện.
        public static string KindFor(string weightClass)
        {
            if (weightClass == WeightClassCode.Max2200) return "Mechanical";
            if (weightClass == WeightClassCode.Max2600) return "Mechanical";
            return "Ground";
        }

        // eventId được loại khỏi phép trừ: một sự kiện không tự trừ suất của chính nó khi
        // camera gửi lại cùng event_id, nếu không lần gửi lại sẽ bị đẩy sang block khác.
        // fallbackZoneId: zone ZoneRouter đã chọn, CHỈ dùng khi distances null.
        // distances: DriverLaneMap.BlockDistances() — block_no -> độ dài đường đi.
        public BlockAllocation Allocate(string eventId, string weightClass,
                                        IDictionary<int, double> distances, int fallbackZoneId)
        {
            var ungVien = new List<UngVien>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = PickSql;
                cmd.Parameters.AddWithValue("@zone_id",
                    distances == null ? (object)fallbackZoneId : DBNull.Value);
                cmd.Parameters.AddWithValue("@event_id",   eventId ?? string.Empty);
                cmd.Parameters.AddWithValue("@window",     PendingDebitWindowSeconds);
                cmd.Parameters.AddWithValue("@plc_fresh",  PlcFreshMinutes);
                cmd.Parameters.AddWithValue("@kind",       KindFor(weightClass));

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int no = Convert.ToInt32(r["block_no"]);
                        double d;
                        bool coTrenBanDo = distances != null && distances.TryGetValue(no, out d);
                        ungVien.Add(new UngVien
                        {
                            BlockNo  = no,
                            ZoneId   = Convert.ToInt32(r["zone_id"]),
                            Free     = Convert.ToInt32(r["free_capacity"]),
                            Fresh    = Convert.ToInt32(r["fresh_reads"]) > 0,
                            CoTrenBanDo = coTrenBanDo,
                            KhoangCach  = coTrenBanDo ? distances[no] : double.MaxValue
                        });
                    }
                }
            }

            // Không ứng viên nào = không block nào còn chỗ. Trả về "hết chỗ" chứ KHÔNG
            // trả về một block bừa: gửi tài xế tới chỗ đã đầy còn tệ hơn là nói thẳng
            // rằng bãi đã hết chỗ.
            var chon = ungVien
                .OrderByDescending(u => u.Fresh)
                .ThenByDescending(u => u.CoTrenBanDo)
                .ThenBy(u => u.KhoangCach)
                .ThenByDescending(u => u.Free)
                .ThenBy(u => u.BlockNo)
                .FirstOrDefault();

            if (chon == null) return BlockAllocation.NoCapacity();

            // Nhãn Fresh đi theo quyết định, không tính lại lúc đọc.
            return BlockAllocation.At(chon.BlockNo, chon.ZoneId, chon.Fresh);
        }

        private sealed class UngVien
        {
            public int    BlockNo;
            public int    ZoneId;
            public int    Free;
            public bool   Fresh;
            public bool   CoTrenBanDo;
            public double KhoangCach;
        }
    }

    // Kết quả chọn block. `BlockNo` null nghĩa là zone đã hết chỗ, không phải lỗi.
    public class BlockAllocation
    {
        public int?  BlockNo           { get; private set; }
        // Zone của block được chọn. Có thể khác zone ZoneRouter đưa ra, vì block
        // được chọn trên toàn bãi theo khoảng cách.
        public int?  ZoneId            { get; private set; }
        public bool  OccupancyVerified { get; private set; }

        public bool HasCapacity { get { return BlockNo.HasValue; } }

        public static BlockAllocation At(int blockNo, int zoneId, bool occupancyVerified)
        {
            return new BlockAllocation { BlockNo = blockNo, ZoneId = zoneId, OccupancyVerified = occupancyVerified };
        }

        public static BlockAllocation NoCapacity()
        {
            return new BlockAllocation { BlockNo = null, OccupancyVerified = false };
        }
    }
}
