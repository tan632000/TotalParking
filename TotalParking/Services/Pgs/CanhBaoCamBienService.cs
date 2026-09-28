using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using TotalParking.Models;
using TotalParking.Services.Plc;

namespace TotalParking.Services.Pgs
{
    // Sinh cảnh báo khi một cảm biến đỗ thường báo lỗi, và đóng khi nó hết lỗi.
    //
    // ===================== VÌ SAO CẦN =====================
    // Ngày 28/09/2026, bốn cảm biến của ZCU 0 lô 1 (vị trí 5..8) báo lỗi liên
    // tục 12 và 22 tiếng mà KHÔNG ai được báo. Phát hiện ra chỉ vì có người
    // tình cờ đi rà. Trước file này, bảng canh_bao chỉ có hai mã: PLC-CONN-01
    // (mất kết nối PLC) và SLOT-DUP-01 (thẻ trùng khối) — không mã nào nói về
    // cảm biến.
    //
    // Hậu quả của việc không biết: v_led_capacity_port đếm ô trống bằng
    // trang_thai = 0. Cảm biến lỗi mang trang_thai = 2 nên ô đó KHÔNG được tính
    // là trống, mà vẫn nằm trong mẫu số. Bảng LED vì thế báo THIẾU chỗ trống.
    // Sai theo chiều thiếu thì an toàn — không dụ tài xế vào chỗ không có — nên
    // sự cố này không tự lộ ra, nó chỉ âm thầm làm bãi trông đầy hơn thực tế.
    // Ngày 28/09 bảng 66 hướng lên có 4 trên 7 cảm biến hỏng: hơn một nửa khu
    // đó vô hình.
    //
    // ===================== VÌ SAO ĐÒI read_at CÒN TƯƠI =====================
    // Khi CCU mất kết nối, trang_thai giữ nguyên giá trị cuối còn read_at đứng
    // lại. Không có vị từ độ tươi thì mọi cảm biến đang ở trạng thái lỗi lúc
    // mất kết nối sẽ tiếp tục sinh cảnh báo, và người vận hành nhận một tràng
    // báo "cảm biến hỏng" cho cái thực ra là một sự cố đường truyền duy nhất.
    //
    // ===================== ÂN HẠN DỰA VÀO changed_at =====================
    // Cùng cách với CanhBaoTheTrungService: changed_at chỉ đổi khi trang_thai
    // thật sự đổi, còn read_at đổi mỗi lượt ghi. Một cảm biến chớp sang lỗi rồi
    // trở lại ngay sẽ không kịp vượt ngưỡng. Cách này không mất trạng thái khi
    // ứng dụng khởi động lại, và hai tiến trình cùng chạy cho cùng kết quả.
    //
    // changed_at NULL nghĩa là chưa đổi lần nào kể từ dòng đầu tiên, tức đã ổn
    // định từ lâu — tính là đủ điều kiện, không loại.
    //
    // ===================== CHỈ CẢM BIẾN CÓ TRONG BẢN ĐỒ =====================
    // pgs_sensor_state giữ cả 320 kênh của 5 ZCU, trong đó 241 kênh không lắp
    // cảm biến. JOIN với pgs_sensor_map lọc về đúng 79 cảm biến thật. Báo động
    // cho kênh trống chỉ làm người vận hành quen bỏ qua bảng cảnh báo.
    //
    // ===================== MỘT CẢNH BÁO MỖI CẢM BIẾN =====================
    // Không gộp theo lô. Đơn vị hành động là một ô đỗ: thợ đi tới đúng ô đó.
    // Khi cả một nhánh hỏng thì nhiều dòng cùng hiện ra, và chính hình dạng đó
    // là manh mối — bốn vị trí liền nhau cùng hỏng đọc ra sự cố đấu dây chung
    // chứ không phải bốn cảm biến tự hỏng độc lập.
    public static class CanhBaoCamBienService
    {
        // Tiền tố của khoá chống trùng. Dạng đầy đủ: CAM_BIEN_LOI:<zcu>.<lo>.<vi_tri>.
        public const string TienToKhoa = "CAM_BIEN_LOI:";

        // Nhịp 60 giây: vòng ghi trạng thái chạy 5 giây một lượt, nên mỗi nhịp ở
        // đây luôn nhìn thấy kết quả của nhiều lượt ghi mới.
        private const int NhipMs = 60000;
        private const string MaLoi = "PGS-SENSOR-01";

