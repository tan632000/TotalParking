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
    //   Lịch sử tìm xe    -> plc_request (572 lượt quẹt thẻ tìm xe)
    //   Nguyên nhân lỗi   -> canh_bao nhóm theo ma_loi
    //
    // Hai bảng còn lại — lịch sử bảo trì và doanh thu — KHÔNG có nguồn. Cơ sở
    // dữ liệu không có bảng bảo trì nào, và không có cột nào mang nghĩa tiền:
    // đã tra information_schema cho mọi tên chứa fee/phi/amount/price và chỉ ra
    // đúng cột `plate`. Chúng được để trạng thái rỗng trong giao diện thay vì
    // nối bừa vào một nguồn gần đúng.
    public class BaoCaoRepository
    {
        // ===================== ĐÂY LÀ NHẬT KÝ TÌM XE =====================
        // Bảng plc_request từng là nhật ký xin phép cho xe vào, nhưng ý nghĩa hai
        // cột đã bị đổi mục đích khi chuyển sang hợp đồng mới, và chính
        // PlcConnection.SafeLogFind ghi lại điều đó:
        //
        //     result_permit = TÌM THẤY XE hay không   (không còn là "cho vào")
        //     result_class  = SỐ BLOCK trả về          (không còn là 2200/2600)
        //     reject_reason = INVALID_CARD khi KHÔNG TÌM THẤY
        //
        // Dữ liệu xác nhận: result_class mang giá trị 103, 95, 64, 96, 72 — đó là
        // số khối, không phải hạng tải.
        //
        // Bản đầu của bảng này gán nhãn "Cho vào / Từ chối", khiến người trực
        // tưởng hệ thống đang chặn thẻ khách. Thực tế 445 lượt INVALID_CARD chỉ
        // là 445 lần quẹt thẻ tìm xe mà xe không có trong bãi — câu trả lời đúng,
        // vì bãi đang gần như trống.
        //
        // LEFT JOIN parking_card chứ không JOIN: một lượt quẹt bằng thẻ lạ vẫn là
        // một sự kiện có thật và người trực cần thấy nó.
        private const string SqlLichSu =
            "SELECT r.received_at, b.block_no, b.zone_id, " +
            "       r.card_code, c.card_no, c.plate, c.vehicle_name, " +
            "       r.result_permit, r.result_class, r.reject_reason " +
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
                            TimThay     = Bool(r, "result_permit"),
                            BlockTraVe  = Int(r, "result_class"),
                            LyDo        = Str(r, "reject_reason")
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
        // null = lượt quẹt chưa được trả lời. Khác hẳn "không tìm thấy".
        public bool?    TimThay    { get; set; }
        // Số khối trả về cho HMI. 0 khi không tìm thấy.
        public int?     BlockTraVe { get; set; }
        public string   LyDo       { get; set; }
    }

    public class NguyenNhanLoi
    {
        public string   MaLoi   { get; set; }
        public string   MucDo   { get; set; }
        public int      SoLan   { get; set; }
        public DateTime GanNhat { get; set; }
    }
}
