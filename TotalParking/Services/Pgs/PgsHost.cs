using System;
using System.Configuration;
using System.Threading;
using System.Web.Hosting;

namespace TotalParking.Services.Pgs
{
    // Giữ vòng đọc cảm biến đỗ thường sống cùng vòng đời ứng dụng, cùng khuôn
    // với PlcHost và LedHost.
    //
    // ===================== KHÁC HAI TẦNG KIA Ở MỘT ĐIỂM =====================
    // Tầng này gần như chỉ đọc: byte duy nhất gửi xuống là lệnh giữ nhịp
    // $CCU,01,LIVE*42#, không ghi cấu hình, không điều khiển gì. Nên không cần
    // công tắc "cho phép ghi tay" như plc:allowManualWrite.
    //
    // ===================== VÌ SAO MỘT LUỒNG RIÊNG CHO CCU =====================
    // Bản cũ chạy một luồng tuần tự cho cả 5 ZCU, mỗi lần đọc chặn tới TimeoutMs.
    // Với ZCU thì được, vì chúng tự đẩy khung và không đòi hỏi gì từ mình.
    //
    // CCU thì có hạn chót: quá 30 giây không nhận được lệnh nào là nó ngừng đẩy
    // dữ liệu và đóng socket (tài liệu mục 2.2.1). Nếu nhét CCU vào vòng chung,
    // vài thiết bị im tiếng mỗi con chặn 4 giây là đủ đẩy khe giữa hai lệnh LIVE
    // vượt 30 giây — và hỏng theo kiểu tệ nhất: số đứng im, không lỗi nào trong log.
    //
    // Nên đồng hồ LIVE phải độc lập với mọi thiết bị khác.
    //
    // ===================== CHƯA NỐI VÀO BẢNG LED =====================
    // Đã biết chính xác ô nào có xe, nhưng CHƯA biết cảm biến nào thuộc zone nào
    // — bảng ánh xạ đó là thứ cấu hình riêng cho từng ZCU (tài liệu mục 4.4).
    // Bảng chỉ hướng cần số theo zone, nên chưa nối được.
    //
    // Giai đoạn này chỉ đọc và phơi ra ở /PgsStatus để đối chiếu thực địa.
    public class PgsHost : IRegisteredObject
    {
        private static readonly object Sync = new object();
        private static PgsHost _instance;

        private CcuConnection _ccu;
        private Thread _loop;
        private volatile bool _stop;

        public static bool Enabled
        {
            get { return ReadBool("pgs:enabled", false); }
        }

        public static int TimeoutMs      { get { return ReadInt("pgs:timeoutMs", 4000); } }
        public static int LiveIntervalMs { get { return ReadInt("pgs:liveIntervalMs", 5000); } }
        public static int ZcuQuaHanMs    { get { return ReadInt("pgs:zcuQuaHanMs", 10000); } }
        public static int CcuPort        { get { return ReadInt("pgs:ccuPort", 2000); } }

        public static string CcuHost
        {
            get
            {
                string v = ConfigurationManager.AppSettings["pgs:ccuHost"];
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
        }

        public static CcuConnection Ccu
        {
            get { var i = _instance; return i == null ? null : i._ccu; }
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
                if (CcuHost == null) return;

                host._ccu = new CcuConnection(CcuHost, CcuPort, LiveIntervalMs, ZcuQuaHanMs);
                host._loop = new Thread(host.Run) { IsBackground = true, Name = "PgsHost" };
                host._loop.Start();
            }
        }

        private void Run()
        {
            while (!_stop)
            {
                try { _ccu.Poll(TimeoutMs); }
                catch (Exception)
                {
                    // CcuConnection đã tự nuốt lỗi của nó. Tới đây chỉ còn lỗi của
                    // chính vòng lặp — không được để nó kết thúc vòng.
                }
                if (!_stop) Thread.Sleep(20);
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

            try { if (_ccu != null) _ccu.Dispose(); } catch (Exception) { }

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
