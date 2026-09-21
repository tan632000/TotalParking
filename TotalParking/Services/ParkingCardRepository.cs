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

            // Ho so the lay tu file xuat DEC (xem Database/39_card_dec_fields.sql).
            // Deu co the null: 487 the nap truoc khi co cac cot nay.
            public string    CardType     { get; set; }
            public string    VehicleName  { get; set; }
            public string    WeightText   { get; set; }
            public string    Plate        { get; set; }
            public string    CustomerName { get; set; }
            public DateTime? ExpiryDate   { get; set; }
        }

        public IList<CardRow> GetAllRows()
        {
            var list = new List<CardRow>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT c.card_id, c.card_code, c.card_no, t.code AS customer_type, " +
                    "       w.code AS weight_class, c.source_label, c.is_active, c.created_at, " +
                    "       c.card_type, c.vehicle_name, c.weight_text, c.plate, " +
                    "       c.customer_name, c.expiry_date " +
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
                            CreatedAt    = Convert.ToDateTime(r["created_at"]),
                            CardType     = Text(r["card_type"]),
                            VehicleName  = Text(r["vehicle_name"]),
                            WeightText   = Text(r["weight_text"]),
                            Plate        = Text(r["plate"]),
                            CustomerName = Text(r["customer_name"]),
                            ExpiryDate   = r["expiry_date"] == DBNull.Value
                                               ? (DateTime?)null
                                               : Convert.ToDateTime(r["expiry_date"])
                        });
                    }
                }
            }
            return list;
        }

        // Convert.ToString(DBNull) tra ve chuoi rong, nhung viet ro ra de nguoi
        // doc sau khong phai tra lai tai lieu moi biet cot null thi ra gi.
        private static string Text(object v)
        {
            return v == DBNull.Value || v == null ? null : Convert.ToString(v);
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

        public class WriteResult
        {
            public int Inserted { get; set; }
            public int Updated  { get; set; }
        }

        // Ghi cả lô trong MỘT giao dịch.
        //
        // Hoặc vào hết, hoặc không dòng nào vào. Nạp nửa chừng rồi lỗi là trạng
        // thái tệ nhất: người dùng không biết dòng nào đã vào, nạp lại thì vướng
        // khoá duy nhất, mà gỡ ra cũng không biết gỡ tới đâu.
        //
        // Thêm mới và cập nhật đi CHUNG một giao dịch chứ không phải hai, vì lý
        // do trên áp cho cả lô: nếu 111 thẻ mới vào xong rồi 17 thẻ cập nhật mới
        // lỗi, hệ thống đứng ở trạng thái không ai mô tả được bằng một câu.
        public WriteResult WriteBatch(IList<CardImportParser.Row> toInsert,
                                      IList<CardImportParser.Row> toUpdate)
        {
            var result = new WriteResult();
            if ((toInsert == null || toInsert.Count == 0) &&
                (toUpdate == null || toUpdate.Count == 0)) return result;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    result.Inserted = Insert(conn, tx, toInsert);
                    result.Updated  = Update(conn, tx, toUpdate);
                    tx.Commit();
                }
            }
            return result;
        }

        private static int Insert(MySqlConnection conn, MySqlTransaction tx,
                                  IList<CardImportParser.Row> list)
        {
            if (list == null || list.Count == 0) return 0;

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                        "INSERT INTO parking_card " +
                        "(card_code, card_no, card_type, vehicle_name, customer_type_id, " +
                        " weight_class_id, weight_text, plate, customer_name, expiry_date, " +
                        " source_label, is_active) " +
                        "SELECT @code, @no, @ctype_txt, @veh, t.customer_type_id, " +
                        "       w.weight_class_id, @wtext, @plate, @cust, @exp, @label, @act " +
                        "FROM   customer_type t, weight_class w " +
                        "WHERE  t.code = @ctype AND w.code = @wclass";

                AddCardParams(cmd);
                cmd.Parameters.Add("@code",  MySqlDbType.String);
                cmd.Parameters.Add("@no",    MySqlDbType.String);
                cmd.Parameters.Add("@ctype", MySqlDbType.String);
                cmd.Parameters.Add("@label", MySqlDbType.String);
                cmd.Parameters.Add("@act",   MySqlDbType.Byte);

                int n = 0;
                foreach (var row in list)
                {
                    BindCardParams(cmd, row);
                    cmd.Parameters["@code"].Value  = row.CardCode;
                    cmd.Parameters["@no"].Value    = row.CardNo;
                    cmd.Parameters["@ctype"].Value = row.CustomerType;
                    cmd.Parameters["@label"].Value = row.SourceLabel;
                    cmd.Parameters["@act"].Value   = row.IsActive ? 1 : 0;
                    n += cmd.ExecuteNonQuery();
                }
                return n;
            }
        }

        // Cap nhat ho so cho the DA CO trong he thong, doi chieu bang card_code.
        //
        // ======================= KHONG SUA KHOA VA KHONG SUA LO NHAP =======================
        // card_code la dieu kien tim dong nen khong doi duoc. card_no cung khong
        // sua: no la so in tren mat the nhua, doi trong DB thi DB va cai the
        // trong tui khach noi hai chuyen khac nhau, ma khong ai phat hien duoc
        // cho den luc doi chieu tay.
        //
        // source_label giu nguyen vi no ghi lai the nay VAO he thong tu dau --
        // do la lich su, khong phai trang thai. Rieng hang tai thi ghi de: 17
        // the tu lo 17/09 dang mang gia tri doan (file do khong co cot hang tai),
        // con file nay co, nen so trong file dung hon so dang luu.
        private static int Update(MySqlConnection conn, MySqlTransaction tx,
                                  IList<CardImportParser.Row> list)
        {
            if (list == null || list.Count == 0) return 0;

            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "UPDATE parking_card c " +
                        "JOIN   weight_class w ON w.code = @wclass " +
                        "SET    c.card_type       = @ctype_txt, " +
                        "       c.vehicle_name    = @veh, " +
                        "       c.weight_class_id = w.weight_class_id, " +
                        "       c.weight_text     = @wtext, " +
                        "       c.plate           = @plate, " +
                        "       c.customer_name   = @cust, " +
                        "       c.expiry_date     = @exp " +
                        "WHERE  c.card_code = @code";

                    AddCardParams(cmd);          // @wclass da nam trong nhom nay
                    cmd.Parameters.Add("@code", MySqlDbType.String);

                    int n = 0;
                    foreach (var row in list)
                    {
                        BindCardParams(cmd, row);
                        cmd.Parameters["@code"].Value = row.CardCode;
                        // ExecuteNonQuery tra 0 khi cac cot da dung y het gia tri
                        // moi. Dem dong DA XU LY chu khong dem dong MySQL doi byte,
                        // neu khong thi nap lai cung mot file se bao "0 the cap
                        // nhat" trong khi 17 the that su da dung du lieu moi nhat.
                        cmd.ExecuteNonQuery();
                        n++;
                    }
                    return n;
                }
            }
        }

        // Bay tham so dung chung giua INSERT va UPDATE. Khai bao mot lan de hai
        // lenh khong the lech nhau ve kieu du lieu hay ve cach xu ly null.
        private static void AddCardParams(MySqlCommand cmd)
        {
            cmd.Parameters.Add("@ctype_txt", MySqlDbType.String);
            cmd.Parameters.Add("@veh",       MySqlDbType.String);
            cmd.Parameters.Add("@wtext",     MySqlDbType.String);
            cmd.Parameters.Add("@plate",     MySqlDbType.String);
            cmd.Parameters.Add("@cust",      MySqlDbType.String);
            cmd.Parameters.Add("@exp",       MySqlDbType.Date);
            cmd.Parameters.Add("@wclass",    MySqlDbType.String);
        }

        private static void BindCardParams(MySqlCommand cmd, CardImportParser.Row row)
        {
            // O trong trong Excel ve day la chuoi rong. Luu NULL chu khong luu
            // chuoi rong: "chua co du lieu" va "co du lieu la rong" la hai y
            // khac nhau, va chi NULL moi loc duoc bang IS NULL.
            cmd.Parameters["@ctype_txt"].Value = Nullable(row.CardType);
            cmd.Parameters["@veh"].Value       = Nullable(row.VehicleName);
            cmd.Parameters["@wtext"].Value     = Nullable(row.WeightText);
            cmd.Parameters["@plate"].Value     = Nullable(row.Plate);
            cmd.Parameters["@cust"].Value      = Nullable(row.CustomerName);
            cmd.Parameters["@exp"].Value       = row.ExpiryDate.HasValue
                                                     ? (object)row.ExpiryDate.Value
                                                     : DBNull.Value;
            cmd.Parameters["@wclass"].Value    = row.WeightClass;
        }

        private static object Nullable(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? (object)DBNull.Value : s;
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
