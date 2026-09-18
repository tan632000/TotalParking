using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Web.Hosting;

namespace TotalParking.Services.Pgs
{
    // Giữ vòng đọc cảm biến đỗ thường sống cùng vòng đời ứng dụng, cùng khuôn
    // với PlcHost và LedHost.
    //
    // ===================== KHÁC HAI TẦNG KIA Ở MỘT ĐIỂM =====================
    // Tầng này CHỈ ĐỌC. Không gửi byte nào xuống ZCU, không ghi thanh ghi, không
    // điều khiển gì. Nên không cần công tắc "cho phép ghi tay" như plc:allowManualWrite.
    //
    // ===================== CHƯA NỐI VÀO BẢNG LED =====================
    // Đếm được bao nhiêu cảm biến đang khác trạng thái nền, nhưng CHƯA biết bit
    // nào ứng với ô đỗ nào, cũng chưa biết bit bật nghĩa là có xe hay trống.
    // Thiếu hai thứ đó thì không chia được số theo zone, mà bảng chỉ hướng cần
    // số theo zone.
    //
    // Nên giai đoạn này chỉ đọc, lọc nhiễu và phơi ra ở /PgsStatus. Nối vào
    // v_led_capacity.used_standard sau khi có bảng ánh xạ cảm biến -> ô đỗ.
    public class PgsHost : IRegisteredObject
    {
        private static readonly object Sync = new object();
        private static PgsHost _instance;

        private readonly List<PgsConnection> _conns = new List<PgsConnection>();
        private Thread _loop;
        private volatile bool _stop;

        public static bool Enabled
        {
            get { return ReadBool("pgs:enabled", false); }
        }

        // Thời gian trạng thái phải giữ nguyên trước khi được chấp nhận.
        // Mặc định 10 giây: ZCU 2 có cảm biến hỏng sinh 4.121 lần đổi một ngày,
        // có lúc hai lần trong một giây. Ngưỡng thấp hơn thì nhiễu vẫn lọt.
        public static int DebounceMs
        {
            get { return ReadInt("pgs:debounceMs", 10000); }
        }

        public static int TimeoutMs { get { return ReadInt("pgs:timeoutMs", 4000); } }
        public static int Port      { get { return ReadInt("pgs:port", 2000); } }

        public static string[] Hosts
        {
            get
            {
                string v = ConfigurationManager.AppSettings["pgs:hosts"];
                if (string.IsNullOrWhiteSpace(v)) return new string[0];
                return v.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => x.Length > 0)
                        .ToArray();
            }
        }

        public static IEnumerable<PgsConnection> Connections
        {
            get
            {
                var i = _instance;
                return i == null ? new PgsConnection[0] : i._conns.ToArray();
            }
        }

        public static bool IsRunning
        {
            get { var i = _instance; return i != null && i._loop != null; }
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_instance != null) return;

                var host = new PgsHost();
                HostingEnvironment.RegisterObject(host);
                _instance = host;

                if (!Enabled) return;

                foreach (var h in Hosts)
                    host._conns.Add(new PgsConnection(h, Port, DebounceMs));

                if (host._conns.Count == 0) return;

                host._loop = new Thread(host.Run) { IsBackground = true, Name = "PgsHost" };
                host._loop.Start();
            }
        }

        // Một luồng cho tất cả ZCU, không phải một luồng mỗi con.
        //
        // Số thiết bị nhỏ (5) và mỗi lần đọc chặn tối đa TimeoutMs, nên vòng tuần
        // tự vẫn theo kịp: ZCU đẩy 2-3 gói mỗi giây, mà mỗi gói là trạng thái đầy
        // đủ nên lỡ vài gói không mất gì.
        private void Run()
        {
            while (!_stop)
            {
                foreach (var c in _conns)
                {
                    if (_stop) break;
                    try { c.Poll(TimeoutMs); }
                    catch (Exception)
                    {
                        // PgsConnection đã tự nuốt lỗi của nó. Tới đây chỉ còn lỗi
                        // của chính vòng lặp — không được để nó kết thúc vòng.
                    }
                }
                if (!_stop) Thread.Sleep(50);
            }
        }

        public void Stop(bool immediate)
        {
            _stop = true;
            try
            {
                if (_loop != null && !immediate) _loop.Join(2000);
            }
            catch (Exception) { }

            foreach (var c in _conns)
            {
                try { c.Dispose(); } catch (Exception) { }
            }

            HostingEnvironment.UnregisterObject(this);
            lock (Sync) { if (_instance == this) _instance = null; }
        }

        private static bool ReadBool(string key, bool dflt)
        {
            bool b;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out b) ? b : dflt;
        }

        private static int ReadInt(string key, int dflt)
        {
            int n;
            return int.TryParse(ConfigurationManager.AppSettings[key], out n) ? n : dflt;
        }
    }
}
