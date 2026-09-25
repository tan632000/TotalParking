using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Đọc cấu hình PLC. Bảng này đổi rất hiếm nên chỉ nạp lúc khởi động và khi
    // có thao tác cấu hình — không truy vấn mỗi vòng poll.
    //
    // ===================== TRẠNG THÁI KẾT NỐI CŨNG NẰM Ở ĐÂY =====================
    // Trước đây chú thích này nói trạng thái kết nối KHÔNG được để trong bảng
    // cấu hình, vì 112 PLC poll 500ms sẽ thành hàng chục UPDATE mỗi giây. Lo ngại
    // đó vẫn đúng, nhưng ngày 24/09 người dùng chọn đưa nó vào bảng để truy vấn
    // được bằng SQL cùng chỗ với cấu hình.
    //
    // Nên cách ghi phải gánh lấy lo ngại đó, và nó gánh bằng ba lớp:
    //   1. PlcTrangThaiWriter chiếu mỗi 5 giây, không phải mỗi nhịp poll;
    //   2. chỉ ghi khi trạng thái đổi, và phải ổn định vài lượt mới được ghi;
    //   3. last_probe_at cập nhật cả bảng bằng MỘT câu, để cột không nói dối khi
    //      site chết — đó là thứ duy nhất phân biệt "online ổn định ba ngày" với
    //      "site chết năm phút trước".
    //
    // Nguồn sự thật lúc chạy vẫn là PlcConnection.IsOnline trong bộ nhớ; ba cột
    // này là bản chiếu, hệ thống không đọc ngược lại chúng để ra quyết định.
    public class PlcDeviceRepository
    {
        private const string SelectSql =
            "SELECT p.plc_id, p.block_id, b.block_no, b.zone_id, " +
            "       p.ip_address, p.port, p.plc_node, p.pc_node, " +
            "       p.timeout_ms, p.poll_ms, " +
            "       p.card_word, p.card_word_len, p.card_layout, " +
            "       p.request_bit, p.request_bit_area, " +
            "       p.permit_bit, p.permit_bit_area, p.class_word, " +
            "       p.find_card_word, p.find_card_len, p.find_answer_word, " +
            "       p.scan_card_word, p.scan_card_len, p.weight_band_word, " +
            "       p.is_active " +
            "FROM   plc_device p " +
            "JOIN   block b ON b.block_id = p.block_id ";

        public IList<PlcDevice> GetAll(bool activeOnly = true)
        {
            var result = new List<PlcDevice>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // CHỈ lọc theo cờ của thiết bị, KHÔNG lọc theo cờ của khối.
                //
                // Hai cờ mang hai nghĩa khác hẳn nhau:
                //   plc_device.is_active  "có kết nối và đọc thiết bị này không"
                //   block.is_active       "khối này có nhận xe và tính vào sức chứa không"
                //
                // Trộn chúng ở đây làm khối bị tắt vận hành cũng biến mất khỏi
                // vòng poll, nên không ai còn biết PLC của nó sống hay chết —
                // đúng lúc cần biết nhất. Ngược lại, tách ra thì một khối đang
                // sửa chữa vẫn được giám sát, và vẫn trả xe ra được: đường lấy
                // xe (CarLocatorService) không đọc block.is_active, chỉ
                // BlockAllocator đọc để thôi xếp xe MỚI vào đó.
                cmd.CommandText = SelectSql +
                    (activeOnly ? "WHERE p.is_active = 1 " : "") +
                    "ORDER BY b.block_no";

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }

            return result;
        }

        // Convert.To* thay vi GetInt32: cot TINYINT/SMALLINT UNSIGNED tra ve
        // kieu nho hon Int32, va GetInt32 tren mot so kieu se nem InvalidCast.
        // Day cung la khuon ma ParkingCardRepository dang dung.
        private static PlcDevice Map(IDataRecord r)
        {
            return new PlcDevice
            {
                PlcId          = Convert.ToInt32(r["plc_id"]),
                BlockId        = Convert.ToInt32(r["block_id"]),
                BlockNo        = Convert.ToInt32(r["block_no"]),
                ZoneId         = Convert.ToInt32(r["zone_id"]),
                IpAddress      = Convert.ToString(r["ip_address"]),
                Port           = Convert.ToInt32(r["port"]),
                PlcNode        = Convert.ToByte(r["plc_node"]),
                PcNode         = Convert.ToByte(r["pc_node"]),
                TimeoutMs      = Convert.ToInt32(r["timeout_ms"]),
                PollMs         = Convert.ToInt32(r["poll_ms"]),
                CardWord       = Convert.ToInt32(r["card_word"]),
                CardWordLen    = Convert.ToInt32(r["card_word_len"]),
                CardLayout     = GetNullableString(r, "card_layout"),
                RequestBit     = GetNullableString(r, "request_bit"),
                RequestBitArea = Convert.ToString(r["request_bit_area"]),
                PermitBit      = Convert.ToString(r["permit_bit"]),
                PermitBitArea  = Convert.ToString(r["permit_bit_area"]),
                ClassWord      = Convert.ToInt32(r["class_word"]),
                FindCardWord   = Convert.ToInt32(r["find_card_word"]),
                FindCardLen    = Convert.ToInt32(r["find_card_len"]),
                FindAnswerWord = Convert.ToInt32(r["find_answer_word"]),
                ScanCardWord   = Convert.ToInt32(r["scan_card_word"]),
                ScanCardLen    = Convert.ToInt32(r["scan_card_len"]),
                WeightBandWord = Convert.ToInt32(r["weight_band_word"]),
                IsActive       = Convert.ToBoolean(r["is_active"])
            };
        }

        private static string GetNullableString(IDataRecord r, string column)
        {
            object v = r[column];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }

        // ===================== GHI BẢN CHIẾU TRẠNG THÁI KẾT NỐI =====================
        // Ba phương thức dưới đây chỉ được gọi từ PlcTrangThaiWriter, chạy trên
        // luồng riêng. Chúng KHÔNG được gọi từ vòng poll: một MySqlException ném
        // ra trong LoopAsync sẽ giết vòng poll vĩnh viễn mà IsRunning vẫn báo true.

        // Cập nhật mốc quan sát cho toàn bộ thiết bị đang trong vòng poll, bằng
        // MỘT câu lệnh. Chạy mỗi lượt chiếu kể cả khi không có gì đổi — đó chính
        // là điều làm nó hữu ích: cột này cũ nghĩa là số liệu đang đóng băng.
        public void GhiMocQuanSat(ICollection<int> plcIds)
        {
            if (plcIds == null || plcIds.Count == 0) return;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // plcIds đến từ danh sách kết nối trong bộ nhớ, không phải từ đầu
                // vào người dùng, nhưng vẫn nối bằng số nguyên đã ép kiểu để
                // không mở đường chèn SQL nếu sau này nguồn đổi.
                var ids = string.Join(",", plcIds.Select(x => x.ToString()));
                cmd.CommandText = "UPDATE plc_device SET last_probe_at = NOW(3) " +
                                  "WHERE plc_id IN (" + ids + ")";
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public void GhiTrangThaiKetNoi(int plcId, bool ketNoiDuoc)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // connected_changed_at CHỈ nhích khi giá trị thật sự đổi.
                //
                // Trước đây nó được đặt NOW(3) vô điều kiện, và điều đó làm cột
                // nói dối sau mỗi lần ứng dụng khởi động lại: PlcTrangThaiWriter
                // giữ trạng thái đã ghi trong RAM, nên sau recycle nó coi mọi
                // thiết bị là "chưa biết" và ghi lại cả 112 dòng. Đo được ngày
                // 24/09: toàn bộ 112 dòng cùng một mốc 22:56, trong khi vài PLC
                // đã chết từ hôm trước.
                //
                // Hệ quả không chỉ là hiển thị sai: CongVanHanhService dùng đúng
                // cột này để đếm "mất kết nối bao lâu rồi", nên mỗi lần recycle
                // là đồng hồ đó bị đặt lại từ đầu.
                //
                // <=> là toán tử so sánh an-toàn-NULL của MySQL: NULL <=> NULL
                // cho TRUE, nên thiết bị vừa rời vòng poll rồi quay lại cũng
                // không bị nhích mốc oan.
                cmd.CommandText =
                    "UPDATE plc_device " +
                    "SET connected_changed_at = IF(is_connected <=> @noi, connected_changed_at, NOW(3)), " +
                    "    is_connected  = @noi, " +
                    "    last_probe_at = NOW(3) " +
                    "WHERE plc_id = @id";
                cmd.Parameters.AddWithValue("@noi", ketNoiDuoc ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", plcId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Thiết bị rời vòng poll thì không còn quan sát được. NULL nghĩa là
        // "không biết", khác hẳn 0 nghĩa là "biết chắc đang chết".
        public void XoaTrangThaiKetNoi(ICollection<int> plcIds)
        {
            if (plcIds == null || plcIds.Count == 0) return;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // Cùng lý do như GhiTrangThaiKetNoi: chỉ nhích mốc khi thật sự
                // đổi. Thiết bị đã là NULL rồi mà gọi lại thì không được coi là
                // một lần đổi trạng thái.
                var ids = string.Join(",", plcIds.Select(x => x.ToString()));
                cmd.CommandText = "UPDATE plc_device " +
                                  "SET connected_changed_at = IF(is_connected IS NULL, connected_changed_at, NOW(3)), " +
                                  "    is_connected = NULL " +
                                  "WHERE plc_id IN (" + ids + ")";
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
