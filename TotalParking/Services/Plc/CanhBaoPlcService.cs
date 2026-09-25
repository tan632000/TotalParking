using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace TotalParking.Services.Plc
{
    // Sinh cảnh báo khi PLC mất kết nối quá lâu, và đóng cảnh báo khi nó nối lại.
    //
    // ===================== VÌ SAO CẦN CHỐNG TRÙNG =====================
    // Vòng này chạy 30 giây một lượt. Một PLC chết ba ngày mà mỗi lượt sinh một
    // dòng thì bảng có 8.640 dòng cho MỘT sự cố, và trang Alarms thành vô dụng.
    //
    // Cách chặn nằm ở CSDL chứ không ở đây: cột khoa_chong_trung có ràng buộc
    // UNIQUE, chỉ mang giá trị khi cảnh báo đang mở. Lần ghi thứ hai cho cùng một
    // khối bị chính CSDL từ chối (lỗi 1062), và CanhBaoRepository.Ghi nuốt lỗi đó
    // rồi trả 0. Dùng ràng buộc thay vì kiểm-rồi-ghi trong mã vì hai tiến trình
    // cùng chạy thì kiểm-rồi-ghi vẫn lọt.
    //
    // ===================== NULL KHÁC 0 =====================
    // PlcDeviceRepository đặt is_connected = NULL khi thiết bị rời vòng poll:
    // NULL nghĩa là "không quan sát được", khác hẳn 0 nghĩa là "biết chắc đang
    // chết". Chỉ 0 mới sinh cảnh báo. Nếu coi is_connected <> 1 là mất kết nối
    // thì mỗi lần gỡ thiết bị khỏi vòng poll sẽ đẻ ra một cảnh báo giả.
    //
    // ===================== HAI LỚP CHỐNG RUNG =====================
    // PlcTrangThaiWriter đã đòi 3 lần quan sát liên tiếp cùng trạng thái ở nhịp
    // 5 giây trước khi ghi is_connected — một sự cố vài giây không bao giờ tới
    // được cột đó. Ngưỡng 5 phút ở đây là lớp thứ hai: ngắn hơn thì bảng đầy
    // cảnh báo tự tắt, và người vận hành bắt đầu bỏ qua chúng.
    public static class CanhBaoPlcService
    {
        // Tiền tố của khoá chống trùng. Dạng đầy đủ: PLC_MAT_KET_NOI:<block_no>.
        public const string TienToKhoa = "PLC_MAT_KET_NOI:";

        private const int NhipMs = 30000;
        private const string MaLoi = "PLC-CONN-01";

        private static readonly object Sync = new object();
        private static Task _loop;
        private static CancellationTokenSource _cts;
        private static readonly CanhBaoRepository Repo = new CanhBaoRepository();

        public static long SoLanSinh { get; private set; }
        public static long SoLanDong { get; private set; }
        public static long SoLanLoi  { get; private set; }
        public static string LoiCuoi { get; private set; }
        public static DateTime? ChayCuoiUtc { get; private set; }

        public static int NguongPhut
        {
            get
            {
                int n;
                string raw = ConfigurationManager.AppSettings["plc:canhBaoMatKetNoiSauPhut"];
                return int.TryParse(raw, out n) && n > 0 ? n : 5;
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
                    PlcAuditLog.Error(null, 0, "CANH BAO PLC",
                                      "Khong chay duoc vong sinh canh bao: " + ex.Message);
                }

                try { await Task.Delay(NhipMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static void ChayMotLuot()
        {
            int nguong = NguongPhut;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();
                SinhChoKhoiDangChet(conn, nguong);
                DongChoKhoiDaNoiLai(conn);
            }

            ChayCuoiUtc = DateTime.UtcNow;
        }

        // plc_device KHÔNG có cột block_no — khoá ngoài là block_id, và hai giá trị
        // đó khác nhau. Phải JOIN sang block để lấy số khối mà người vận hành đọc.
        private static void SinhChoKhoiDangChet(MySqlConnection conn, int nguong)
        {
            const string sql =
                "SELECT b.block_no, b.zone_id, d.ip_address, " +
                "       TIMESTAMPDIFF(MINUTE, d.connected_changed_at, NOW(3)) AS so_phut " +
                "FROM   plc_device d JOIN block b ON b.block_id = d.block_id " +
                "WHERE  d.is_connected = 0 " +
                "  AND  d.connected_changed_at IS NOT NULL " +
                "  AND  d.connected_changed_at < NOW(3) - INTERVAL @phut MINUTE";

            var khoi = new List<int[]>();          // [block_no, zone_id, so_phut]
            var ip = new List<string>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@phut", nguong);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        khoi.Add(new[]
                        {
                            Convert.ToInt32(r[0]),
                            r[1] == DBNull.Value ? 0 : Convert.ToInt32(r[1]),
                            r[3] == DBNull.Value ? nguong : Convert.ToInt32(r[3])
                        });
                        ip.Add(r[2] == DBNull.Value ? null : Convert.ToString(r[2]));
                    }
                }
            }

            for (int i = 0; i < khoi.Count; i++)
            {
                int blockNo = khoi[i][0];
                int zoneId  = khoi[i][1];
                int soPhut  = khoi[i][2];

                var cb = new CanhBao
                {
                    Nguon   = "hardware",
                    MucDo   = "critical",
                    MaLoi   = MaLoi,
                    BlockNo = blockNo,
                    ZoneId  = zoneId > 0 ? (int?)zoneId : null,
                    ThietBi = "PLC " + blockNo.ToString(CultureInfo.InvariantCulture),
                    MoTa    = "PLC khối " + blockNo + (ip[i] == null ? "" : " (" + ip[i] + ")") +
                              " mất kết nối " + soPhut + " phút."
                };

                // Trả 0 nghĩa là khối này đã có cảnh báo đang mở — chuyện bình
                // thường, không phải lỗi.
                if (Repo.Ghi(cb, TienToKhoa + blockNo) > 0)
                {
                    SoLanSinh++;
                    PlcAuditLog.Error(ip[i], blockNo, "CANH BAO PLC",
                        "Sinh canh bao: PLC mat ket noi qua " + nguong + " phut.");
                }
            }
        }

        // Đóng cảnh báo của những khối đã nối lại.
        //
        // Đọc danh sách khoá đang mở rồi đối chiếu với khối đang kết nối, thay vì
        // gọi DongTheoKhoa cho cả 112 khối mỗi 30 giây. Bình thường không có cảnh
        // báo nào mở thì vòng này không ghi gì cả.
        private static void DongChoKhoiDaNoiLai(MySqlConnection conn)
        {
            IList<string> dangMo = Repo.DocKhoaDangMo(conn, TienToKhoa);
            if (dangMo.Count == 0) return;

            var daNoiLai = new HashSet<int>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT b.block_no FROM plc_device d " +
                    "JOIN   block b ON b.block_id = d.block_id " +
                    "WHERE  d.is_connected = 1";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) daNoiLai.Add(Convert.ToInt32(r[0]));
                }
            }

            foreach (string khoa in dangMo)
            {
                int blockNo;
                string phan = khoa.Substring(TienToKhoa.Length);
                if (!int.TryParse(phan, NumberStyles.Integer, CultureInfo.InvariantCulture, out blockNo))
                    continue;
                if (!daNoiLai.Contains(blockNo)) continue;

                if (Repo.DongTheoKhoa(khoa) > 0)
                {
                    SoLanDong++;
                    PlcAuditLog.Recovered(null, blockNo, "CANH BAO PLC");
                }
            }
        }
    }
}
