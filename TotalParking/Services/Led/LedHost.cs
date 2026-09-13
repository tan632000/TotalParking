using System;
using System.Configuration;
using System.Web.Hosting;

namespace TotalParking.Services.Led
{
    // Giữ vòng đẩy LED sống cùng vòng đời ứng dụng web, cùng khuôn với PlcHost.
    //
    // Mặc định TẮT. Khác với tầng PLC ở một điểm quan trọng: khi tắt vòng đẩy
    // có trật tự, LedHost XOÁ mọi bảng về trạng thái trống trước khi buông.
    // Board giữ nội dung cuối vĩnh viễn và không có watchdog, nên không xoá
    // nghĩa là để lại một tấm bảng đang nói dối giữa hầm.
    public class LedHost : IRegisteredObject
    {
        private static readonly object Sync = new object();
        private static LedHost _instance;

        private LedPublisher _publisher;

        public static LedPublisher Publisher
        {
            get { return _instance == null ? null : _instance._publisher; }
        }

        public static bool Enabled
        {
            get { return ReadBool("led:enabled", false); }
        }

        // Cho phép gửi khung tuỳ ý từ endpoint nghiệm thu. Tách riêng khỏi
        // led:enabled: chạy vòng đẩy là việc thường ngày, còn cưỡng bức nội dung
        // một tấm bảng đang chỉ đường cho tài xế thì không.
        public static bool AllowManualSend
        {
            get { return ReadBool("led:allowManualSend", false); }
        }

        public static int IntervalMs
        {
            get { return ReadInt("led:intervalMs", 5000); }
        }

        public static int TimeoutMs
        {
            get { return ReadInt("led:timeoutMs", 3000); }
        }

        public static void Initialize()
        {
            lock (Sync)
            {
                if (_instance != null) return;

                var host = new LedHost();
                HostingEnvironment.RegisterObject(host);
                _instance = host;

                try
                {
                    host._publisher = new LedPublisher(IntervalMs, TimeoutMs);
                    host._publisher.Load();
                    if (Enabled) host._publisher.StartLoop();
                }
                catch (Exception)
                {
                    // Chưa chạy 09_led_panel.sql, chưa có DB — ứng dụng web vẫn
                    // phải lên được, chỉ là không có tầng LED.
                    host._publisher = null;
                }
            }
        }

        public void Stop(bool immediate)
        {
            lock (Sync)
            {
                if (_publisher != null)
                {
                    // Dừng nhịp TRƯỚC rồi mới xoá bảng: nếu xoá trước thì nhịp
                    // kế tiếp sẽ đẩy lại số cũ và công xoá thành vô ích.
                    _publisher.Stop();
                    try { _publisher.BlankAll(); } catch { }
                    _publisher.Dispose();
                    _publisher = null;
                }
                _instance = null;
            }
            HostingEnvironment.UnregisterObject(this);
        }

        private static bool ReadBool(string key, bool fallback)
        {
            bool v;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out v) ? v : fallback;
        }

        private static int ReadInt(string key, int fallback)
        {
            int v;
            return int.TryParse(ConfigurationManager.AppSettings[key], out v) ? v : fallback;
        }
    }
}
