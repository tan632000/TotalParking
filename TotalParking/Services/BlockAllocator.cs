using System;
using MySqlConnector;

namespace TotalParking.Services
{
    // Chọn block đích trong zone mà ZoneRouter đã chọn.
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

        // Xếp hạng ứng viên:
        //   1. block được PLC báo cáo gần đây đứng trước block không được báo cáo
        //   2. còn nhiều chỗ hơn đứng trước
        //   3. hoà nhau thì block_no nhỏ hơn thắng, để kết quả luôn xác định
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
        // ===================== CỐ Ý KHÔNG ĐỤNG `fresh_reads` =====================
        //
        // Khối đỗ nền không có dòng `plc_slot_state` nào nên `fresh_reads` của chúng
        // luôn bằng 0, và mệnh đề `(fresh_reads > 0) DESC` đẩy chúng xuống cuối bảng
        // xếp hạng. Có thể cho chúng dùng `standard_fresh` để cạnh tranh sòng phẳng,
        // nhưng ĐỪNG — ít nhất là chưa.
        //
        // Lý do: `Allocate` chỉ nhận `zoneId`, KHÔNG nhận hạng tải. Nó không phân
        // biệt được xe cần pallet cơ khí với xe phải xuống nền. Thử cho khối đỗ nền
        // cạnh tranh bằng `standard_fresh` thì zone 2 và zone 5 lập tức đổi từ khối
        // 80 và 19 sang khối 902 và 905 — tức xe cơ khí bị chỉ xuống bãi nền.
        //
        // Giữ chúng ở cuối là lớp chắn tình cờ cho lỗ hổng đó. Sửa đúng là truyền
        // hạng tải vào đây rồi lọc theo `kind`; việc ấy đổi chữ ký hàm và hai nơi
        // gọi, nên để thành một thay đổi riêng có chủ đích.
        private const string PickSql =
            "SELECT c.block_no, c.free_capacity, c.fresh_reads FROM ( " +
            "  SELECT b.block_no AS block_no, " +
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
            "         (SELECT COUNT(*) FROM plc_slot_state s WHERE s.block_id = b.block_id " +
            "            AND s.read_at >= NOW() - INTERVAL @plc_fresh MINUTE) AS fresh_reads " +
            "  FROM   block b " +
            "  WHERE  b.zone_id = @zone_id AND b.is_active = 1 " +
            ") c " +
            "WHERE  c.free_capacity > 0 " +
            "ORDER  BY (c.fresh_reads > 0) DESC, c.free_capacity DESC, c.block_no ASC " +
            "LIMIT  1";

        // eventId được loại khỏi phép trừ: một sự kiện không tự trừ suất của chính nó khi
        // camera gửi lại cùng event_id, nếu không lần gửi lại sẽ bị đẩy sang block khác.
        public BlockAllocation Allocate(int zoneId, string eventId)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = PickSql;
                cmd.Parameters.AddWithValue("@zone_id",    zoneId);
                cmd.Parameters.AddWithValue("@event_id",   eventId ?? string.Empty);
                cmd.Parameters.AddWithValue("@window",     PendingDebitWindowSeconds);
                cmd.Parameters.AddWithValue("@plc_fresh",  PlcFreshMinutes);

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    // Không dòng nào = không block nào trong zone còn chỗ. Trả về "hết chỗ"
                    // chứ KHÔNG trả về một block bừa: gửi tài xế tới chỗ đã đầy còn tệ hơn
                    // là nói thẳng rằng bãi đã hết chỗ.
                    if (!r.Read()) return BlockAllocation.NoCapacity();

                    return BlockAllocation.At(
                        Convert.ToInt32(r["block_no"]),
                        // Nhãn này đi theo quyết định, không tính lại lúc đọc.
                        Convert.ToInt32(r["fresh_reads"]) > 0);
                }
            }
        }
    }

    // Kết quả chọn block. `BlockNo` null nghĩa là zone đã hết chỗ, không phải lỗi.
    public class BlockAllocation
    {
        public int?  BlockNo           { get; private set; }
        public bool  OccupancyVerified { get; private set; }

        public bool HasCapacity { get { return BlockNo.HasValue; } }

        public static BlockAllocation At(int blockNo, bool occupancyVerified)
        {
            return new BlockAllocation { BlockNo = blockNo, OccupancyVerified = occupancyVerified };
        }

        public static BlockAllocation NoCapacity()
        {
            return new BlockAllocation { BlockNo = null, OccupancyVerified = false };
        }
    }
}
