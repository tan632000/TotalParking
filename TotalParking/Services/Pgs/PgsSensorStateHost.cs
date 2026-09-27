using System;
using System.Configuration;
using System.Threading;
using System.Web.Hosting;

namespace TotalParking.Services.Pgs
{
    // Vòng ghi trạng thái cảm biến đỗ thường xuống pgs_sensor_state.
    //
    // ===================== VÌ SAO TÁCH KHỎI CcuConnection =====================
    // CcuConnection.Nap() chạy trên luồng đọc socket. Ghi cơ sở dữ liệu ngay tại
    // đó nghĩa là mỗi lần MySQL chậm một nhịp thì luồng đọc CCU đứng lại, và
    // khung kế tiếp dồn trong bộ đệm. Lớp này chỉ chụp ảnh trạng thái đã có sẵn
    // trong bộ nhớ rồi ghi, nên CCU chạy độc lập với tốc độ của cơ sở dữ liệu.
    //
    // ===================== NHỊP 5 GIÂY =====================
    // CCU đẩy trọn một vòng 5 ZCU mỗi ~2,5 giây (đo 27/09/2026: 2,0 gói/giây).
    // Ghi mỗi 5 giây là mỗi ảnh chụp đều mới, mà số lần ghi chỉ bằng một nửa số
    // vòng đọc. Nhanh hơn không thêm thông tin vì chính CCU không gửi nhanh hơn.
    //
    // Ô đỗ thường không đổi trạng thái trong vòng vài giây, nên độ trễ 5 giây
    // không ảnh hưởng tới con số trên bảng LED.
    public class PgsSensorStateHost : IRegisteredObject
    {
        private const int DefaultIntervalMs = 5000;
        private const int MinIntervalMs     = 2000;

        private static readonly object Sync = new object();
        private static PgsSensorStateHost _instance;

        private readonly PgsSensorStateWriter _writer = new PgsSensorStateWriter();
        private Timer _timer;
        private int   _busy;

        // Kết quả lần ghi gần nhất, để /PgsStatus phơi ra mà không phải ghi lại.
        public static DateTime? LastRunAt   { get; private set; }
        public static string    LastSummary { get; private set; }
        public static string    LastError   { get; private set; }

        // Công tắc RIÊNG, không dùng chung pgs:enabled. Tắt vòng ghi mà vẫn giữ
        // vòng đọc là một trạng thái hợp lệ: khi cơ sở dữ liệu đang bảo trì thì
        // bảng LED vẫn phải chạy bằng số liệu trong bộ nhớ.
        public static bool Enabled
        {
            get
            {
                bool b;
                string v = ConfigurationManager.AppSettings["pgs:ghiTrangThai"];
                // Mặc định BẬT: không có bảng ghi thì trang Điều hướng xe và mọi
                // báo cáo đỗ thường đều quay lại đọc con số đứng yên ở sức chứa.
                if (string.IsNullOrEmpty(v)) return true;
                return bool.TryParse(v, out b) && b;
            }
        }

        public static int IntervalMs
        {
            get
            {
                int ms;
                if (!int.TryParse(ConfigurationManager.AppSettings["pgs:ghiTrangThaiMs"], out ms))
                    ms = DefaultIntervalMs;
                return Math.Max(MinIntervalMs, ms);
            }
        }

        public static bool IsRunning
        {
            get { return _instance != null && _instance._timer != null; }
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_instance != null) return;

                var host = new PgsSensorStateHost();
                HostingEnvironment.RegisterObject(host);
                _instance = host;

                if (!Enabled) return;

                // dueTime 8 giây: đủ để PgsHost nối được CCU và nhận trọn một
                // vòng 5 ZCU. Ghi sớm hơn chỉ tạo một lượt rỗng.
                host._timer = new Timer(_ => host.Tick(), null, 8000, IntervalMs);
            }
        }

        private void Tick()
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;

            try
            {
                var ccu = PgsHost.Ccu;
                if (ccu == null)
                {
                    LastError = "PgsHost chua khoi tao.";
                    return;
                }

                var zcus = ccu.Snapshot();
                var r = _writer.Ghi(zcus);

                if (!string.IsNullOrEmpty(r.Loi))
                {
                    LastError = r.Loi;
                    return;
                }

                LastRunAt   = DateTime.Now;
                LastError   = null;
                LastSummary = string.Format("{0} ZCU, {1} cam bien", r.SoZcu, r.SoCamBien);

                // Không ZCU nào đang kết nối thì không có gì để ghi — nhưng đó là
                // tin đáng biết, không phải chuyện bình thường. Nói rõ thay vì để
                // LastSummary hiện "0 ZCU" trông như vòng ghi vừa chạy xong êm.
                if (r.SoZcu == 0)
                    LastSummary = "khong ZCU nao dang ket noi, khong ghi gi";
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        public void Stop(bool immediate)
        {
            lock (Sync)
            {
                if (_timer != null)
                {
                    _timer.Dispose();
                    _timer = null;
                }
                HostingEnvironment.UnregisterObject(this);
                _instance = null;
            }
        }
    }
}
