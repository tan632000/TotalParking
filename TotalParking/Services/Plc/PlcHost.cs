using System;
using System.Configuration;
using System.Web.Hosting;

namespace TotalParking.Services.Plc
{
    // Giữ vòng poll PLC sống cùng vòng đời của ứng dụng web.
    //
    // Dùng IRegisteredObject chứ không phải QueueBackgroundWorkItem: cái sau dành
    // cho việc ngắn chạy một lần rồi thôi, còn đây là vòng lặp không kết thúc và
    // cần được báo trước khi IIS gỡ AppDomain. Không đăng ký thì lúc app pool
    // recycle vòng lặp bị cắt giữa chừng, để lại socket TCP mở phía PLC.
    //
    // Mặc định TẮT (plc:enabled = false). Lý do: máy dev không nối được vào mạng
    // PLC, và một vòng poll cứ 500ms lại quay số vào IP không tồn tại sẽ làm đầy
    // log và làm chậm mọi lần F5. Bật ở máy có mạng OT.
    public class PlcHost : IRegisteredObject
    {
        private static readonly object Sync = new object();
        private static PlcHost _instance;

        private PlcConnectionManager _manager;

        public static PlcConnectionManager Manager
        {
            get { return _instance == null ? null : _instance._manager; }
        }

        public static bool Enabled
        {
            get { return ReadBool("plc:enabled", false); }
        }

        // Cho phép ghi thẳng thanh ghi từ endpoint nghiệm thu. Tách riêng khỏi
        // plc:enabled: chạy vòng poll là việc thường ngày, còn cưỡng bức một
        // thanh ghi trên thiết bị thật thì không — và W75.0 = 1 sai lúc nghĩa là
        // HMI cho gửi xe vào block đang giữ xe khác.
        public static bool AllowManualWrite
        {
            get { return ReadBool("plc:allowManualWrite", false); }
        }

        // LUÔN nạp cấu hình PLC, nhưng chỉ chạy vòng poll khi plc:enabled.
        //
        // Nạp cả khi tắt là chủ ý: công cụ nghiệm thu /PlcStatus/Read cần danh
        // sách kết nối. Thứ tự làm việc ngoài hiện trường là đọc thử D100 trước
        // để biết bố cục mã thẻ, rồi mới bật vòng poll — nếu phải bật poll mới
        // đọc được thì thứ tự đó không làm được.
        //
        // Nạp cấu hình không mở socket nào. Kết nối chỉ được thiết lập khi có
        // người gọi Read/Write, hoặc khi vòng poll chạy.
        public static void Initialize()
        {
            lock (Sync)
            {
                if (_instance != null) return;

                var host = new PlcHost();
                HostingEnvironment.RegisterObject(host);
                _instance = host;

                try
                {
                    host._manager = new PlcConnectionManager();
                    host._manager.Load();
                    if (Enabled) host._manager.StartLoop();
                }
                catch (Exception)
                {
                    // Không có DB, chưa chạy migration, chưa seed plc_device —
                    // đều là trạng thái bình thường ở máy chưa cấu hình. Ứng dụng
                    // web phải lên được, chỉ là không có tầng PLC.
                    host._manager = null;
                }
            }
        }

        public void Stop(bool immediate)
        {
            lock (Sync)
            {
                if (_manager != null)
                {
                    _manager.Dispose();
                    _manager = null;
                }
                _instance = null;
            }
            HostingEnvironment.UnregisterObject(this);
        }

        private static bool ReadBool(string key, bool fallback)
        {
            string raw = ConfigurationManager.AppSettings[key];
            bool value;
            return bool.TryParse(raw, out value) ? value : fallback;
        }
    }
}
