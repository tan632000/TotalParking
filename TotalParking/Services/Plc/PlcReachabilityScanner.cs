using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Định kỳ dò xem PLC nào đang sống, kể cả những cái KHÔNG nằm trong vòng poll.
    //
    // ===================== VÌ SAO CẦN =====================
    // Vòng poll chỉ đọc thiết bị is_active = 1. Một PLC được cấp nguồn và đưa lên
    // mạng sẽ không được ai để ý cho tới khi có người sửa DB rồi khởi động lại app.
    // Thực tế đã xảy ra: 29 PLC đang chạy mà SCADA mù hoàn toàn, và chỉ phát hiện
    // ra khi phải quét mạng bằng tay để truy một sự cố.
    //
    // ===================== DÒ BẰNG FINS/UDP =====================
    // Đọc một word D0 qua FINS/UDP (OmronFinsUdpClient.ProbeAsync) — chỉ đọc,
    // không có phiên nên không chiếm khe nào của PLC. Không còn gói TCP nào tới
    // cổng 9600: từ 07/10 toàn hệ thống chỉ nói chuyện với PLC qua UDP.
    //
    // "Sống" ở đây nghĩa là PLC trả lời FINS thật, chặt hơn cách cũ (chỉ thấy cổng
    // TCP mở) — nhưng vẫn không có nghĩa khối đỗ đã sẵn sàng vận hành.
    //
    // ===================== CHỈ BÁO CÁO, KHÔNG TỰ BẬT =====================
    // Lớp này KHÔNG đổi is_active. PLC trả lời FINS không có nghĩa khối đỗ đã nghiệm
    // thu xong; dữ liệu ô đỗ còn sót của nó sẽ chảy thẳng vào sức chứa zone, số
    // chỗ trống trên bảng LED và kết quả BlockAllocator. Máy quan sát, người quyết.
    public static class PlcReachabilityScanner
    {
        private const int MaxConcurrent = 16;   // giống hàng rào của vòng poll

        private static readonly object Sync = new object();
        private static Dictionary<int, Row> _ketQua = new Dictionary<int, Row>();
        private static DateTime? _quetLuc;
        private static Task _loop;
        private static CancellationTokenSource _cts;

        public class Row
        {
            public int      BlockNo  { get; set; }
            public string   Endpoint { get; set; }
            public bool     InPoll   { get; set; }   // is_active = 1
            public bool     Alive    { get; set; }   // PLC trả lời FINS/UDP
            public DateTime? LastSeen { get; set; }
        }

        public static bool Enabled
        {
            get
            {
                bool b;
                string v = ConfigurationManager.AppSettings["plc:discoveryEnabled"];
                return !bool.TryParse(v, out b) || b;   // mặc định BẬT
            }
        }

        private static int IntervalMs
        {
            get
            {
                int n;
                string v = ConfigurationManager.AppSettings["plc:discoveryMs"];
                if (!int.TryParse(v, out n) || n < 30000) n = 300000;   // 5 phút
                return n;
            }
        }

        private static int TimeoutMs
        {
            get
            {
                int n;
                string v = ConfigurationManager.AppSettings["plc:discoveryTimeoutMs"];
                if (!int.TryParse(v, out n) || n < 200 || n > 10000) n = 2000;
                return n;
            }
        }

        public static void Start()
        {
            if (!Enabled) return;
            lock (Sync)
            {
                if (_loop != null) return;
                _cts  = new CancellationTokenSource();
                _loop = Task.Run(() => LoopAsync(_cts.Token));
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                if (_cts != null) _cts.Cancel();
                _loop = null;
            }
        }

        // Ảnh chụp kết quả lượt quét gần nhất. Trả bản sao để phía gọi không giữ
        // tham chiếu vào tập đang được lượt quét sau thay thế.
        public static IList<Row> Snapshot()
        {
            lock (Sync) { return _ketQua.Values.OrderBy(r => r.BlockNo).ToList(); }
        }

        public static DateTime? LastScan { get { lock (Sync) { return _quetLuc; } } }

        // PLC sống nhưng không nằm trong vòng poll — đây là thứ cần người xử lý.
        public static IList<Row> AliveButNotPolled()
        {
            return Snapshot().Where(r => r.Alive && !r.InPoll).ToList();
        }

        private static async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await ScanOnceAsync(token).ConfigureAwait(false); }
                catch (Exception) { /* không được để hỏng vòng */ }

                try { await Task.Delay(IntervalMs, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        public static async Task ScanOnceAsync(CancellationToken token)
        {
            // activeOnly: false — đây chính là điểm khác vòng poll.
            var devices = new PlcDeviceRepository().GetAll(false);
            var gate = new SemaphoreSlim(MaxConcurrent);
            var moi  = new Dictionary<int, Row>();

            // Giữ LastSeen cũ: một PLC vừa tắt vẫn cần biết lần cuối thấy nó là khi nào.
            Dictionary<int, Row> cu;
            lock (Sync) { cu = _ketQua; }

            var tasks = devices.Select(async d =>
            {
                await gate.WaitAsync(token).ConfigureAwait(false);
                bool song;
                try { song = await ProbeAsync(d.IpAddress, d.Port).ConfigureAwait(false); }
                finally { gate.Release(); }

                Row truoc;
                DateTime? thayLuc = cu.TryGetValue(d.BlockNo, out truoc) ? truoc.LastSeen : null;
                if (song) thayLuc = DateTime.Now;

                lock (moi)
                {
                    moi[d.BlockNo] = new Row
                    {
                        BlockNo  = d.BlockNo,
                        Endpoint = d.Endpoint,
                        InPoll   = d.IsActive,
                        Alive    = song,
                        LastSeen = thayLuc
                    };
                }
            }).ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
            gate.Dispose();

            lock (Sync) { _ketQua = moi; _quetLuc = DateTime.Now; }
        }

        // Đọc một word D0 qua FINS/UDP rồi đóng. Không có gói TCP nào.
        private static Task<bool> ProbeAsync(string ip, int port)
        {
            return OmronFinsUdpClient.ProbeAsync(ip, port, TimeoutMs);
        }
    }
}
