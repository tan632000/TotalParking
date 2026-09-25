using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;

namespace TotalParking.Services
{
    // Một dòng cảnh báo, đọc từ bảng canh_bao.
    public class CanhBao
    {
        public long   Id        { get; set; }
        public string Nguon     { get; set; }
        public string MucDo     { get; set; }
        public string MaLoi     { get; set; }
        public int?   BlockNo   { get; set; }
        public int?   ZoneId    { get; set; }
        public string ThietBi   { get; set; }
        public string MoTa      { get; set; }

        public DateTime  XayRaLuc { get; set; }
        public DateTime? HetLuc   { get; set; }

        public string    XacNhanBoi { get; set; }
        public string    XacNhanIp  { get; set; }
        public DateTime? XacNhanLuc { get; set; }

        public bool DangMo      { get { return !HetLuc.HasValue; } }
        public bool ChuaXacNhan { get { return !XacNhanLuc.HasValue; } }
    }

    // Vài con số cho header. Kích thước cố định: không có mảng nào ở đây, và đó
    // là điều kiện để header poll được từ 22 trang mà không thành gánh nặng.
    public class TomTatCanhBao
    {
        public int    ChuaXacNhan { get; set; }
        public int    DangMo      { get; set; }
        public string MucCaoNhat  { get; set; }   // null = không có cảnh báo nào đang mở
    }

    // Đọc và ghi cảnh báo thiết bị.
    //
    // ===================== GIỚI HẠN TRẢ VỀ LÀ BẮT BUỘC =====================
    // 112 PLC cộng nút sự cố, chạy vài tháng, không có cơ chế giữ-xoá nào trong
    // toàn dự án. Nếu trả cả bảng thì tới lúc 50.000 dòng, API trả về vài chục MB,
    // trình duyệt dựng 50.000 thẻ <tr>, và mỗi ký tự gõ vào ô tìm kiếm khoá luồng
    // giao diện vài giây — trang cảnh báo chết đúng lúc cần nó nhất.
    //
    // Nên mặc định trả: TẤT CẢ cảnh báo chưa xác nhận (người vận hành phải thấy
    // hết những gì đang chờ xử lý) cộng tối đa GioiHanDaXacNhan dòng gần nhất
    // trong số đã xác nhận. Xem hết lịch sử là việc của một endpoint riêng.
    public class CanhBaoRepository
    {
        public const int GioiHanDaXacNhan = 200;

        private const string CotChung =
            "canh_bao_id, nguon, muc_do, ma_loi, block_no, zone_id, thiet_bi, mo_ta, " +
            "xay_ra_luc, het_luc, xac_nhan_boi, xac_nhan_ip, xac_nhan_luc ";

