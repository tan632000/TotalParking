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

        // ===================== DO TIN CAY TUNG KHOI PLC =====================
        // Tra loi cau hoi bao tri hoi hang ngay: KHOI NAO CAN DI XEM TRUOC?
        //
        // Nguon la canh_bao voi ma_loi PLC-CONN-01, do CanhBaoPlcService sinh ra
        // khi mot khoi mat ket noi qua nguong. Moi dong mang thoi diem xay ra va
        // thoi diem dong, nen tinh duoc ba thu khac nhau ve ban chat:
        //
        //   so_lan       — mat BAO NHIEU LAN  -> chap chon
        //   tong_phut    — mat TONG BAO LAU   -> muc do anh huong
        //   lan_lau_nhat — lan mat DAI NHAT   -> chet han hay chi thoang qua
        //
        // Hai khoi cung "mat ket noi 2 lan" co the can hai viec sua khac han nhau:
        // mot cai chap chon vai giay (nghi cap hoac switch), mot cai chet 38 tieng
        // (nghi nguon hoac thiet bi). Gop chung thanh mot con so la vut di dung
        // phan giup ky thuat vien chon mang theo do nghe gi.
        //
        // COALESCE(het_luc, NOW()): su co DANG MO van dang tinh gio, nen lay moc
        // la bay gio. Bo qua chung se lam khoi dang hong nhat trong nhu it van de
        // nhat.
        private const string SqlDoTinCay =
            "SELECT c.block_no, MIN(c.zone_id) AS zone_id, MIN(d.ip_address) AS ip_address, " +
            "       COUNT(*) AS so_lan, " +
            "       SUM(TIMESTAMPDIFF(MINUTE, c.xay_ra_luc, COALESCE(c.het_luc, NOW(3)))) AS tong_phut, " +
            "       MAX(TIMESTAMPDIFF(MINUTE, c.xay_ra_luc, COALESCE(c.het_luc, NOW(3)))) AS lan_lau_nhat, " +
            "       SUM(c.het_luc IS NULL) AS dang_mo, " +
            "       MAX(c.xay_ra_luc) AS gan_nhat " +
            "FROM   canh_bao c " +
            "LEFT   JOIN block b ON b.block_no = c.block_no " +
            "LEFT   JOIN plc_device d ON d.block_id = b.block_id " +
            "WHERE  c.ma_loi = 'PLC-CONN-01' " +
            "  AND  c.block_no IS NOT NULL " +
            "  AND  c.xay_ra_luc >= NOW(3) - INTERVAL @so_ngay DAY " +
            "GROUP  BY c.block_no " +
            // Dang mat ket noi len dau bat ke so lan: do la viec phai lam NGAY.
            "ORDER  BY dang_mo DESC, so_lan DESC, tong_phut DESC";

        public IList<DoTinCayKhoi> DoTinCay(int soNgay)
        {
            if (soNgay <= 0) soNgay = 30;
            if (soNgay > 365) soNgay = 365;

            var ds = new List<DoTinCayKhoi>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlDoTinCay;
                cmd.Parameters.AddWithValue("@so_ngay", soNgay);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new DoTinCayKhoi
                        {
                            BlockNo    = Convert.ToInt32(r["block_no"]),
                            ZoneId     = Int(r, "zone_id"),
                            Ip         = Str(r, "ip_address"),
                            SoLan      = Convert.ToInt32(r["so_lan"]),
                            TongPhut   = Convert.ToInt32(r["tong_phut"]),
                            LanLauNhat = Convert.ToInt32(r["lan_lau_nhat"]),
                            DangMo     = Convert.ToInt32(r["dang_mo"]) > 0,
                            GanNhat    = Convert.ToDateTime(r["gan_nhat"])
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

    public class DoTinCayKhoi
    {
        public int      BlockNo    { get; set; }
        public int?     ZoneId     { get; set; }
        public string   Ip         { get; set; }
        public int      SoLan      { get; set; }
        public int      TongPhut   { get; set; }
        public int      LanLauNhat { get; set; }
        public bool     DangMo     { get; set; }
        public DateTime GanNhat    { get; set; }

        // ===================== PHAN LOAI DE BIET MANG GI THEO =====================
        // Khong phai mot thang diem — mot goi y ve DANG hong, vi hai khoi cung so
        // lan mat co the can hai viec sua khac han nhau.
        public string Dang
        {
            get
            {
                if (DangMo) return "dang_mat";
                if (SoLan >= 2) return "chap_chon";
                if (LanLauNhat >= 60) return "chet_lau";
                return "thoang_qua";
            }
        }

        public string GoiY
        {
            get
            {
                switch (Dang)
                {
                    case "dang_mat":
                        return "Đang mất kết nối ngay lúc này — kiểm tra trước tiên.";
                    case "chap_chon":
                        return "Mất nhiều lần rồi tự nối lại: nghi đầu nối, dây mạng hoặc cổng switch.";
                    case "chet_lau":
                        return "Mất liền một mạch rồi mới nối lại: nghi mất nguồn hoặc thiết bị treo.";
                    default:
                        return "Mất thoáng qua, có thể là nhiễu. Theo dõi thêm.";
                }
            }
        }
    }

    public class NguyenNhanLoi
    {
        public string   MaLoi   { get; set; }
        public string   MucDo   { get; set; }
        public int      SoLan   { get; set; }
        public DateTime GanNhat { get; set; }
    }
}
