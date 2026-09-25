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
        // Thông tin sửa được của một thẻ. KHÔNG có card_code: xem CapNhat().
        public class CardEdit
        {
            public string    CardNo         { get; set; }
            public string    CardType       { get; set; }
            public string    VehicleName    { get; set; }
            public string    Plate          { get; set; }
            public string    CustomerName   { get; set; }
            public string    WeightText     { get; set; }
            public DateTime? ExpiryDate     { get; set; }
            public int       CustomerTypeId { get; set; }
            public int       WeightClassId  { get; set; }
        }

        // Kết quả của một lần đổi trạng thái. Lý do bị chặn là thứ người vận hành
        // cần biết, nên nó là một giá trị chứ không phải một ngoại lệ.
        public enum KetQuaDoiTrangThai
        {
            Ok, KhongThay, DangTrongODo, CoPhienDangMo, VuaQuet,
            // Câu lệnh không đổi dòng nào, nhưng lúc đọc lại thì không lý do nào
            // còn đúng — một cuộc đua vừa xảy ra. KHÔNG được coi là Ok: báo thành
            // công trong khi cờ vẫn nguyên là dối người vận hành.
            KhongDoiDuoc
        }

        // Sửa thông tin thẻ. Trả về số dòng đổi (0 = không tìm thấy card_id).
        //
        // ===================== card_code KHÔNG NẰM TRONG SET =====================
        // Mã thẻ là giá trị nằm trong thanh ghi ô đỗ của PLC. Đổi nó khi xe đang
        // gửi thì thẻ trong tay tài xế và mã trong PLC không còn khớp — xe nằm đó
        // nhưng không tra ra.
        //
        // Cách khoá là KHÔNG BAO GIỜ GHI nó, chứ không phải nhận rồi so sánh rồi
        // từ chối. Ít đường hỏng hơn, và không phụ thuộc người gọi nhớ kiểm.
        //
        // ===================== ĐỊNH DANH BẰNG card_id =====================
        // WriteBatch dùng "WHERE card_code = @code" vì nó phục vụ nhập file, nơi
        // mã thẻ CHÍNH LÀ khoá tra cứu. Ở đây thì khác: mã đến từ thân POST, và
        // dùng nó làm điều kiện nghĩa là một giá trị gửi lên có thể trỏ sang thẻ
        // của khách khác và ghi đè lên đó.
        public int CapNhat(int cardId, CardEdit e)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "UPDATE parking_card SET " +
                    "  card_no          = @no, " +
                    "  card_type        = @ctype, " +
                    "  vehicle_name     = @veh, " +
                    "  plate            = @plate, " +
                    "  customer_name    = @cust, " +
                    "  weight_text      = @wtext, " +
                    "  expiry_date      = @exp, " +
                    "  customer_type_id = @ctid, " +
                    "  weight_class_id  = @wcid " +
                    "WHERE card_id = @id";

                cmd.Parameters.AddWithValue("@no",    e.CardNo);
                cmd.Parameters.AddWithValue("@ctype", (object)e.CardType     ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@veh",   (object)e.VehicleName  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@plate", (object)e.Plate        ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cust",  (object)e.CustomerName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@wtext", (object)e.WeightText   ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@exp",   (object)e.ExpiryDate   ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ctid",  e.CustomerTypeId);
                cmd.Parameters.AddWithValue("@wcid",  e.WeightClassId);
                cmd.Parameters.AddWithValue("@id",    cardId);

                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        // Tắt hoặc bật lại một thẻ.
        //
        // ===================== BA LỚP CHẶN NẰM TRONG CHÍNH CÂU LỆNH =====================
        // Kiểm trước rồi ghi sau để hở một cửa sổ: vòng quét ô đỗ chạy 45 giây một
        // lượt, và một xe có thể vừa được gửi vào giữa hai bước. Đặt cả ba điều
        // kiện vào mệnh đề WHERE thì không có cửa sổ nào cả.
        //
        // Vì sao phải chặn: CardScanService từ chối thẻ bị tắt TRƯỚC khi tra phiên
        // đang mở, nên tắt thẻ của một chiếc xe đang nằm trong bãi nghĩa là tài xế
        // quẹt thẻ ở cổng và không lấy được xe ra.
        //
        // Lớp plc_request CÓ giới hạn 24 giờ. Bảng đó là nhật ký chỉ ghi thêm,
        // không có đường xoá; chặn theo toàn bộ lịch sử sẽ khoá vĩnh viễn những
        // thẻ từng quẹt một lần từ đợt nghiệm thu, và số đó chỉ lớn lên.
        //
        // Bật lại KHÔNG cần lớp chặn nào: nó chỉ mở ra, không khoá ai cả.
        public KetQuaDoiTrangThai DatTrangThai(int cardId, bool bat)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();

                int n;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = bat
                        ? "UPDATE parking_card SET is_active = 1 WHERE card_id = @id"
                        : "UPDATE parking_card c " +
                          "LEFT JOIN plc_slot_state s ON s.card_code = c.card_code " +
                          "LEFT JOIN parking_session p ON p.card_id = c.card_id " +
                          "                          AND p.active_card_id IS NOT NULL " +
                          "LEFT JOIN plc_request r ON r.card_code = c.card_code " +
                          "                       AND r.received_at > NOW() - INTERVAL 24 HOUR " +
                          "SET c.is_active = 0 " +
                          "WHERE c.card_id = @id " +
                          "  AND s.card_code IS NULL " +
                          "  AND p.card_id  IS NULL " +
                          "  AND r.card_code IS NULL";
                    cmd.Parameters.AddWithValue("@id", cardId);
                    n = cmd.ExecuteNonQuery();
                }

                if (n > 0) return KetQuaDoiTrangThai.Ok;

                // Tới đây thì HOẶC không tìm thấy HOẶC bị chặn. Truy vấn dưới đây
                // chỉ để dựng thông báo cho đúng lý do — quyết định đã nằm trong
                // câu lệnh ở trên rồi.
                return LyDoBiChan(conn, cardId, bat);
            }
        }

        // Đọc kèm is_active để KHÔNG báo thành công nhầm.
        //
        // Chuỗi kết nối không đặt UseAffectedRows, nên ExecuteNonQuery đếm số dòng
        // KHỚP chứ không phải số dòng đổi: với một dòng có thật, n = 0 chỉ xảy ra
        // khi bị chặn. Nhưng giữa lúc câu lệnh chạy và lúc đọc lại, vòng quét 45
        // giây có thể vừa xoá mã ô hoặc phiên vừa đóng — khi đó cả ba EXISTS đều
        // 0. Nếu trả Ok ở tình huống đó thì API báo 200 và nhật ký ghi "OK" trong
        // khi is_active vẫn nguyên: đúng thứ mà nhật ký sinh ra để ngăn.
        private static KetQuaDoiTrangThai LyDoBiChan(MySqlConnection conn, int cardId, bool bat)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " +
                    "  EXISTS(SELECT 1 FROM plc_slot_state s WHERE s.card_code = c.card_code), " +
                    "  EXISTS(SELECT 1 FROM parking_session p WHERE p.card_id = c.card_id " +
                    "                                         AND p.active_card_id IS NOT NULL), " +
                    "  EXISTS(SELECT 1 FROM plc_request r WHERE r.card_code = c.card_code " +
                    "                                     AND r.received_at > NOW() - INTERVAL 24 HOUR), " +
                    "  c.is_active " +
                    "FROM parking_card c WHERE c.card_id = @id";
                cmd.Parameters.AddWithValue("@id", cardId);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return KetQuaDoiTrangThai.KhongThay;
                    if (Convert.ToInt32(r[0]) == 1) return KetQuaDoiTrangThai.DangTrongODo;
                    if (Convert.ToInt32(r[1]) == 1) return KetQuaDoiTrangThai.CoPhienDangMo;
                    if (Convert.ToInt32(r[2]) == 1) return KetQuaDoiTrangThai.VuaQuet;

                    // Không lý do nào còn đúng. Chỉ báo Ok khi cờ ĐÃ đúng như yêu
                    // cầu; ngược lại là một cuộc đua vừa xảy ra và thao tác thật
                    // sự chưa có tác dụng.
                    bool dangBat = Convert.ToBoolean(r[3]);
                    return dangBat == bat
                        ? KetQuaDoiTrangThai.Ok
                        : KetQuaDoiTrangThai.KhongDoiDuoc;
                }
            }
        }

        // Một dòng thẻ theo card_id, dùng để dựng nhật ký giá trị cũ.
        public CardRow DocTheoId(int cardId)
        {
            foreach (var r in GetAllRows())
                if (r.CardId == cardId) return r;
            return null;
        }

        // Danh sách hai bảng tra cứu, để giao diện dựng ô chọn thay vì viết cứng id.
        public IList<KeyValuePair<int, string>> DanhSachTraCuu(string bang)
        {
            // Tên bảng KHÔNG đến từ người dùng: chỉ hai giá trị cố định ở đây.
            string sql = bang == "customer_type"
                ? "SELECT customer_type_id, code FROM customer_type ORDER BY customer_type_id"
                : "SELECT weight_class_id, code FROM weight_class ORDER BY weight_class_id";

            var ra = new List<KeyValuePair<int, string>>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        ra.Add(new KeyValuePair<int, string>(
                            Convert.ToInt32(r[0]), Convert.ToString(r[1])));
                }
            }
            return ra;
        }

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
