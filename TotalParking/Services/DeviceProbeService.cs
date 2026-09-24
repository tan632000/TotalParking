using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Thăm dò trạng thái sống của thiết bị ngoại vi: PLC, bảng LED, PGS, camera AI.
    //
    // TỰ THĂM DÒ chứ không đọc trạng thái từ vòng poll của PlcHost/LedHost. Lý do:
    // hai vòng đó phụ thuộc cờ plc:enabled / led:enabled, mà khi cờ tắt thì mọi
    // thiết bị đều báo offline dù đang sống. Một bảng giám sát báo sai vì cờ cấu
    // hình còn tệ hơn là không có bảng giám sát.
    //
    // Chỉ mở TCP rồi đóng ngay, KHÔNG bắt tay FINS. Bắt tay sẽ xin cấp node và
    // PLC chỉ có vài node — thăm dò kiểu đó vài lần là PLC hết node, từ chối cả
    // kết nối thật. Đã dính đúng lỗi này một lần khi dò thủ công.
    public class DeviceProbeService
    {
        // Cache để mở nhiều tab hoặc poll dày không biến thành tràn ngập kết nối
        // xuống thiết bị đang chạy.
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);
        private static readonly object _sync = new object();
        private static IList<DeviceGroupStatus> _cache;
        private static DateTime _cachedAtUtc;

        private const int ProbeTimeoutMs = 1500;
        // Chặn số socket mở cùng lúc. 112 PLC x 1 socket đồng thời là quá nhiều
        // cho một lần tải trang.
        private const int MaxParallel = 24;

        // Camera AI không có cổng nào để thăm dò và KHÔNG có heartbeat — nó chỉ
        // gửi khi có xe. Nên im lặng không kết luận được gì: có thể camera chết,
        // có thể đơn giản là đêm khuya không ai vào bãi.
        //
        // Quá ngưỡng này thì báo "chưa rõ" (vàng), KHÔNG phải "hỏng" (đỏ). Ngưỡng
        // để trong Web.config vì nó phụ thuộc lưu lượng từng bãi: 30 phút là hợp
        // lý cho bãi thương mại giờ cao điểm, nhưng ở chung cư lúc 11 giờ đêm thì
        // đó là báo động giả mỗi ngày.
        private const int DefaultCameraSilentMinutes = 120;

        private static TimeSpan CameraSilentAfter
        {
            get
            {
                int m;
                if (!int.TryParse(ConfigurationManager.AppSettings["camera:silentAfterMinutes"], out m) || m <= 0)
                    m = DefaultCameraSilentMinutes;
                return TimeSpan.FromMinutes(m);
            }
        }

        public IList<DeviceGroupStatus> GetSummary()
        {
            lock (_sync)
            {
                if (_cache != null && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
                    return _cache;
            }

            var groups = Build();

            lock (_sync)
            {
                _cache = groups;
                _cachedAtUtc = DateTime.UtcNow;
            }
            return groups;
        }

        private IList<DeviceGroupStatus> Build()
        {
            var result = new List<DeviceGroupStatus>();

            result.Add(CameraGroup());
            result.Add(ProbeGroup("plc", "PLC khối đỗ", PlcEndpoints()));
            result.Add(ProbeGroup("led", "Bảng LED", LedEndpoints()));
            result.Add(PgsGroup());

            // Máy phát thẻ: không có bảng, không có IP, không có giao thức nào đã
            // biết. Bản cũ ghi cứng "Giữ thẻ (Hold)" — một trạng thái nghiệp vụ
            // cụ thể cho thiết bị chưa hề được đấu nối vào hệ thống.
            result.Add(new DeviceGroupStatus
            {
                Key       = "dispenser",
                Name      = "Máy phát thẻ",
                Monitored = false,
                Detail    = "Chưa đấu nối vào SCADA"
            });

            return result;
        }

        // ------------------------------------------------------------- camera AI
        private DeviceGroupStatus CameraGroup()
        {
            var g = new DeviceGroupStatus { Key = "camera", Name = "Camera AI-VDS", Total = 1 };

            try
            {
                using (var conn = new MySqlConnection(Db.ConnectionString))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MAX(received_at) FROM vehicle_event";
                    conn.Open();
                    object v = cmd.ExecuteScalar();

                    if (v == null || v == DBNull.Value)
                    {
                        g.Monitored = true;
                        g.Online    = 0;
                        g.Detail    = "Chưa nhận sự kiện nào";
                        return g;
                    }

                    var last   = Convert.ToDateTime(v);
                    var age    = DateTime.Now - last;
                    var silent = age > CameraSilentAfter;

                    g.Monitored = true;
                    g.LastSeen  = last;
                    g.Online    = silent ? 0 : 1;
                    // Im lặng -> "chưa rõ", không phải "hỏng". Xem ghi chú ở
                    // CameraSilentAfter và DeviceGroupStatus.Inconclusive.
                    g.Inconclusive = silent;
                    g.Detail = silent
                        ? string.Format("Chưa rõ - không có sự kiện {0}", Humanize(age))
                        : "Đang nhận sự kiện";
                    return g;
                }
            }
            catch (Exception ex)
            {
                g.Monitored = false;
                g.Detail    = "Không đọc được CSDL: " + ex.Message;
                return g;
            }
        }

        private static string Humanize(TimeSpan d)
        {
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " phút";
            if (d.TotalHours   < 24) return (int)d.TotalHours + " giờ";
            return (int)d.TotalDays + " ngày";
        }

        // ------------------------------------------------------- danh sach dia chi
        private IList<Endpoint> PlcEndpoints()
        {
            try
            {
                return new PlcDeviceRepository().GetAll()
                    .Select(d => new Endpoint(d.IpAddress, d.Port, "Block " + d.BlockNo))
                    .ToList();
            }
            catch { return null; }
        }

        private IList<Endpoint> LedEndpoints()
        {
            try
            {
                return new LedPanelRepository().GetPanels(false)
                    .Select(p => new Endpoint(p.IpAddress, p.Port, p.Name ?? ("LED " + p.Code)))
                    .ToList();
            }
            catch { return null; }
        }

        // PGS KHÔNG tự mở socket thăm dò, khác ba nhóm còn lại.
        //
        // Vì sao: vòng nền đã giữ sẵn một kết nối tới CCU và biết chính xác nó
        // sống hay chết. Thăm dò thêm nghĩa là cứ mỗi lần hết hạn bộ nhớ đệm
        // (15 giây) lại mở một socket nữa tới đúng địa chỉ đó — chính là cách
        // 33 socket FinWait2 tích tụ ở .75 hồi tháng 9, vì CCU không hoàn tất
        // bắt tay đóng khi bị nối rồi cắt ngay.
        //
        // Đọc lại từ vòng nền vừa chính xác hơn vừa không tốn socket nào.
        private DeviceGroupStatus PgsGroup()
        {
            var g = new DeviceGroupStatus { Key = "pgs", Name = "Hệ thống PGS" };
            var ccu = Services.Pgs.PgsHost.Ccu;

            if (ccu == null)
            {
                g.Monitored = false;
                g.Detail    = "Chưa cấu hình CCU";
                return g;
            }

            g.Monitored = true;
            g.Total     = 1;
            g.Online    = ccu.IsOnline ? 1 : 0;

            var zcus = ccu.Snapshot();
            int song = zcus.Count(z => z.DangKetNoi && z.TuoiGoiGiay <= ccu.ZcuQuaHanGiay);

            if (ccu.IsOnline)
            {
                g.Detail = "CCU " + ccu.Host + " · " + song + "/" + zcus.Count + " ZCU đang kết nối";
            }
            else
            {
                g.Detail = ccu.LastError ?? "CCU không phản hồi";
                g.Offline.Add(ccu.Host);
            }

            return g;
        }

        // ------------------------------------------------------------- tham do TCP
        private DeviceGroupStatus ProbeGroup(string key, string name, IList<Endpoint> endpoints)
        {
            var g = new DeviceGroupStatus { Key = key, Name = name };

            if (endpoints == null)
            {
                g.Monitored = false;
                g.Detail    = "Chưa có danh sách địa chỉ";
                return g;
            }

            g.Monitored = true;
            g.Total     = endpoints.Count;

            if (endpoints.Count == 0)
            {
                g.Detail = "Chưa khai báo thiết bị nào";
                return g;
            }

            var results = ProbeAll(endpoints);

            g.Online   = results.Count(r => r.Value);
            g.LastSeen = DateTime.Now;
            foreach (var r in results.Where(r => !r.Value).Take(12))
                g.Offline.Add(r.Key.Label + " (" + r.Key.Host + ")");

            g.Detail = string.Format("{0}/{1} phản hồi", g.Online, g.Total);
            return g;
        }

        private IDictionary<Endpoint, bool> ProbeAll(IList<Endpoint> endpoints)
        {
            var map = new Dictionary<Endpoint, bool>();
            using (var gate = new System.Threading.SemaphoreSlim(MaxParallel))
            {
                var tasks = endpoints.Select(async ep =>
                {
                    await gate.WaitAsync().ConfigureAwait(false);
                    try { return new { ep, ok = await ProbeAsync(ep).ConfigureAwait(false) }; }
                    finally { gate.Release(); }
                }).ToArray();

                // Chặn ở đây là chấp nhận được: đây là endpoint chẩn đoán, và kết
                // quả đã được cache 15 giây nên không phải lần tải trang nào cũng
                // chờ. Task.WhenAll rồi .Result vì controller MVC5 đang đồng bộ.
                var all = Task.WhenAll(tasks).GetAwaiter().GetResult();
                foreach (var r in all) map[r.ep] = r.ok;
            }
            return map;
        }

        private static async Task<bool> ProbeAsync(Endpoint ep)
        {
            var client = new TcpClient();
            try
            {
                var connect = client.ConnectAsync(ep.Host, ep.Port);
                var done    = await Task.WhenAny(connect, Task.Delay(ProbeTimeoutMs))
                                        .ConfigureAwait(false);
                return done == connect && !connect.IsFaulted && client.Connected;
            }
            catch { return false; }
            finally
            {
                // Đóng ngay. Không gửi byte nào, không bắt tay — xem ghi chú đầu lớp.
                try { client.Close(); } catch { }
            }
        }

        public class Endpoint
        {
            public string Host  { get; private set; }
            public int    Port  { get; private set; }
            public string Label { get; private set; }

            public Endpoint(string host, int port, string label)
            {
                Host = host; Port = port; Label = label;
            }
        }
    }
}
