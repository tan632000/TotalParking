using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Tra vị trí xe theo mã thẻ / số thẻ / biển số, cho ô "Tìm vị trí" ở trang
    // Báo cáo.
    //
    // Trả về DANH SÁCH chứ không phải một kết quả: một thẻ dùng nhiều lần theo
    // thời gian nên tra ra nhiều phiên. Sắp xếp để phiên ĐANG MỞ luôn đứng đầu —
    // người hỏi "xe tôi đang ở đâu" cần câu trả lời đó trước, lịch sử là phụ.
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
            "SELECT s.session_id, s.status, s.plate, " +
            "       c.card_code, c.card_no, " +
            "       s.zone_id, z.code AS zone_code, z.name AS zone_name, " +
            "       s.block_id, b.block_no, b.kind AS block_kind, b.slot_count, " +
            "       sl.label AS slot_label, " +
            "       s.created_at, s.parked_at, s.completed_at, " +
            "       (s.active_card_id IS NOT NULL) AS is_active " +
            "FROM   parking_session s " +
            "JOIN   parking_card c ON c.card_id = s.card_id " +
            "LEFT   JOIN zone  z  ON z.zone_id  = s.zone_id " +
            "LEFT   JOIN block b  ON b.block_id = s.block_id " +
            "LEFT   JOIN parking_slot sl ON sl.slot_id = s.slot_id " +
            "WHERE  c.card_code = @q OR c.card_no = @q OR s.plate = @q " +
            // Phiên đang mở lên đầu, rồi tới phiên mới nhất.
            "ORDER  BY (s.active_card_id IS NOT NULL) DESC, s.created_at DESC " +
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
            return new VehicleLocation
            {
                SessionId   = Convert.ToInt64(r["session_id"]),
                Status      = Convert.ToString(r["status"]),
                IsActive    = Convert.ToBoolean(r["is_active"]),
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
                SlotLabel   = Str(r, "slot_label"),
                CreatedAt   = Convert.ToDateTime(r["created_at"]),
                ParkedAt    = Date(r, "parked_at"),
                CompletedAt = Date(r, "completed_at")
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
