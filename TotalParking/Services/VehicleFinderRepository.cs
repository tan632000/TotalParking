using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Tra vị trí xe theo mã thẻ / số thẻ / biển số, cho ô "Tìm vị trí" ở trang
    // Báo cáo và trang Truy vết.
    //
    // ===================== VÌ SAO ĐỌC THANH GHI =====================
    // Bản trước tra bảng parking_session. Bảng đó có 0 dòng, và nó rỗng không
    // phải vì bãi vắng: hàm duy nhất tạo phiên (CardScanService.TryOpenSession)
    // không còn nơi nào gọi tới. Hệ quả là ô tìm xe luôn trả "thẻ hợp lệ nhưng
    // chưa có phiên gửi xe nào" kể cả khi chiếc xe đang nằm trong khối và
    // /SlotStatus nhìn thấy nó.
    //
    // Nguồn duy nhất biết xe đang ở đâu là plc_slot_state — thanh ghi do vòng
    // quét PLC ghi về. Repository này đọc thẳng từ đó.
    //
    // ===================== KHÁC CarLocatorService MỘT CHỖ =====================
    // CarLocatorService trả lời cho PLC nên phải chọn ĐÚNG MỘT khối, và nó từ
    // chối trả lời khi một mã thẻ nằm ở nhiều khối — chỉ sai khối thì tài xế đi
    // nhầm tầng.
    //
    // Ở đây người đọc là nhân viên trực, không phải cơ cấu cơ khí. Thấy cả hai
    // vị trí và tự đi kiểm tra là việc làm được, và hữu ích hơn hẳn một câu
    // "không tìm thấy". Nên chỗ này TRẢ VỀ TẤT CẢ, kèm cảnh báo THE_TRUNG_BLOCK
    // mà CanhBaoTheTrungService đã sinh sẵn cho đúng tình huống đó.
    //
    // Repository này CHỈ ĐỌC. Không có đường nào từ đây ghi xuống PLC hay đổi
    // trạng thái phiên: tra cứu vị trí không được phép gây tác dụng phụ.
    public class VehicleFinderRepository
    {
        // Giới hạn số phiên trả về. Đủ để thấy lịch sử gần đây của một thẻ mà
        // không biến ô tìm kiếm thành công cụ xuất báo cáo.
        private const int MaxResults = 10;

        // static readonly chu khong phai const: chuoi nay noi voi MaxResults (int),
        // ma noi string voi int can int.ToString() nen khong phai bieu thuc hang.
        private static readonly string SelectSql =
            "SELECT s.block_id, s.slot_index, s.word_addr, s.card_code, " +
            "       s.read_at, s.changed_at, " +
            "       c.card_no, c.plate, " +
            "       b.block_no, b.zone_id, b.kind AS block_kind, b.slot_count, " +
            "       z.code AS zone_code, z.name AS zone_name " +
            "FROM   plc_slot_state s " +
            "JOIN   block b ON b.block_id = s.block_id " +
            "LEFT   JOIN zone z ON z.zone_id = b.zone_id " +
            // JOIN chu khong LEFT JOIN: chi tra ve the CO trong danh muc. Mot ma
            // la trong thanh ghi khong tra cuu duoc bang bien so hay so the, va
            // hien no ra day chi lam nhieu.
            "JOIN   parking_card c ON c.card_code = s.card_code " +
            "WHERE  s.card_code IS NOT NULL " +
            "  AND  (c.card_code = @q OR c.card_no = @q OR c.plate = @q) " +
            // Cung nguong loc rac voi v_slot_taken va CarLocatorService: gia tri
            // duoi 65536 la so dem hoac thanh ghi noi bo cua ladder, khong phai
            // ma the. Ba noi phai noi cung mot thu tieng.
            "  AND  (LENGTH(s.card_code) < 8 OR CONV(s.card_code, 16, 10) > 65535) " +
            // O nao vua doi gan day nhat thi tin hon.
            "ORDER  BY s.changed_at DESC, s.read_at DESC " +
            "LIMIT  " + MaxResults;

        // Trả về danh sách rỗng khi không tìm thấy — KHÔNG bịa kết quả.
        //
        // Phiên bản cũ của ô tìm kiếm này sinh ngẫu nhiên một mã thẻ rồi luôn trả
        // về "Block A-01 / Pallet P08" cho mọi truy vấn không khớp, nên gõ gì cũng
        // ra xe. Nhân viên đi tìm chiếc xe không tồn tại là hỏng việc thật, nên
        // "không tìm thấy" phải là một câu trả lời hợp lệ.
        public IList<VehicleLocation> Find(string query)
        {
            var result = new List<VehicleLocation>();

            if (string.IsNullOrWhiteSpace(query)) return result;
            query = query.Trim();
            if (query.Length == 0) return result;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SelectSql;
                cmd.Parameters.AddWithValue("@q", query);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }

            return result;
        }

        // Thẻ có tồn tại trong danh sách đăng ký hay không — dùng để phân biệt hai
        // câu trả lời rất khác nhau:
        //   "thẻ này không có trong hệ thống"  (gõ sai, hoặc thẻ chưa đăng ký)
        //   "thẻ hợp lệ nhưng chưa từng gửi xe"
        // Gộp cả hai thành "không tìm thấy" là bắt nhân viên tự đoán.
        public bool CardExists(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            query = query.Trim();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT 1 FROM parking_card WHERE card_code = @q OR card_no = @q LIMIT 1";
                cmd.Parameters.AddWithValue("@q", query);

                conn.Open();
                return cmd.ExecuteScalar() != null;
            }
        }

        // Convert.To* thay vi GetInt32: cot TINYINT/SMALLINT UNSIGNED tra ve kieu
        // nho hon Int32 va GetInt32 se nem InvalidCast. Cung khuon voi
        // ParkingCardRepository va PlcDeviceRepository.
        private static VehicleLocation Map(IDataRecord r)
        {
            // changed_at = lan cuoi O NAY doi ma the, tuc la luc chiec xe nay
            // vao o. NULL nghia la dong duoc ghi lan dau va chua doi lan nao —
            // lui ve read_at chu khong de DateTime.MinValue, neu khong thi cot
            // "thoi gian gui" se hien mot con so hang nghin nam.
            DateTime vaoLuc = r["changed_at"] == DBNull.Value
                ? Convert.ToDateTime(r["read_at"])
                : Convert.ToDateTime(r["changed_at"]);

            int slotIndex = Convert.ToInt32(r["slot_index"]);
            int wordAddr  = Convert.ToInt32(r["word_addr"]);

            return new VehicleLocation
            {
                // Khong co phien gui xe nao dung sau ket qua nay — no den tu
                // thanh ghi. Giao dien khong hien session_id, nhung de 0 thay vi
                // bia mot so la cach trung thuc duy nhat.
                SessionId   = 0,
                // Chuoi nay duoc giao dien in ra sau "DANG TRONG BAI - ", nen no
                // phai noi ro nguon tin chu khong mo ta mot trang thai phien.
                Status      = "theo thanh ghi PLC",
                // Chi tra ve o DANG giu ma the, nen luon la dang trong bai.
                IsActive    = true,
                Plate       = Str(r, "plate"),
                CardCode    = Convert.ToString(r["card_code"]),
                CardNo      = Str(r, "card_no"),
                ZoneId      = Int(r, "zone_id"),
                ZoneCode    = Str(r, "zone_code"),
                ZoneName    = Str(r, "zone_name"),
                BlockId     = Int(r, "block_id"),
                BlockNo     = Int(r, "block_no"),
                BlockKind   = Str(r, "block_kind"),
                SlotCount   = Int(r, "slot_count"),
                // Bang parking_slot rong nen khong co nhan o dat san. Nhung thanh
                // ghi biet chinh xac o nao va dia chi nao, va do la thu nguoi di
                // tim xe can — kem theo dia chi de ky thuat vien doi chieu duoc.
                SlotLabel   = "Ô " + slotIndex.ToString("00") + " (D" + wordAddr + ")",
                CreatedAt   = vaoLuc,
                ParkedAt    = vaoLuc,
                CompletedAt = null
            };
        }

        private static string Str(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }

        private static int? Int(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        private static DateTime? Date(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
