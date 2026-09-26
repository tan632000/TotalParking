using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;

namespace TotalParking.Services
{
    // Số liệu cho trang Báo cáo & thống kê.
    //
    // ===================== CHỈ HAI BẢNG, KHÔNG PHẢI BỐN =====================
    // Trang Báo cáo có bốn bảng, cả bốn từng viết cứng trong HTML. Hai bảng ở
    // đây là hai bảng DUY NHẤT có nguồn dữ liệu thật:
    //
    //   Lịch sử quẹt thẻ  -> plc_request (572 lượt)
    //   Nguyên nhân lỗi   -> canh_bao nhóm theo ma_loi
    //
    // Hai bảng còn lại — lịch sử bảo trì và doanh thu — KHÔNG có nguồn. Cơ sở
    // dữ liệu không có bảng bảo trì nào, và không có cột nào mang nghĩa tiền:
    // đã tra information_schema cho mọi tên chứa fee/phi/amount/price và chỉ ra
    // đúng cột `plate`. Chúng được để trạng thái rỗng trong giao diện thay vì
    // nối bừa vào một nguồn gần đúng.
    public class BaoCaoRepository
    {
        // ===================== VÌ SAO KHÔNG PHẢI "VÀO / RA" =====================
        // Bảng cũ có cột "Loại hành động" với hai giá trị Gửi xe vào / Lấy xe ra.
        // plc_request KHÔNG phân biệt được hai chiều đó — nó ghi lại mỗi lượt
        // ladder hỏi "thẻ này có được phép không", và câu trả lời là cho vào hay
        // từ chối kèm lý do. Nên cột đó đổi thành KẾT QUẢ, đúng thứ dữ liệu biết.
        //
        // LEFT JOIN parking_card chứ không JOIN: một lượt quẹt thẻ lạ vẫn là một
        // sự kiện có thật và người trực cần thấy nó — đó chính là lúc hay có
        // chuyện. 19/572 lượt không đọc được mã thẻ, 53 lượt mã không có trong
        // danh mục; giấu chúng đi là giấu đúng phần đáng xem.
        private const string SqlLichSu =
            "SELECT r.received_at, b.block_no, b.zone_id, " +
            "       r.card_code, c.card_no, c.plate, c.vehicle_name, " +
            "       r.result_permit, r.reject_reason " +
            "FROM   plc_request r " +
            "JOIN   block b ON b.block_id = r.block_id " +
            "LEFT   JOIN parking_card c ON c.card_code = r.card_code " +
            "ORDER  BY r.received_at DESC " +
            "LIMIT  @gioi_han";

        public IList<LuotQuetThe> LichSuQuetThe(int gioiHan)
        {
            if (gioiHan <= 0) gioiHan = 50;
            if (gioiHan > 500) gioiHan = 500;

            var ds = new List<LuotQuetThe>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlLichSu;
                cmd.Parameters.AddWithValue("@gioi_han", gioiHan);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new LuotQuetThe
                        {
                            Luc         = Convert.ToDateTime(r["received_at"]),
                            BlockNo     = Convert.ToInt32(r["block_no"]),
                            ZoneId      = Int(r, "zone_id"),
                            MaThe       = Str(r, "card_code"),
                            SoThe       = Str(r, "card_no"),
                            BienSo      = Str(r, "plate"),
                            TenXe       = Str(r, "vehicle_name"),
                            ChoVao      = Bool(r, "result_permit"),
                            LyDoTuChoi  = Str(r, "reject_reason")
                        });
                    }
                }
            }
            return ds;
        }

        // Pareto nguyên nhân lỗi. Gom theo ma_loi vì đó là thứ lặp lại được;
        // mo_ta có kèm số khối nên mỗi dòng một khác và không gom được.
        private const string SqlLoi =
            "SELECT ma_loi, muc_do, COUNT(*) AS so_lan, MAX(xay_ra_luc) AS gan_nhat " +
            "FROM   canh_bao " +
            "GROUP  BY ma_loi, muc_do " +
            "ORDER  BY so_lan DESC, gan_nhat DESC";

        public IList<NguyenNhanLoi> NguyenNhanLoi()
        {
            var ds = new List<NguyenNhanLoi>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlLoi;
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new NguyenNhanLoi
                        {
                            MaLoi   = Str(r, "ma_loi"),
                            MucDo   = Str(r, "muc_do"),
                            SoLan   = Convert.ToInt32(r["so_lan"]),
                            GanNhat = Convert.ToDateTime(r["gan_nhat"])
                        });
                    }
                }
            }
            return ds;
        }

        private static string Str(IDataRecord r, string cot)
        {
            object v = r[cot];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }

        private static int? Int(IDataRecord r, string cot)
        {
            object v = r[cot];
            return v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        // NULL khác false: result_permit NULL nghĩa là lượt quẹt chưa được trả
        // lời, không phải bị từ chối. Gộp hai cái lại là báo sai cho người trực.
        private static bool? Bool(IDataRecord r, string cot)
        {
            object v = r[cot];
            return v == DBNull.Value ? (bool?)null : Convert.ToInt32(v) == 1;
        }
    }

    public class LuotQuetThe
    {
        public DateTime Luc        { get; set; }
        public int      BlockNo    { get; set; }
        public int?     ZoneId     { get; set; }
        public string   MaThe      { get; set; }
        public string   SoThe      { get; set; }
        public string   BienSo     { get; set; }
        public string   TenXe      { get; set; }
        public bool?    ChoVao     { get; set; }
        public string   LyDoTuChoi { get; set; }
    }

    public class NguyenNhanLoi
    {
        public string   MaLoi   { get; set; }
        public string   MucDo   { get; set; }
        public int      SoLan   { get; set; }
        public DateTime GanNhat { get; set; }
    }
}
