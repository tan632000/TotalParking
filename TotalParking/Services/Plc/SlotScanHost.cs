using System;
using System.Configuration;
using System.Threading;
using System.Web.Hosting;

namespace TotalParking.Services.Plc
{
    // Vòng quét trạng thái ô đỗ: đọc D400/D202…D308 trên mọi PLC đang kết nối,
    // lưu vào plc_slot_state.
    //
    // TÁCH RIÊNG khỏi plc:enabled là chủ ý. Vòng poll của PlcHost có GHI xuống PLC
    // (D1000 khi có người tìm xe); vòng này CHỈ ĐỌC. Gộp hai thứ vào một công tắc
    // sẽ buộc phải bật quyền ghi mới có được dữ liệu chiếm chỗ — trong khi dữ liệu
    // chiếm chỗ là thứ bảng LED và chức năng tìm xe cần trước tiên.
    //
    // Chu kỳ mặc định 45 giây: xe vào/ra không nhanh hơn thế, và 112 PLC × 3 lệnh
    // đọc mỗi vòng là 336 khung — chạy dày hơn chỉ làm cạn node của PLC mà không
    // thêm thông tin. Đã dính lỗi cạn node một lần khi dò thủ công.
    public class SlotScanHost : IRegisteredObject
    {
        private const int DefaultIntervalMs = 45000;
        private const int MinIntervalMs     = 10000;

        private static readonly object Sync = new object();
        private static SlotScanHost _instance;

        private readonly SlotOccupancyReader _reader = new SlotOccupancyReader();
        private Timer _timer;
        private int   _busy;

        // Kết quả lần quét gần nhất, để /SlotStatus phơi ra mà không phải quét lại.
        public static DateTime? LastRunAt   { get; private set; }
        public static string    LastSummary { get; private set; }
        public static string    LastError   { get; private set; }

        public static bool Enabled
        {
            get
            {
                bool b;
                string v = ConfigurationManager.AppSettings["plc:slotScanEnabled"];
                return bool.TryParse(v, out b) && b;
            }
        }

        public static int IntervalMs
        {
            get
            {
                int ms;
                if (!int.TryParse(ConfigurationManager.AppSettings["plc:slotScanMs"], out ms))
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

                var host = new SlotScanHost();
                HostingEnvironment.RegisterObject(host);
                _instance = host;

                if (!Enabled) return;

                // dueTime 5 giây: để PlcHost kịp nạp xong danh sách kết nối.
                host._timer = new Timer(_ => host.Tick(), null, 5000, IntervalMs);
            }
        }

        private void Tick()
        {
            // Nhịp trước còn chạy thì bỏ nhịp này. Một vòng quét 112 PLC có thể lâu
            // hơn chu kỳ khi nhiều con không phản hồi; xếp hàng chỉ làm dồn kết nối.
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;

            try
            {
                var manager = PlcHost.Manager;
                if (manager == null)
                {
                    LastError = "Chua nap duoc cau hinh PLC.";
                    return;
                }

                var r = _reader.ScanAsync(manager).GetAwaiter().GetResult();

                LastRunAt = DateTime.Now;
                LastError = null;
                LastSummary = string.Format(
                    "{0}/{1} block doc duoc, {2} o, {3} co xe, {4} nghi ngo, {5} doi",
                    r.BlocksRead, r.BlocksTried, r.SlotsRead, r.Occupied, r.Suspect, r.Changed);

                // Lỗi của từng PLC không làm hỏng vòng quét, nhưng phải nhìn thấy
                // được — nếu không thì một PLC chết âm thầm sẽ khiến ô của nó giữ
                // mãi giá trị cũ, và người tìm xe được chỉ tới chỗ không có xe.
                if (r.Failures.Count > 0)
                    LastSummary += string.Format("  |  {0} block loi", r.Failures.Count);
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
                _instance = null;
            }
            HostingEnvironment.UnregisterObject(this);
        }
    }
}
