using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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

        // ------------------------------------------------------------ quản trị thẻ
        //
        // Danh sách cho màn hình quản lý. KHÔNG dùng v_parking_card vì view đó
        // thiếu `source_label` — cột dùng để lọc và gỡ theo lô, thứ cần nhất khi
        // một lô nhập sai và phải rút lại.
        public class CardRow
        {
            public int    CardId       { get; set; }
            public string CardCode     { get; set; }
            public string CardNo       { get; set; }
            public string CustomerType { get; set; }
            public string WeightClass  { get; set; }
            public string SourceLabel  { get; set; }
            public bool   IsActive     { get; set; }
            public DateTime CreatedAt  { get; set; }
        }

        public IList<CardRow> GetAllRows()
        {
            var list = new List<CardRow>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT c.card_id, c.card_code, c.card_no, t.code AS customer_type, " +
                    "       w.code AS weight_class, c.source_label, c.is_active, c.created_at " +
                    "FROM   parking_card c " +
                    "JOIN   customer_type t ON t.customer_type_id = c.customer_type_id " +
                    "JOIN   weight_class  w ON w.weight_class_id  = c.weight_class_id " +
                    "ORDER  BY c.card_no";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new CardRow
                        {
                            CardId       = Convert.ToInt32(r["card_id"]),
                            CardCode     = Convert.ToString(r["card_code"]),
                            CardNo       = Convert.ToString(r["card_no"]),
                            CustomerType = Convert.ToString(r["customer_type"]),
                            WeightClass  = Convert.ToString(r["weight_class"]),
                            SourceLabel  = Convert.ToString(r["source_label"]),
                            IsActive     = Convert.ToBoolean(r["is_active"]),
                            CreatedAt    = Convert.ToDateTime(r["created_at"])
                        });
                    }
                }
            }
            return list;
        }

        // Mã thẻ và số thẻ ĐÃ CÓ trong hệ thống. Dùng ở bước xem trước để báo
        // trước dòng nào sẽ bị bỏ vì trùng, thay vì để người dùng bấm nạp rồi mới
        // biết. `card_code` và `card_no` đều là khoá duy nhất.
        public void LoadExistingKeys(HashSet<string> codes, HashSet<string> nos)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT card_code, card_no FROM parking_card";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        codes.Add(Convert.ToString(r["card_code"]));
                        nos.Add(Convert.ToString(r["card_no"]));
                    }
                }
            }
        }

        // Ghi cả lô trong MỘT giao dịch.
        //
        // Hoặc vào hết, hoặc không dòng nào vào. Nạp nửa chừng rồi lỗi là trạng
        // thái tệ nhất: người dùng không biết dòng nào đã vào, nạp lại thì vướng
        // khoá duy nhất, mà gỡ ra cũng không biết gỡ tới đâu.
        public int InsertBatch(IEnumerable<CardCsvParser.Row> rows)
        {
            var list = rows.ToList();
            if (list.Count == 0) return 0;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "INSERT INTO parking_card " +
                        "(card_code, card_no, customer_type_id, weight_class_id, source_label, is_active) " +
                        "SELECT @code, @no, t.customer_type_id, w.weight_class_id, @label, @act " +
                        "FROM   customer_type t, weight_class w " +
                        "WHERE  t.code = @ctype AND w.code = @wclass";

                    cmd.Parameters.Add("@code",   MySqlDbType.String);
                    cmd.Parameters.Add("@no",     MySqlDbType.String);
                    cmd.Parameters.Add("@ctype",  MySqlDbType.String);
                    cmd.Parameters.Add("@wclass", MySqlDbType.String);
                    cmd.Parameters.Add("@label",  MySqlDbType.String);
                    cmd.Parameters.Add("@act",    MySqlDbType.Byte);

                    int n = 0;
                    foreach (var row in list)
                    {
                        cmd.Parameters["@code"].Value   = row.CardCode;
                        cmd.Parameters["@no"].Value     = row.CardNo;
                        cmd.Parameters["@ctype"].Value  = row.CustomerType;
                        cmd.Parameters["@wclass"].Value = row.WeightClass;
                        cmd.Parameters["@label"].Value  = row.SourceLabel;
                        cmd.Parameters["@act"].Value    = row.IsActive ? 1 : 0;
                        n += cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                    return n;
                }
            }
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
