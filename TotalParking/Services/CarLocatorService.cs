using System;
using MySqlConnector;

namespace TotalParking.Services
{
    // Tra vị trí xe theo mã thẻ, cho luồng TÌM XE.
    //
    // Khách quẹt RFID ở một block bất kỳ -> PLC ghi mã thẻ vào D1002 -> SCADA đọc,
    // tra ở đây, rồi ghi SỐ BLOCK nơi xe đang đỗ xuống D1000 của chính PLC đó.
    //
    // Nguồn sự thật là plc_slot_state: chính các PLC đã báo ô nào đang giữ thẻ nào.
    // KHÔNG dùng parking_session — bảng đó ghi ý định của SCADA, còn thanh ghi ô
    // ghi sự thật của thiết bị. Hai cái lệch nhau ngay khi có người thao tác tay
    // tại HMI mà SCADA không biết, và trong giai đoạn nghiệm thu thì chuyện đó
    // xảy ra liên tục.
    public class CarLocatorService
    {
        // Giá trị ghi xuống D1000 khi không tìm thấy xe.
        //
        // 0 chứ không phải để nguyên giá trị cũ: để nguyên thì khách quẹt thẻ lạ sẽ
        // thấy số block của người trước đó và đi tới block không có xe mình.
        public const int NotFound = 0;

        // Trả về số block đang giữ thẻ này, hoặc NotFound.
        //
        // ===================== LỌC DỮ LIỆU RÁC =====================
        // Thanh ghi ô có thể mang giá trị khác 0 mà không phải mã thẻ. Thực tế đang
        // có giá trị '03010000' xuất hiện ở BẢY block cùng lúc — đó là thanh ghi
        // nội bộ của ladder, không phải xe.
        //
        // Cách lọc: MỘT MÃ THẺ CHỈ ĐƯỢC NẰM Ở ĐÚNG MỘT BLOCK. Xe không ở hai nơi
        // cùng lúc, nên mã nào xuất hiện ở nhiều block là rác — bất kể nó trông
        // giống mã thẻ đến đâu.
        //
        // Quy tắc này TỰ KIỂM và không cần thẻ phải đăng ký trong parking_card.
        // Đó là điểm quan trọng: thẻ test ngoài hiện trường không có trong danh sách
        // đăng ký, nếu bắt buộc phải đăng ký thì mọi lượt test đều trả 'không tìm
        // thấy' và không phân biệt được với lỗi thật.
        //
        // Muốn chặt hơn (chỉ chấp nhận thẻ đã đăng ký) thì đặt
        // plc:requireRegisteredCard = true trong Web.config.
        private const string SqlBase =
            "SELECT b.block_no " +
            "FROM   plc_slot_state s " +
            "JOIN   block b ON b.block_id = s.block_id " +
            "{0}" +
            "WHERE  s.card_code = @code " +
            // Mã nằm ở nhiều block = rác, loại thẳng.
            "  AND  (SELECT COUNT(DISTINCT s2.block_id) FROM plc_slot_state s2 " +
            "        WHERE s2.card_code = @code) = 1 " +
            // Giá trị quá nhỏ = rác, loại thẳng. Xem v_slot_taken (file 27):
            // sau khi có điện lại ngày 17/09, D400 của block 27 mang giá trị
            // '000003e8' (= 1000) — ladder khởi tạo lại, không phải xe. Nó chỉ
            // nằm ở một block nên quy tắc duy nhất ở trên KHÔNG bắt được.
            //
            // Phải trùng khít với điều kiện trong v_slot_taken, nếu không thì số
            // trên bảng LED và kết quả tìm xe sẽ nói hai chuyện khác nhau.
            "  AND  (LENGTH(s.card_code) < 8 OR CONV(s.card_code, 16, 10) > 65535) " +
            // Ô nào vừa đổi gần đây nhất thì tin hơn.
            "ORDER  BY s.changed_at DESC, s.read_at DESC " +
            "LIMIT  1";

        private static string Sql
        {
            get
            {
                string v = System.Configuration.ConfigurationManager
                               .AppSettings["plc:requireRegisteredCard"];
                bool strict;
                if (!bool.TryParse(v, out strict)) strict = false;
                return string.Format(SqlBase,
                    strict ? "JOIN parking_card c ON c.card_code = s.card_code " : "");
            }
        }

        public int FindBlockNo(string cardCode)
        {
            if (string.IsNullOrWhiteSpace(cardCode)) return NotFound;
            cardCode = cardCode.Trim();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = Sql;
                cmd.Parameters.AddWithValue("@code", cardCode);
                conn.Open();
                object v = cmd.ExecuteScalar();
                return v == null || v == DBNull.Value ? NotFound : Convert.ToInt32(v);
            }
        }
    }
}
