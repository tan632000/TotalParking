using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Truy cập danh sách thẻ xe. ADO.NET thuần: schema nhỏ, chỉ hai thao tác, không
    // đáng kéo Entity Framework vào một project chưa hề có ORM.
    //
    // Đọc từ view v_parking_card nên phía gọi làm việc với mã chữ (VANG / 2200KG),
    // không phải nhớ mã số trong bảng tra cứu.
    public class ParkingCardRepository
    {
        private const string SelectColumns =
            "card_id, card_code, card_no, customer_type, customer_type_name, " +
            "weight_class, max_weight_kg, is_active";

        // Đường nóng: quét thẻ tại HMI -> tra hạng tải để trả xuống PLC.
        // Trả null khi thẻ không có trong danh sách đăng ký.
        public ParkingCard FindByCardCode(string cardCode)
        {
            if (string.IsNullOrEmpty(cardCode)) return null;
            cardCode = cardCode.Trim();
            if (cardCode.Length == 0) return null;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " + SelectColumns + " FROM v_parking_card WHERE card_code = @code";
                cmd.Parameters.AddWithValue("@code", cardCode);

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    return reader.Read() ? Map(reader) : null;
                }
            }
        }

        // Toàn bộ danh sách, dùng khi nạp bảng thẻ xuống PLC. 467 bản ghi nên đọc
        // một lần vào bộ nhớ là hợp lý; phía gọi tự lọc theo nhu cầu.
        public IList<ParkingCard> GetAll(bool activeOnly = true)
        {
            var result = new List<ParkingCard>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " + SelectColumns + " FROM v_parking_card " +
                    (activeOnly ? "WHERE is_active = 1 " : "") +
                    "ORDER BY card_no";

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }

            return result;
        }

        private static ParkingCard Map(IDataRecord r)
        {
            return new ParkingCard
            {
                CardId           = Convert.ToInt32(r["card_id"]),
                CardCode         = Convert.ToString(r["card_code"]),
                CardNo           = Convert.ToString(r["card_no"]),
                CustomerType     = Convert.ToString(r["customer_type"]),
                CustomerTypeName = Convert.ToString(r["customer_type_name"]),
                WeightClass      = Convert.ToString(r["weight_class"]),
                MaxWeightKg      = r["max_weight_kg"] == DBNull.Value
                                       ? (int?)null
                                       : Convert.ToInt32(r["max_weight_kg"]),
                IsActive         = Convert.ToBoolean(r["is_active"])
            };
        }
    }
}