        private static readonly object Sync = new object();
        private static Task _loop;
        private static CancellationTokenSource _cts;
        private static readonly CanhBaoRepository Repo = new CanhBaoRepository();

        public static long SoLanSinh { get; private set; }
        public static long SoLanDong { get; private set; }
        public static long SoLanLoi  { get; private set; }
        public static string LoiCuoi { get; private set; }
        public static DateTime? ChayCuoiUtc { get; private set; }

        // Số phút một cảm biến phải giữ nguyên trạng thái lỗi trước khi báo.
        // Mặc định 10: đủ dài để bỏ qua nhiễu nhất thời, đủ ngắn so với 22 tiếng
        // mà sự cố ngày 28/09 đã trôi qua trong im lặng.
        public static int NguongLoiPhut
        {
            get
            {
                int n;
                string raw = ConfigurationManager.AppSettings["pgs:canhBaoCamBienLoiSauPhut"];
                return int.TryParse(raw, out n) && n > 0 ? n : 10;
            }
        }

        public static bool IsRunning { get { return _loop != null; } }

        public static void Start()
        {
            lock (Sync)
            {
                if (_loop != null) return;
                _cts = new CancellationTokenSource();
                _loop = Task.Run(() => VongAsync(_cts.Token));
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                if (_cts != null) { _cts.Cancel(); _cts = null; }
                _loop = null;
            }
        }

        private static async Task VongAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ChayMotLuot();
                }
                catch (Exception ex)
                {
                    // Nuốt ở đây chứ không để ném ra: một lỗi CSDL thoát khỏi vòng
                    // lặp sẽ giết luôn luồng, và từ đó không còn cảnh báo nào được
                    // sinh ra trong khi IsRunning vẫn báo true.
                    SoLanLoi++;
                    LoiCuoi = ex.Message;
                    PlcAuditLog.Error(null, 0, "CANH BAO CAM BIEN",
                                      "Khong chay duoc vong cam bien loi: " + ex.Message);
                }

                try { await Task.Delay(NhipMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        private sealed class CamBienLoi
        {
            public int    Zcu     { get; set; }
            public int    Lo      { get; set; }
            public int    ViTri   { get; set; }
            public string Nhan    { get; set; }
            public int?   ZoneId  { get; set; }
            public string CacBang { get; set; }

            public string Khoa
            {
                get { return Zcu + "." + Lo + "." + ViTri; }
            }
        }

        private static void ChayMotLuot()
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();
                IList<CamBienLoi> loi = DocCamBienLoi(conn, NguongLoiPhut);
                SinhChoCamBienLoi(loi);
                DongChoCamBienHetLoi(conn, loi);
            }

            ChayCuoiUtc = DateTime.UtcNow;
        }

        // trang_thai: 0 trống, 1 có xe, 2 LỖI, 3 không lắp.
        //
        // Cột cac_bang trả về những bảng LED đang quảng bá cảm biến này — đó là
        // thứ biến cảnh báo thành việc làm được: người đọc thấy ngay con số trên
        // bảng nào đang thiếu. Cảm biến chưa gắn cổng nào thì cột này rỗng.
        private const string SqlLoi =
            "SELECT s.zcu_id, s.lo, s.vi_tri, m.nhan_ban_ve, m.zone_id, " +
            "       (SELECT GROUP_CONCAT(DISTINCT p.code " +
            "                            ORDER BY CAST(p.code AS UNSIGNED) SEPARATOR ', ') " +
            "          FROM led_port_sensor ps " +
            "          JOIN led_panel p ON p.panel_id = ps.panel_id " +
            "         WHERE ps.zcu_id = s.zcu_id AND ps.lo = s.lo " +
            "           AND ps.vi_tri = s.vi_tri) AS cac_bang " +
            "FROM   pgs_sensor_state s " +
            "JOIN   pgs_sensor_map m " +
            "       ON m.zcu_id = s.zcu_id AND m.lo = s.lo AND m.vi_tri = s.vi_tri " +
            "WHERE  s.trang_thai = 2 " +
            // Dữ liệu phải còn tươi: CCU rớt thì trang_thai đứng nguyên giá trị
            // cũ, và báo động lúc đó là báo nhầm đường truyền thành cảm biến.
            "  AND  s.read_at >= NOW(3) - INTERVAL 5 MINUTE " +
            "  AND  (s.changed_at IS NULL " +
            "        OR s.changed_at < NOW(3) - INTERVAL @phut MINUTE) " +
            "ORDER  BY s.zcu_id, s.lo, s.vi_tri";

