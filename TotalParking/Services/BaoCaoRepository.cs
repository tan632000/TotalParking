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

        // ===================== SUC KHOE VONG DOC O =====================
        // Khac han bai toan mat ket noi o tren. Mot khoi co the KET NOI BINH
        // THUONG ma thanh ghi o cua no khong duoc cap nhat: lenh doc tra ve loi,
        // hoac ladder dang ban. Vong quet bao "110/112 block doc duoc" luc nay va
        // "112/112" luc khac — chap chon, nen phai do chu khong the nhin mot lan
        // roi ket luan.
        //
        // read_at la thoi diem vong quet ghi dong nay. No duoc cap nhat MOI lan
        // quet ke ca khi gia tri khong doi, nen "cu" nghia la khong doc duoc,
        // khong phai "o khong co gi thay doi".
        //
        // Nguong 5 phut: cung nguong ma BlockMapRepository dung cho cot `fresh`.
        // Nhip quet la 45 giay, nen 5 phut la da bo lo khoang sau luot lien tiep.
        private const string SqlSucKhoeO =
            "SELECT b.block_no, b.zone_id, COUNT(*) AS so_o, " +
            "       SUM(s.read_at IS NULL) AS chua_doc, " +
            "       SUM(s.read_at < NOW(3) - INTERVAL 5 MINUTE) AS qua_han, " +
            "       TIMESTAMPDIFF(MINUTE, MIN(s.read_at), NOW(3)) AS cu_nhat_phut " +
            "FROM   plc_slot_state s " +
            "JOIN   block b ON b.block_id = s.block_id " +
            "GROUP  BY b.block_no, b.zone_id " +
            // Chi tra ve khoi CO VAN DE. Liet ke ca 112 khoi binh thuong chi lam
            // nguoi truc phai tu loc bang mat.
            "HAVING chua_doc > 0 OR qua_han > 0 " +
            "ORDER  BY qua_han DESC, cu_nhat_phut DESC";

        private const string SqlTongO =
            "SELECT COUNT(*) AS tong_o, " +
            "       COUNT(DISTINCT block_id) AS tong_khoi, " +
            "       SUM(read_at < NOW(3) - INTERVAL 5 MINUTE) AS o_qua_han, " +
            "       SUM(read_at IS NULL) AS o_chua_doc, " +
            "       MAX(read_at) AS quet_gan_nhat " +
            "FROM   plc_slot_state";

        public SucKhoeVongDoc SucKhoeO()
        {
            var kq = new SucKhoeVongDoc { Khoi = new List<KhoiDocLoi>() };

            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = SqlTongO;
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            kq.TongO      = Convert.ToInt32(r["tong_o"]);
                            kq.TongKhoi   = Convert.ToInt32(r["tong_khoi"]);
                            kq.OQuaHan    = r["o_qua_han"] == DBNull.Value ? 0 : Convert.ToInt32(r["o_qua_han"]);
                            kq.OChuaDoc   = r["o_chua_doc"] == DBNull.Value ? 0 : Convert.ToInt32(r["o_chua_doc"]);
                            kq.QuetGanNhat = r["quet_gan_nhat"] == DBNull.Value
                                                 ? (DateTime?)null : Convert.ToDateTime(r["quet_gan_nhat"]);
                        }
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = SqlSucKhoeO;
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            kq.Khoi.Add(new KhoiDocLoi
                            {
                                BlockNo    = Convert.ToInt32(r["block_no"]),
                                ZoneId     = Int(r, "zone_id"),
                                SoO        = Convert.ToInt32(r["so_o"]),
                                ChuaDoc    = Convert.ToInt32(r["chua_doc"]),
                                QuaHan     = Convert.ToInt32(r["qua_han"]),
                                CuNhatPhut = r["cu_nhat_phut"] == DBNull.Value ? 0 : Convert.ToInt32(r["cu_nhat_phut"])
                            });
                        }
                    }
                }
            }
            return kq;
        }

        // ===================== XU HUONG TRA CUU TIM XE =====================
        // Hai chuoi tren cung mot truc ngay, vi mot minh chuoi dau KHONG doc duoc:
        //
        //   19/09 co 108 luot tra cuu va 0 luot tim thay.
        //
        // Con so do co the la he thong hong (giai ma the sai, vong quet chet),
        // HOAC bai xe hom do trong. Khong the biet neu chi nhin mot chuoi. Nen
        // ghep them so xe duoc dinh tuyen vao bai cung ngay tu vehicle_routing:
        // nhieu xe vao ma khong ai tim thay la dau hieu hong; it xe vao thi ti le
        // thap la binh thuong.
        //
        // He thong KHONG luu lich su suc chua theo thoi gian, nen day la thu gan
        // nhat voi "hom do bai co xe khong" ma du lieu tra loi duoc.
        private const string SqlXuHuong =
            "SELECT ngay, " +
            "       SUM(tra_cuu) AS tra_cuu, SUM(thay) AS thay, SUM(chua_tra_loi) AS chua_tra_loi, " +
            "       SUM(xe_vao) AS xe_vao " +
            "FROM ( " +
            "  SELECT DATE(received_at) AS ngay, COUNT(*) AS tra_cuu, " +
            "         SUM(result_permit = 1) AS thay, " +
            "         SUM(result_permit IS NULL) AS chua_tra_loi, 0 AS xe_vao " +
            "  FROM   plc_request WHERE received_at >= CURDATE() - INTERVAL @so_ngay DAY " +
            "  GROUP  BY DATE(received_at) " +
            "  UNION ALL " +
            "  SELECT DATE(decided_at), 0, 0, 0, SUM(outcome = 'ROUTED') " +
            "  FROM   vehicle_routing WHERE decided_at >= CURDATE() - INTERVAL @so_ngay DAY " +
            "  GROUP  BY DATE(decided_at) " +
            ") t GROUP BY ngay ORDER BY ngay";

        public IList<NgayTraCuu> XuHuongTraCuu(int soNgay)
        {
            if (soNgay <= 0) soNgay = 14;
            if (soNgay > 90) soNgay = 90;

            var ds = new List<NgayTraCuu>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlXuHuong;
                cmd.Parameters.AddWithValue("@so_ngay", soNgay);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new NgayTraCuu
                        {
                            Ngay       = Convert.ToDateTime(r["ngay"]),
                            TraCuu     = Convert.ToInt32(r["tra_cuu"]),
                            Thay       = Convert.ToInt32(r["thay"]),
                            ChuaTraLoi = Convert.ToInt32(r["chua_tra_loi"]),
                            XeVao      = Convert.ToInt32(r["xe_vao"])
                        });
                    }
                }
            }
            return ds;
        }

        // ===================== XE VAO THEO NGAY TRONG TUAN =====================
        // Trang nay truoc do hien "mat do do xe theo ngay trong tuan (%)". He
        // thong KHONG luu lich su suc chua, chi biet suc chua HIEN TAI qua
        // v_zone_capacity, nen khong the tinh duoc ti le lap day cua thu Ba
        // tuan truoc. Cau hoi tra loi duoc bang du lieu that la: moi thu co
        // bao nhieu xe vao bai.
        //
        // Chia cho so ngay thuc te gop vao, khong lay tong: cua so 30 ngay
        // khong chia het cho 7, nen co thu gop 5 lan co thu gop 4. Lay tong
        // thi thu nao lot them mot lan se luon trong nhu ngay dong nhat.
        //
        // Chi dem ROUTED. MANUAL va REJECTED la xe khong vao duoc bai, gop
        // vao se lam con so "xe vao" to hon thuc te.
        private const string SqlXeVaoTheoThu =
            "SELECT DAYOFWEEK(decided_at) AS thu, " +
            "       COUNT(DISTINCT DATE(decided_at)) AS so_ngay, " +
            "       COUNT(*) AS tong_xe " +
            "FROM   vehicle_routing " +
            "WHERE  outcome = 'ROUTED' " +
            "  AND  decided_at >= CURDATE() - INTERVAL @so_ngay DAY " +
            "GROUP  BY thu ORDER BY thu";

        public IList<XeVaoTheoThu> XeVaoTheoNgayTrongTuan(int soNgay)
        {
            if (soNgay <= 0)  soNgay = 30;
            if (soNgay > 365) soNgay = 365;

            var ds = new List<XeVaoTheoThu>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlXeVaoTheoThu;
                cmd.Parameters.AddWithValue("@so_ngay", soNgay);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new XeVaoTheoThu
                        {
                            // MySQL dem CN = 1, T7 = 7
                            Thu    = Convert.ToInt32(r["thu"]),
                            SoNgay = Convert.ToInt32(r["so_ngay"]),
                            TongXe = Convert.ToInt32(r["tong_xe"])
                        });
                    }
                }
            }
            return ds;
        }

        // ===================== PHAN LOAI PHUONG TIEN =====================
        // Nhan phan loai lay NGUYEN VAN tu cot category do camera ghi, khong
        // gom lai thanh SUV / Sedan / qua kho. Bo nhan that trong du lieu la
        // "Tieu chuan/Nhe", "Qua kho", "Tieu chuan/Nang" va vai dong SUV,
        // Sedan con sot tu dot cu; ep chung vao ba o co san la bia lai mot
        // lan nua duoi hinh thuc khac.
        //
        // Dem rieng so xe BI TU CHOI. Camera xep 177 lan vao nhom qua kho
        // nhung chi 24 lan bai that su tu choi — hai con so khac nhau, gop
        // lam mot se lam nguoi doc tuong bai dang tu choi gap bay lan thuc te.
        private const string SqlPhanLoaiXe =
            "SELECT COALESCE(e.category, '(không đọc được)') AS nhan, " +
            "       COUNT(*) AS so_xe, " +
            "       SUM(r.outcome = 'REJECTED') AS so_tu_choi, " +
            "       MIN(e.height_mm) AS cao_min, MAX(e.height_mm) AS cao_max " +
            "FROM   vehicle_routing r " +
            "JOIN   vehicle_event   e ON e.event_id = r.event_id " +
            "WHERE  r.decided_at >= CURDATE() - INTERVAL @so_ngay DAY " +
            "GROUP  BY nhan ORDER BY so_xe DESC";

        public IList<NhomXe> PhanLoaiXe(int soNgay)
        {
            if (soNgay <= 0)  soNgay = 30;
            if (soNgay > 365) soNgay = 365;

            var ds = new List<NhomXe>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlPhanLoaiXe;
                cmd.Parameters.AddWithValue("@so_ngay", soNgay);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new NhomXe
                        {
                            Nhan     = Str(r, "nhan"),
                            SoXe     = Convert.ToInt32(r["so_xe"]),
                            SoTuChoi = r["so_tu_choi"] == DBNull.Value
                                           ? 0 : Convert.ToInt32(r["so_tu_choi"]),
                            CaoMin   = Int(r, "cao_min"),
                            CaoMax   = Int(r, "cao_max")
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

    public class XeVaoTheoThu
    {
        // 1 = Chu Nhat ... 7 = Thu Bay, theo quy uoc cua MySQL DAYOFWEEK
        public int Thu    { get; set; }
        public int SoNgay { get; set; }
        public int TongXe { get; set; }

        public double TrungBinh
        {
            get { return SoNgay == 0 ? 0 : (double)TongXe / SoNgay; }
        }
    }

    public class NhomXe
    {
        public string Nhan     { get; set; }
        public int    SoXe     { get; set; }
        public int    SoTuChoi { get; set; }
        public int?   CaoMin   { get; set; }
        public int?   CaoMax   { get; set; }
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

    public class SucKhoeVongDoc
    {
        public int  TongO    { get; set; }
        public int  TongKhoi { get; set; }
        public int  OQuaHan  { get; set; }
        public int  OChuaDoc { get; set; }
        public DateTime? QuetGanNhat { get; set; }
        public IList<KhoiDocLoi> Khoi { get; set; }
    }

    public class KhoiDocLoi
    {
        public int  BlockNo    { get; set; }
        public int? ZoneId     { get; set; }
        public int  SoO        { get; set; }
        public int  ChuaDoc    { get; set; }
        public int  QuaHan     { get; set; }
        public int  CuNhatPhut { get; set; }
    }

    public class NgayTraCuu
    {
        public DateTime Ngay       { get; set; }
        public int      TraCuu     { get; set; }
        public int      Thay       { get; set; }
        public int      ChuaTraLoi { get; set; }
        public int      XeVao      { get; set; }

        public int KhongThay { get { return TraCuu - Thay - ChuaTraLoi; } }

        // Chia cho 0 ra NaN va giao dien se hien "NaN%" — mot ngay khong ai tra
        // cuu thi ti le khong ton tai, khong phai bang khong.
        public double? TiLeThay
        {
            get { return TraCuu == 0 ? (double?)null : (double)Thay * 100 / TraCuu; }
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
