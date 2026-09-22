using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
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
    // ===================== CHỈ DÒ TCP, KHÔNG BẮT TAY FINS =====================
    // Mở TCP rồi đóng ngay, không gửi khung FINS nào.
    //
    // PLC Omron chỉ cấp được một số ít node FINS/TCP cùng lúc; hết thì trả lỗi
    // 0x00000020 và mọi kết nối mới đều hỏng. Bắt tay FINS chỉ để hỏi "còn sống
    // không" là tiêu một khe mà vòng poll đang cần. Dò TCP trả lời đủ câu hỏi này
    // mà không đụng vào hạn mức đó.
    //
    // Đổi lại: TCP mở không chứng minh ladder đang chạy. Đây là chỉ báo "có mặt
    // trên mạng", không phải "sẵn sàng vận hành" — và đó đúng là điều cần báo.
    //
    // ===================== CHỈ BÁO CÁO, KHÔNG TỰ BẬT =====================
    // Lớp này KHÔNG đổi is_active. Cổng 9600 mở không có nghĩa khối đỗ đã nghiệm
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
            public bool     Alive    { get; set; }   // TCP 9600 mở
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

        // Mở TCP rồi đóng. Không gửi byte nào.
        private static async Task<bool> ProbeAsync(string ip, int port)
        {
            var client = new TcpClient();
            try
            {
                var noi = client.ConnectAsync(ip, port);
                var xong = await Task.WhenAny(noi, Task.Delay(TimeoutMs)).ConfigureAwait(false);
                if (xong != noi) return false;
                await noi.ConfigureAwait(false);   // để lỗi nổi lên nếu có
                return client.Connected;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                try { client.Close(); } catch (Exception) { }
            }
        }
    }
}
