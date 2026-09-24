using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace TotalParking.Services.Plc
{
    // Tự đưa khối ra khỏi vận hành khi PLC của nó chết lâu, và tự đưa vào lại khi
    // PLC sống lại ổn định.
    //
    // ===================== ĐÂY LÀ MỘT NGOẠI LỆ CÓ CHỦ Ý =====================
    // Nguyên tắc của hệ thống là "cổng vận hành do người quyết": dữ liệu ô đỗ của
    // một khối chưa sẵn sàng sẽ chảy vào sức chứa và vào thuật toán xếp xe, nên
    // việc đưa một khối vào hay ra khỏi vận hành là quyết định của con người.
    //
    // Ngày 24/09 người dùng chọn tự động hoá nó, sau khi đã nghe đánh đổi. Lý do
    // của họ: hạn chế thao tác thủ công khi bàn giao. Quyết định đó được tôn
    // trọng, nhưng vì dịch vụ này TỰ THAY ĐỔI SỨC CHỨA của một bãi đang phục vụ
    // xe thật, nó mang bốn lớp an toàn mà một tính năng thường không cần.
    //
    // ===================== BỐN LỚP AN TOÀN =====================
    //
    // 1. CÔNG TẮC TỔNG. plc:tuDongCongVanHanh mặc định TẮT. Bật nhầm một tính
    //    năng tự đổi sức chứa thì tệ hơn nhiều so với việc quên bật nó.
    //
    // 2. TRỄ BẤT ĐỐI XỨNG. Hạ sau khi mất kết nối liên tục quá haSauPhut (15),
    //    bật sau khi nối lại liên tục quá batSauPhut (5). Block 89 đo được ngày
    //    24/09 mất đúng 3 giây rồi lên lại (12/15 lần ping); ngưỡng tính bằng
    //    phút khiến loại chập chờn đó không bao giờ chạm tới cờ.
    //
    // 3. TRẦN SỐ LẦN ĐỔI MỖI NGÀY. Vượt tranDoiCoMoiNgay (4) thì ngừng tự đổi
    //    khối đó. Một khối dao động là dấu hiệu hỏng phần cứng — để máy bật tắt
    //    sức chứa liên tục thì tài xế thấy số trên bảng LED nhảy loạn, và không
    //    ai biết vì sao.
    //
    // 4. CHỈ ĐỤNG KHỐI CHÍNH MÌNH ĐÃ HẠ. Khối do người khoá tay không có dấu
    //    tu_dong_ha_luc, nên dịch vụ không được phép mở lại. Nếu bỏ lớp này, một
    //    khối đang có thợ làm việc sẽ bị máy mở lại sau vài phút.
    //
    // ===================== LẤY XE RA KHÔNG BỊ ẢNH HƯỞNG =====================
    // Hạ cờ chỉ làm khối thôi nhận xe MỚI. CarLocatorService không lọc
    // block.is_active, và BlockAllocator là chỗ duy nhất trong C# đọc cờ này —
    // nên xe đang nằm trong khối bị hạ vẫn lấy ra bình thường.
    public static class CongVanHanhService
    {
        private const int NhipMs = 30000;

        private static readonly object Sync = new object();
        private static Task _loop;
        private static CancellationTokenSource _cts;

        public static long SoLanHa   { get; private set; }
        public static long SoLanBat  { get; private set; }
        public static long SoLanLoi  { get; private set; }
        public static string LoiCuoi { get; private set; }
        public static DateTime? ChayCuoiUtc { get; private set; }

        public static bool BatTinhNang
        {
            get
            {
                bool b;
                return bool.TryParse(ConfigurationManager.AppSettings["plc:tuDongCongVanHanh"], out b) && b;
            }
        }

        public static int HaSauPhut  { get { return DocInt("plc:haSauPhut", 15); } }
        public static int BatSauPhut { get { return DocInt("plc:batSauPhut", 5); } }
        public static int TranDoiCo  { get { return DocInt("plc:tranDoiCoMoiNgay", 4); } }

        private static int DocInt(string key, int mac)
        {
            int n;
            return int.TryParse(ConfigurationManager.AppSettings[key], out n) && n > 0 ? n : mac;
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
                    // Đọc công tắc mỗi vòng, không đọc một lần lúc khởi động: người
                    // vận hành phải tắt được ngay bằng Web.config khi thấy nó đổi
                    // sai, không phải chờ khởi động lại ứng dụng.
                    if (BatTinhNang) ChayMotLuot();
                }
                catch (Exception ex)
                {
                    SoLanLoi++;
                    LoiCuoi = ex.Message;
                    PlcAuditLog.Error(null, 0, "CONG VAN HANH",
                                      "Khong chay duoc vong tu ha/bat: " + ex.Message);
                }

                try { await Task.Delay(NhipMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static void ChayMotLuot()
        {
            int haSau  = HaSauPhut;
            int batSau = BatSauPhut;
            int tran   = TranDoiCo;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();

                // Bộ đếm đổi theo ngày. Không reset thì sau vài ngày mọi khối đều
                // vượt trần và dịch vụ tự tê liệt mà không ai hiểu vì sao.
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "UPDATE block SET so_lan_doi_hom_nay = 0, ngay_dem = CURDATE() " +
                        "WHERE ngay_dem IS NULL OR ngay_dem <> CURDATE()";
                    cmd.ExecuteNonQuery();
                }

                foreach (var b in CanHa(conn, haSau, tran))
                {
                    DoiCo(conn, b.Key, false);
                    SoLanHa++;
                    PlcAuditLog.Error(null, b.Key, "CONG VAN HANH",
                        "Tu ha khoi khoi van hanh: PLC mat ket noi qua " + haSau + " phut.");
                }

                foreach (var b in CanBat(conn, batSau, tran))
                {
                    DoiCo(conn, b.Key, true);
                    SoLanBat++;
                    PlcAuditLog.Recovered(null, b.Key, "CONG VAN HANH");
                }
            }

            ChayCuoiUtc = DateTime.UtcNow;
        }

        // Khối cần hạ: PLC đang báo chết, đã chết đủ lâu, khối vẫn đang vận hành,
        // và chưa vượt trần đổi trong ngày.
        private static IList<KeyValuePair<int, int>> CanHa(MySqlConnection conn, int haSau, int tran)
        {
            const string sql =
                "SELECT b.block_no, b.block_id " +
                "FROM   block b JOIN plc_device d ON d.block_id = b.block_id " +
                "WHERE  b.is_active = 1 " +
                "  AND  d.is_connected = 0 " +
                "  AND  d.connected_changed_at IS NOT NULL " +
                "  AND  d.connected_changed_at < NOW(3) - INTERVAL @phut MINUTE " +
                "  AND  b.so_lan_doi_hom_nay < @tran";
            return DocDanhSach(conn, sql, haSau, tran);
        }

        // Khối cần bật lại: PLC đã nối lại đủ lâu, khối đang tắt, VÀ dấu
        // tu_dong_ha_luc cho biết chính dịch vụ này đã hạ nó.
        private static IList<KeyValuePair<int, int>> CanBat(MySqlConnection conn, int batSau, int tran)
        {
            const string sql =
                "SELECT b.block_no, b.block_id " +
                "FROM   block b JOIN plc_device d ON d.block_id = b.block_id " +
                "WHERE  b.is_active = 0 " +
                "  AND  b.tu_dong_ha_luc IS NOT NULL " +
                "  AND  d.is_connected = 1 " +
                "  AND  d.connected_changed_at IS NOT NULL " +
                "  AND  d.connected_changed_at < NOW(3) - INTERVAL @phut MINUTE " +
                "  AND  b.so_lan_doi_hom_nay < @tran";
            return DocDanhSach(conn, sql, batSau, tran);
        }

        private static IList<KeyValuePair<int, int>> DocDanhSach(
            MySqlConnection conn, string sql, int phut, int tran)
        {
            var ra = new List<KeyValuePair<int, int>>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@phut", phut);
                cmd.Parameters.AddWithValue("@tran", tran);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        ra.Add(new KeyValuePair<int, int>(
                            Convert.ToInt32(r[0]), Convert.ToInt32(r[1])));
                }
            }
            return ra;
        }

        private static void DoiCo(MySqlConnection conn, int blockNo, bool bat)
        {
            using (var cmd = conn.CreateCommand())
            {
                // tu_dong_ha_luc đặt khi hạ, xoá khi bật: nó là dấu vân tay cho
                // biết khối này do máy hạ chứ không phải người khoá tay.
                cmd.CommandText =
                    "UPDATE block SET is_active = @bat, " +
                    "       tu_dong_ha_luc = " + (bat ? "NULL" : "NOW(3)") + ", " +
                    "       so_lan_doi_hom_nay = so_lan_doi_hom_nay + 1, " +
                    "       ngay_dem = CURDATE() " +
                    "WHERE block_no = @bn";
                cmd.Parameters.AddWithValue("@bat", bat ? 1 : 0);
                cmd.Parameters.AddWithValue("@bn", blockNo);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
