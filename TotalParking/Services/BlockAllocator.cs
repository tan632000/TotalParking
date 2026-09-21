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
        private const string PickSql =
            "SELECT c.block_no, c.free_capacity, c.fresh_reads FROM ( " +
            "  SELECT b.block_no AS block_no, " +
            "         b.slot_count " +
            "           - (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id = b.block_id) " +
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
