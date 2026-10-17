using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Vòng đọc D3 (mã lỗi) và CIO 100..102 (contactor, động cơ) của mọi khối.
    //
    // ===================== VÌ SAO KHÔNG GỘP VÀO VÒNG POLL QUẸT THẺ =====================
    // Vòng 500ms ở PlcConnectionManager là đường trả lời tài xế. Đọc thêm ở đó thì
    // mỗi PLC chậm làm chậm luôn câu trả lời tìm xe. Ở đây đọc qua
    // PlcConnection.ReadIoAsync, hàm nhường gate cho vòng quẹt thẻ — xem ghi chú
    // tại hàm đó.
    //
    // ===================== CHẠY HẾT NHỊP RỒI MỚI CHỜ =====================
    // Không dùng Timer: nhịp chậm (PLC timeout) sẽ chồng lên nhịp sau, và hai nhịp
    // cùng sửa trạng thái một khối. Cùng mẫu với PlcConnectionManager.LoopAsync.
    //
    // Đọc song song (tối đa 16 PLC, semaphore RIÊNG — throttle của manager là
    // private và phục vụ vòng khác), nhưng XỬ LÝ kết quả tuần tự sau khi cả nhịp
    // đọc xong: trạng thái chống rung không cần khoá, và lệnh ghi DB chỉ phát sinh
    // khi có chuyển trạng thái.
    //
    // ===================== VÒNG ĐỜI =====================
    // Start/Stop do PlcHost gọi. Stop chờ nhịp đang chạy kết thúc TRƯỚC khi
    // PlcHost Dispose các kết nối, để không có lệnh đọc nào chạm gate đã huỷ.
    public static class PlcIoPoller
    {
        private const int DefaultPollMs    = 1000;
        private const int MinPollMs        = 500;
        private const int DefaultTimeoutMs = 800;
        private const int DefaultDebounce  = 3;
        private const int MaxConcurrent    = 16;
        private const int StopWaitMs       = 5000;

        private static readonly object Sync = new object();
        private static CancellationTokenSource _cts;
        private static Task _loop;

        // Dùng lại qua các nhịp: trạng thái chống rung và trạng thái động cơ sống
        // cùng vòng.
        private static LoiD3Service _loiD3;

        // Null tới khi nạp được loại khối từ DB. Nạp trong vòng chứ không ở Start:
        // DB chưa sẵn lúc khởi động thì nhịp sau thử lại, thay vì tắt hẳn việc đếm.
        private static DongCoDetector _dongCo;

        public static DateTime? ChayCuoiUtc  { get; private set; }
        public static int       LastTickMs   { get; private set; }
        public static int       LastReadOk   { get; private set; }
        public static int       LastSkipped  { get; private set; }
        public static string    LoiCuoi      { get; private set; }

        public static bool IsRunning { get { return _loop != null; } }

        public static bool Enabled   { get { return ReadBool("plc:ioPoll", true); } }

        public static int PollMs
        {
            get { return Math.Max(MinPollMs, ReadInt("plc:ioPollMs", DefaultPollMs)); }
        }

        private static int TimeoutMs
        {
            get { return Math.Max(100, ReadInt("plc:ioTimeoutMs", DefaultTimeoutMs)); }
        }

        public static void Start()
        {
            if (!Enabled) return;
            lock (Sync)
            {
                if (_loop != null) return;
                _loiD3 = new LoiD3Service(new CanhBaoD3Store(),
                                          Math.Max(1, ReadInt("plc:d3Debounce", DefaultDebounce)));
                _dongCo = null;
                _cts  = new CancellationTokenSource();
                var ct = _cts.Token;
                _loop = Task.Run(() => VongAsync(ct));
            }
        }

        public static void Stop()
        {
            Task loop;
            lock (Sync)
            {
                if (_cts != null) { _cts.Cancel(); _cts = null; }
                loop  = _loop;
                _loop = null;
            }

            // Chờ ngoài lock: nhịp đang chạy không cần lock này, nhưng giữ lock
            // trong lúc chờ sẽ chặn một Start() song song không cần thiết.
            if (loop != null)
            {
                try { loop.Wait(StopWaitMs); }
                catch (AggregateException) { }
            }
        }

        private static async Task VongAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var dongHo = Stopwatch.StartNew();
                try
                {
                    await NhipAsync(ct).ConfigureAwait(false);
                    LoiCuoi = null;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Nuốt: lỗi thoát khỏi vòng sẽ giết luồng trong khi IsRunning
                    // vẫn báo true.
                    LoiCuoi = ex.Message;
                    PlcAuditLog.Error(null, 0, "IO POLL", ex.Message);
                }

                LastTickMs  = (int)dongHo.ElapsedMilliseconds;
                ChayCuoiUtc = DateTime.UtcNow;

                int cho = Math.Max(50, PollMs - LastTickMs);
                try { await Task.Delay(cho, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        private static async Task NhipAsync(CancellationToken ct)
        {
            var manager = PlcHost.Manager;
            if (manager == null) return;

            PlcConnection[] ketNoi = manager.Connections.ToArray();
            int timeout = TimeoutMs;

            using (var throttle = new SemaphoreSlim(MaxConcurrent))
            {
                var viec = ketNoi.Select(async c =>
                {
                    await throttle.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        return new KeyValuePair<PlcConnection, IoSnapshot>(
                            c, await c.ReadIoAsync(timeout).ConfigureAwait(false));
                    }
                    catch (Exception)
                    {
                        // Gate đã huỷ (đang tắt) hoặc lỗi bất ngờ: coi như bỏ nhịp.
                        return new KeyValuePair<PlcConnection, IoSnapshot>(c, null);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }).ToArray();

                KeyValuePair<PlcConnection, IoSnapshot>[] ketQua =
                    await Task.WhenAll(viec).ConfigureAwait(false);

                // Một mốc giờ cho cả nhịp: các lần đọc trong nhịp cách nhau dưới
                // một nhịp, đúng bằng độ phân giải của việc đếm.
                DateTime luc  = DateTime.Now;
                long     tick = Stopwatch.GetTimestamp();
                DongCoDetector dongCo = NapDongCo(ketNoi);

                int docDuoc = 0;
                foreach (var kq in ketQua)
                {
                    if (ct.IsCancellationRequested) return;
                    var d = kq.Key.Device;

                    if (kq.Value == null)
                    {
                        if (dongCo != null) dongCo.DocLoi(d.BlockNo);
                        continue;
                    }
                    docDuoc++;

                    _loiD3.QuanSat(d.BlockNo, d.ZoneId > 0 ? (int?)d.ZoneId : null, kq.Value.D3);

                    if (dongCo != null)
                    {
                        if (kq.Value.Cio == null) dongCo.DocLoi(d.BlockNo);
                        else dongCo.QuanSat(d.BlockNo, kq.Value.Cio, luc, tick);
                    }
                }

                LastReadOk = docDuoc;
                LastSkipped = ketQua.Length - docDuoc;
            }
        }

        // Tập khối của lần nạp bản đồ gần nhất ("1,2,5,..."). Đổi khi người vận hành
        // Reload danh sách PLC (PlcConnectionManager.Reload) — gỡ hoặc thêm khối.
        private static string _tapKhoiDaNap;

        // Nạp lại bản đồ loại khối VÀ dựng lại bộ nhận lượt chạy khi tập khối đổi.
        // Giữ trạng thái cũ qua một lần Reload là sai: khối bị gỡ lúc động cơ đang
        // chạy sẽ treo ở "đang chạy" (không còn ai gọi DocLoi cho nó), rồi khi được
        // thêm lại vài giờ sau, lần đọc OFF đầu tiên ghi một lượt dài vài giờ. Dựng
        // lại thì an toàn: mọi động cơ lại phải thấy OFF trước khi được đếm, nên
        // cái mất chỉ là lượt đang dở đúng lúc Reload.
        //
        // Bản đồ rỗng = chưa nạp được (DB chưa sẵn, bảng block trống), không phải
        // "không có khối nào": giữ nó thì bộ đếm câm vĩnh viễn. Lỗi lặp lại mỗi
        // nhịp không làm đầy nhật ký vì PlcAuditLog.Error bỏ qua thông báo trùng.
        private static DongCoDetector NapDongCo(PlcConnection[] ketNoi)
        {
            string tapKhoi = string.Join(",", ketNoi.Select(c => c.Device.BlockNo).OrderBy(n => n));
            if (_dongCo != null && tapKhoi == _tapKhoiDaNap) return _dongCo;

            try
            {
                var loaiKhoi = new MotorRunRepository().DocLoaiKhoi();
                if (loaiKhoi.Count == 0)
                {
                    PlcAuditLog.Error(null, 0, "DONG CO", "Ban do loai khoi rong, chua dem dong co");
                    _dongCo = null;
                    return null;
                }
                _dongCo        = new DongCoDetector(new MotorRunStore(), loaiKhoi);
                _tapKhoiDaNap  = tapKhoi;
            }
            catch (Exception ex)
            {
                PlcAuditLog.Error(null, 0, "DONG CO", "Chua nap duoc loai khoi: " + ex.Message);
                _dongCo = null;
            }
            return _dongCo;
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