        public IList<CanhBao> Doc()
        {
            var ra = new List<CanhBao>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // UNION ALL chứ không UNION: hai nhánh loại trừ nhau theo
                // xac_nhan_luc nên không thể trùng dòng, và UNION sẽ tốn một lần
                // khử trùng vô ích trên tập lớn.
                cmd.CommandText =
                    "SELECT * FROM ( " +
                    "  SELECT " + CotChung + "FROM canh_bao WHERE xac_nhan_luc IS NULL " +
                    "  UNION ALL " +
                    "  SELECT * FROM ( " +
                    "    SELECT " + CotChung + "FROM canh_bao WHERE xac_nhan_luc IS NOT NULL " +
                    "    ORDER BY xay_ra_luc DESC LIMIT " + GioiHanDaXacNhan +
                    "  ) AS da_xac_nhan " +
                    ") AS gop ORDER BY xay_ra_luc DESC";

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) ra.Add(Map(r));
                }
            }

            return ra;
        }

        // Ghi một cảnh báo. Trả về id, hoặc 0 khi bị khoá chống trùng chặn lại.
        //
        // Lỗi 1062 (trùng khoá) KHÔNG phải sự cố: nó nghĩa là khối đó đã có một
        // cảnh báo đang mở, tức cơ chế chống trùng đang làm đúng việc. Nuốt nó ở
        // đây để người gọi không phải bọc try/catch — và quan trọng hơn, để một
        // lượt chạy của dịch vụ nền không bị dừng giữa chừng bởi chuyện bình thường.
        public long Ghi(CanhBao cb, string khoaChongTrung = null)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO canh_bao " +
                    "(nguon, muc_do, ma_loi, block_no, zone_id, thiet_bi, mo_ta, " +
                    " xay_ra_luc, khoa_chong_trung) " +
                    "VALUES (@nguon, @mucDo, @maLoi, @blockNo, @zoneId, @thietBi, @moTa, " +
                    "        NOW(3), @khoa)";
                cmd.Parameters.AddWithValue("@nguon",   cb.Nguon);
                cmd.Parameters.AddWithValue("@mucDo",   cb.MucDo);
                cmd.Parameters.AddWithValue("@maLoi",   cb.MaLoi);
                cmd.Parameters.AddWithValue("@blockNo", (object)cb.BlockNo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@zoneId",  (object)cb.ZoneId  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@thietBi", (object)cb.ThietBi ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@moTa",    cb.MoTa);
                cmd.Parameters.AddWithValue("@khoa",    (object)khoaChongTrung ?? DBNull.Value);

                conn.Open();
                try
                {
                    cmd.ExecuteNonQuery();
                    return cmd.LastInsertedId;
                }
                catch (MySqlException ex) when (ex.Number == 1062)
                {
                    return 0;   // đã có cảnh báo đang mở cho khoá này
                }
            }
        }

        // Xác nhận một cảnh báo. Trả về false khi đã có người xác nhận trước.
        //
        // Điều kiện xac_nhan_luc IS NULL nằm trong chính câu UPDATE, không phải
        // kiểm trước rồi ghi sau: kíp trực đổi ca, hai người cùng mở trang và bấm
        // cách nhau vài giây thì kiểm-rồi-ghi sẽ để người sau đè lên người trước,
        // và không còn dấu vết ai thấy cảnh báo đầu tiên.
        public bool XacNhan(long id, string nguoiTuKhai, string ip)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "UPDATE canh_bao " +
                    "SET xac_nhan_boi = @ten, xac_nhan_ip = @ip, xac_nhan_luc = NOW(3) " +
                    "WHERE canh_bao_id = @id AND xac_nhan_luc IS NULL";
                cmd.Parameters.AddWithValue("@ten", nguoiTuKhai);
                cmd.Parameters.AddWithValue("@ip",  (object)ip ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id",  id);

                conn.Open();
                return cmd.ExecuteNonQuery() == 1;
            }
        }

        // Đánh dấu sự cố đã qua. KHÔNG xoá dòng — lịch sử sự cố là thứ cần nhất
        // khi điều tra về sau. Xoá khoá chống trùng để lần sự cố tiếp theo trên
        // cùng khối vẫn sinh được cảnh báo mới.
        public int DongTheoKhoa(string khoaChongTrung)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "UPDATE canh_bao SET het_luc = NOW(3), khoa_chong_trung = NULL " +
                    "WHERE khoa_chong_trung = @khoa";
                cmd.Parameters.AddWithValue("@khoa", khoaChongTrung);
                conn.Open();
                return cmd.ExecuteNonQuery();
            }
        }

        // Vài con số tóm tắt cho header, KHÔNG kéo dòng nào về.
        //
        // Header nằm trong layout dùng chung của 22 trang, nên hàm này chạy nhiều
        // lượt hơn hẳn Doc(). Nếu tóm tắt bằng cách gọi Doc() rồi đếm trong C#,
        // mỗi lượt sẽ kéo toàn bộ cảnh báo chưa xác nhận cộng 200 dòng nữa về bộ
        // nhớ web — đúng cái gánh nặng mà endpoint tóm tắt sinh ra để tránh.
        //
        // MucCaoNhat dùng FIELD(): nó trả 1 cho critical, 4 cho low, nên MIN()
        // cho ra mức NGHIÊM TRỌNG NHẤT trong số đang mở. So chuỗi trực tiếp thì
        // phải xếp hạng bằng tay và dễ sai thứ tự.
        public TomTatCanhBao TomTat()
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT COALESCE(SUM(xac_nhan_luc IS NULL), 0), " +
                    "       COALESCE(SUM(het_luc IS NULL), 0), " +
                    "       MIN(IF(het_luc IS NULL, " +
                    "              FIELD(muc_do,'critical','high','medium','low'), NULL)) " +
                    "FROM   canh_bao";

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return new TomTatCanhBao();
                    return new TomTatCanhBao
                    {
                        ChuaXacNhan = Convert.ToInt32(r[0]),
                        DangMo      = Convert.ToInt32(r[1]),
                        MucCaoNhat  = r[2] == DBNull.Value ? null : TenMuc(Convert.ToInt32(r[2]))
                    };
                }
            }
        }

        private static string TenMuc(int hang)
        {
            switch (hang)
            {
                case 1:  return "critical";
                case 2:  return "high";
                case 3:  return "medium";
                case 4:  return "low";
                default: return null;
            }
        }

        // Các khoá chống trùng đang mở, lọc theo tiền tố.
        //
        // Nhận sẵn connection của người gọi thay vì tự mở: dịch vụ nền gọi hàm này
        // mỗi 30 giây ngay trong một lượt đã có connection, và mở thêm một cái nữa
        // chỉ để chạy một câu SELECT là lãng phí khe kết nối.
        public IList<string> DocKhoaDangMo(MySqlConnection conn, string tienTo)
        {
            var ra = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT khoa_chong_trung FROM canh_bao " +
                    "WHERE het_luc IS NULL AND khoa_chong_trung LIKE @mau";
                cmd.Parameters.AddWithValue("@mau", tienTo + "%");
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        if (r[0] != DBNull.Value) ra.Add(Convert.ToString(r[0]));
                }
            }
            return ra;
        }

        private static CanhBao Map(IDataRecord r)
        {
            return new CanhBao
            {
                Id         = Convert.ToInt64(r["canh_bao_id"]),
                Nguon      = Convert.ToString(r["nguon"]),
                MucDo      = Convert.ToString(r["muc_do"]),
                MaLoi      = Convert.ToString(r["ma_loi"]),
                BlockNo    = SoHoacNull(r, "block_no"),
                ZoneId     = SoHoacNull(r, "zone_id"),
                ThietBi    = ChuoiHoacNull(r, "thiet_bi"),
                MoTa       = Convert.ToString(r["mo_ta"]),
                XayRaLuc   = Convert.ToDateTime(r["xay_ra_luc"]),
                HetLuc     = NgayHoacNull(r, "het_luc"),
                XacNhanBoi = ChuoiHoacNull(r, "xac_nhan_boi"),
                XacNhanIp  = ChuoiHoacNull(r, "xac_nhan_ip"),
                XacNhanLuc = NgayHoacNull(r, "xac_nhan_luc")
            };
        }

        private static string ChuoiHoacNull(IDataRecord r, string cot)
        {
            object v = r[cot];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }

        private static int? SoHoacNull(IDataRecord r, string cot)
        {
            object v = r[cot];
            return v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        private static DateTime? NgayHoacNull(IDataRecord r, string cot)
        {
            object v = r[cot];
            return v == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(v);
        }
    }
}
