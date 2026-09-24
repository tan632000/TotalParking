using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Chiếu trạng thái kết nối của vòng poll xuống ba cột trong plc_device.
    //
    // ===================== VÌ SAO LÀ MỘT LUỒNG RIÊNG =====================
    // Đây là ràng buộc cứng, không phải sở thích kiến trúc.
    //
    // Vòng poll ở PlcConnectionManager.LoopAsync chỉ bọc try quanh Task.WhenAll;
    // phần tính thời gian và Task.Delay nằm NGOÀI khối try đó. Một MySqlException
    // ném ra từ trong vòng lặp sẽ thoát khỏi LoopAsync và giết vòng poll vĩnh
    // viễn — trong khi IsRunning vẫn trả true vì _loop khác null. Cả 112 PLC
    // ngừng được đọc mà giao diện vẫn báo "đang chạy".
    //
    // Nên writer chạy riêng, tự nuốt lỗi của mình CÓ ghi log, và không bao giờ
    // ném lên vòng poll. Cơ sở dữ liệu chết không được phép làm mù tầng PLC.
    //
    // ===================== VÌ SAO KHÔNG GHI MỖI NHỊP =====================
    // 112 PLC poll 500ms mà ghi mỗi nhịp sẽ thành hàng trăm UPDATE mỗi giây vào
    // một bảng cấu hình. Writer chỉ ghi khi trạng thái ĐỔI so với lần chiếu
    // trước, và có thêm khử rung: một PLC phải giữ nguyên trạng thái mới đủ
    // XacNhanLan lần quan sát liên tiếp thì mới được ghi xuống.
    //
    // Khử rung là bắt buộc chứ không phải tối ưu: PlcConnection.MarkOk đặt lại
    // chuỗi lỗi về 0 mỗi lần nối lại được, nên một PLC chập chờn lật trạng thái
    // gần như mỗi nhịp. Không khử rung thì "chỉ ghi khi đổi" vẫn ra hai UPDATE
    // mỗi giây cho riêng thiết bị đó.
    public static class PlcTrangThaiWriter
    {
        // Nhịp chiếu. Dài hơn nhịp poll rất nhiều: đây là bản chiếu để người đọc
        // truy vấn, không phải nguồn sự thật lúc chạy.
        private const int NhipMs = 5000;

        // Số lần quan sát liên tiếp phải thấy cùng một trạng thái mới thì mới ghi.
        // Với nhịp 5 giây, 3 lần nghĩa là PLC phải ổn định ~15 giây.
        private const int XacNhanLan = 3;

        private static readonly object Sync = new object();
        private static Task _loop;
        private static CancellationTokenSource _cts;

        // Trạng thái đã ghi xuống CSDL, theo plc_id.
        private static readonly Dictionary<int, bool> _daGhi = new Dictionary<int, bool>();
        // Trạng thái đang chờ đủ số lần xác nhận, theo plc_id.
        private static readonly Dictionary<int, KeyValuePair<bool, int>> _dangCho =
            new Dictionary<int, KeyValuePair<bool, int>>();

        public static long SoLanGhi   { get; private set; }
        public static long SoLanLoi   { get; private set; }
        public static string LoiCuoi  { get; private set; }
        public static DateTime? ChieuCuoiUtc { get; private set; }

        public static bool IsRunning { get { return _loop != null; } }

        public static void Start(PlcConnectionManager manager)
        {
            lock (Sync)
            {
                if (_loop != null || manager == null) return;
                _cts = new CancellationTokenSource();
                _loop = Task.Run(() => VongAsync(manager, _cts.Token));
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

        private static async Task VongAsync(PlcConnectionManager manager, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ChieuMotLuot(manager);
                }
                catch (Exception ex)
                {
                    // Nuốt CÓ ghi lại. Nuốt im lặng là cách hỏng tệ nhất: cột
                    // không bao giờ có dữ liệu mà không ai biết vì sao.
                    SoLanLoi++;
                    LoiCuoi = ex.Message;
                    PlcAuditLog.Error(null, 0, "GHI TRANG THAI",
                                      "Khong ghi duoc is_connected: " + ex.Message);
                }

                try { await Task.Delay(NhipMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static void ChieuMotLuot(PlcConnectionManager manager)
        {
            var conns = manager.Connections.ToList();
            if (conns.Count == 0) return;

            var doi = new List<KeyValuePair<int, bool>>();
            var dangPoll = new HashSet<int>();

            foreach (var c in conns)
            {
                int id = c.Device.PlcId;
                dangPoll.Add(id);
                bool song = c.IsOnline;

                bool daBiet;
                if (_daGhi.TryGetValue(id, out daBiet) && daBiet == song)
                {
                    _dangCho.Remove(id);      // đã đúng rồi, không còn gì chờ
                    continue;
                }

                // Khác với cái đã ghi -> đếm số lần xác nhận liên tiếp.
                KeyValuePair<bool, int> cho;
                if (_dangCho.TryGetValue(id, out cho) && cho.Key == song)
                {
                    int lan = cho.Value + 1;
                    if (lan >= XacNhanLan)
                    {
                        doi.Add(new KeyValuePair<int, bool>(id, song));
                        _dangCho.Remove(id);
                    }
                    else
                    {
                        _dangCho[id] = new KeyValuePair<bool, int>(song, lan);
                    }
                }
                else
                {
                    _dangCho[id] = new KeyValuePair<bool, int>(song, 1);
                }
            }

            var repo = new PlcDeviceRepository();

            // last_probe_at cập nhật cho TOÀN BỘ dòng đang poll bằng một câu duy
            // nhất, kể cả khi không có gì đổi: đó là thứ cho biết số liệu còn
            // tươi hay đã đóng băng vì site chết.
            repo.GhiMocQuanSat(dangPoll);

            foreach (var d in doi)
            {
                repo.GhiTrangThaiKetNoi(d.Key, d.Value);
                _daGhi[d.Key] = d.Value;
                SoLanGhi++;
            }

            // Thiết bị đã rời vòng poll thì không còn quan sát được nữa. Để
            // nguyên giá trị cũ là nói dối: cột sẽ mãi báo "đang kết nối" cho một
            // thiết bị không ai còn nối tới.
            var raKhoiPoll = _daGhi.Keys.Where(k => !dangPoll.Contains(k)).ToList();
            if (raKhoiPoll.Count > 0)
            {
                repo.XoaTrangThaiKetNoi(raKhoiPoll);
                foreach (var k in raKhoiPoll) { _daGhi.Remove(k); _dangCho.Remove(k); }
            }

            ChieuCuoiUtc = DateTime.UtcNow;
        }
    }
}