        private static IList<CamBienLoi> DocCamBienLoi(MySqlConnection conn, int nguongPhut)
        {
            var ds = new List<CamBienLoi>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlLoi;
                cmd.Parameters.AddWithValue("@phut", nguongPhut);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new CamBienLoi
                        {
                            Zcu     = Convert.ToInt32(r[0]),
                            Lo      = Convert.ToInt32(r[1]),
                            ViTri   = Convert.ToInt32(r[2]),
                            Nhan    = r[3] == DBNull.Value ? null : Convert.ToString(r[3]),
                            ZoneId  = r[4] == DBNull.Value ? (int?)null : Convert.ToInt32(r[4]),
                            CacBang = r[5] == DBNull.Value ? "" : Convert.ToString(r[5])
                        });
                    }
                }
            }

            return ds;
        }

        private static void SinhChoCamBienLoi(IList<CamBienLoi> loi)
        {
            foreach (CamBienLoi c in loi)
            {
                string ten = string.IsNullOrEmpty(c.Nhan) ? c.Khoa : c.Nhan;
                string anhHuong = string.IsNullOrEmpty(c.CacBang)
                    ? "Cảm biến này chưa gắn vào cổng LED nào."
                    : "Bảng LED bị ảnh hưởng: " + c.CacBang + ".";

                var cb = new CanhBao
                {
                    Nguon = "hardware",
                    // high, không phải critical: ô đó chỉ biến mất khỏi số chỗ
                    // trống chứ không gây nguy hiểm, và sai theo chiều thiếu nên
                    // không xếp xe vào chỗ không có. Cùng hạng với SLOT-DUP-01:
                    // cả hai đều nghĩa là "một con số trên màn hình đang sai".
                    MucDo = "high",
                    MaLoi = MaLoi,
                    // Cảm biến đỗ thường không thuộc khối cơ khí nào.
                    BlockNo = null,
                    ZoneId  = c.ZoneId,
                    ThietBi = "Cảm biến " + ten,
                    MoTa    = "Cảm biến đỗ thường " + ten + " (ZCU " + c.Zcu +
                              ", lô " + c.Lo + ", vị trí " + c.ViTri +
                              ") đang báo lỗi. Ô này không được tính là trống nên " +
                              "số chỗ trống hiển thị đang THIẾU so với thực tế. " +
                              anhHuong
                };

                // Trả 0 nghĩa là cảm biến này đã có cảnh báo đang mở — chuyện
                // bình thường, không phải lỗi.
                if (Repo.Ghi(cb, TienToKhoa + c.Khoa) > 0)
                {
                    SoLanSinh++;
                    PlcAuditLog.Error(null, 0, "CANH BAO CAM BIEN",
                        "Sinh canh bao: cam bien " + ten + " (" + c.Khoa + ") bao loi.");
                }
            }
        }

        // Đóng cảnh báo của những cảm biến đã hết lỗi.
        //
        // Đối chiếu khoá đang mở với danh sách lỗi hiện tại, thay vì gọi
        // DongTheoKhoa cho mọi cảm biến. Bình thường không có cảnh báo nào mở thì
        // vòng này không ghi gì cả.
        //
        // Lưu ý về cảm biến rơi khỏi vị từ độ tươi: khi CCU rớt, cảm biến đang
        // lỗi sẽ biến mất khỏi danh sách và cảnh báo của nó được ĐÓNG. Đó là có
        // chủ ý — lúc đó ta không còn quan sát được nó, mà một cảnh báo để mở
        // dựa trên số đo cũ là khẳng định điều mình không biết. Khi CCU nối lại,
        // nếu cảm biến vẫn lỗi thì cảnh báo được sinh lại ở nhịp kế tiếp.
        private static void DongChoCamBienHetLoi(MySqlConnection conn, IList<CamBienLoi> loi)
        {
            IList<string> dangMo = Repo.DocKhoaDangMo(conn, TienToKhoa);
            if (dangMo.Count == 0) return;

            var conLoi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CamBienLoi c in loi) conLoi.Add(c.Khoa);

            foreach (string khoa in dangMo)
            {
                string dinhDanh = khoa.Substring(TienToKhoa.Length);
                if (dinhDanh.Length == 0) continue;
                if (conLoi.Contains(dinhDanh)) continue;

                if (Repo.DongTheoKhoa(khoa) > 0)
                {
                    SoLanDong++;
                    PlcAuditLog.Recovered(null, 0, "CANH BAO CAM BIEN");
                }
            }
        }
    }
}
