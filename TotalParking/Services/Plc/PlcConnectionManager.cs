using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Quản lý toàn bộ kết nối PLC và chạy vòng poll cho từng block.
    //
    // Một PLC một kết nối, một vòng poll độc lập. Với 2 PLC thí điểm thì mô hình
    // nào cũng chạy; chỗ cần cẩn thận là 112 PLC ở quy mô đầy đủ, nên hai điểm
    // được giữ ngay từ đầu:
    //
    //   - Mỗi nhịp poll của các block chạy SONG SONG nhưng có giới hạn. Chạy tuần
    //     tự thì một PLC treo 3 giây làm 111 block còn lại trễ theo. Chạy không
    //     giới hạn thì 112 socket cùng bật một lúc.
    //   - Poll chỉ đọc MỘT bit (cờ yêu cầu), không đọc cả khối. 112 block x 2
    //     lượt/giây là 224 khung/giây trên 112 kết nối — nhẹ. Đọc cả khối mỗi
    //     nhịp thì tốn gấp nhiều lần mà 99% số lần không có gì mới.
    //
    // Trạng thái sống (online/offline, lỗi cuối) chỉ nằm trong bộ nhớ ở đây,
    // không ghi DB mỗi nhịp — xem PlcDeviceRepository.
    public class PlcConnectionManager : IDisposable
    {
        // Số block được poll đồng thời. Giữ nhỏ hơn số PLC để không mở ồ ạt socket,
        // đủ lớn để một PLC chậm không chặn phần còn lại.
        private const int MaxConcurrentPolls = 16;

        // MẢNG BẤT BIẾN, không phải List. Vòng poll duyệt tập này mỗi nhịp, còn
        // Reload() thay cả tập trong lúc đó. Sửa tại chỗ một List đang bị duyệt là
        // lỗi chắc chắn; thay nguyên tử cả tham chiếu thì vòng đang chạy vẫn dùng
        // trọn vẹn tập cũ của nó rồi nhịp sau mới thấy tập mới.
        //
        // volatile để nhịp sau chắc chắn đọc được tham chiếu mới, không dính bản
        // sao trong cache của luồng.
        private volatile PlcConnection[] _connections = new PlcConnection[0];

        private readonly SemaphoreSlim _throttle = new SemaphoreSlim(MaxConcurrentPolls);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task _loop;
        private int  _pollMs = 500;

        public IEnumerable<PlcConnection> Connections { get { return _connections; } }

        // Tra theo SO BLOCK in tren ban ve, khong phai block_id noi bo: day la
        // con so nhan vien hien truong doc duoc tren tu dien.
        public PlcConnection Find(int blockNo)
        {
            return _connections.FirstOrDefault(c => c.Device.BlockNo == blockNo);
        }

        public bool IsRunning
        {
            get { return _loop != null && !_cts.IsCancellationRequested; }
        }

        // Nạp cấu hình PLC. Tách khỏi StartLoop có chủ ý: công cụ chẩn đoán
        // (/PlcStatus/Read, /PlcStatus/Write) cần danh sách kết nối nhưng KHÔNG
        // cần vòng poll. Lúc nghiệm thu, thứ tự hợp lý là đọc thử thanh ghi trước,
        // rồi mới bật vòng poll — nếu hai việc dính vào nhau thì muốn đọc một lần
        // cũng phải bật vòng chạy 500ms.
        public void Load()
        {
            if (_connections.Length > 0) return;

            var devices = new PlcDeviceRepository().GetAll();
            _connections = devices.Select(d => new PlcConnection(d)).ToArray();

            if (devices.Count > 0)
            {
                // Nhịp chung lấy theo PLC nhanh nhất. Từng PLC có poll_ms riêng
                // trong DB nhưng ở quy mô này một nhịp chung đơn giản hơn và đủ dùng.
                _pollMs = Math.Max(100, devices.Min(d => d.PollMs));
            }
        }

        // Kết quả một lượt nạp lại, để endpoint trả về cho người gọi.
        public class ReloadResult
        {
            public int Added   { get; set; }   // block mới được đưa vào vòng poll
            public int Removed { get; set; }   // block bị gỡ khỏi vòng poll
            public int Kept    { get; set; }   // giữ nguyên kết nối đang có
            public int Total   { get; set; }
        }

        // Nạp lại danh sách thiết bị từ DB mà KHÔNG khởi động lại ứng dụng.
        //
        // ===================== VÌ SAO GIỮ LẠI KẾT NỐI CŨ =====================
        // Block nào đã có kết nối và cấu hình không đổi thì dùng lại nguyên đối
        // tượng PlcConnection. Dựng mới tất cả nghĩa là 55 phiên FINS cùng đóng
        // rồi cùng mở lại — đúng kiểu gây loạt lỗi 0x20 "hết khe kết nối" đã thấy
        // mỗi lần app khởi động. Nạp lại để THÊM một block thì không có lý do gì
        // làm gián đoạn 55 block đang chạy tốt.
        //
        // Đổi IP hoặc cổng thì phải dựng mới, vì kết nối cũ đang trỏ tới thiết bị
        // khác. So theo Endpoint chứ không so từng trường: chỉ địa chỉ mới quyết
        // định kết nối có còn đúng chỗ hay không.
        public ReloadResult Reload()
        {
            var devices = new PlcDeviceRepository().GetAll();
            var dangCo  = _connections.ToDictionary(c => c.Device.BlockNo);
            var ketQua  = new ReloadResult();
            var tapMoi  = new List<PlcConnection>(devices.Count);

            foreach (var d in devices)
            {
                PlcConnection cu;
                if (dangCo.TryGetValue(d.BlockNo, out cu) && cu.Device.Endpoint == d.Endpoint)
                {
                    tapMoi.Add(cu);
                    dangCo.Remove(d.BlockNo);
                    ketQua.Kept++;
                }
                else
                {
                    tapMoi.Add(new PlcConnection(d));
                    ketQua.Added++;
                }
            }

            // Thay nguyên tử. Nhịp poll đang chạy vẫn dùng trọn tập cũ của nó.
            _connections = tapMoi.ToArray();

            // HOÃN đóng những kết nối không còn trong danh sách.
            //
            // Thay tham chiếu là nguyên tử, nhưng một nhịp poll đã bắt đầu trước đó
            // vẫn đang giữ tập CŨ và có thể đang gọi PollAsync trên chính những đối
            // tượng này. PlcConnection.Dispose() giải phóng cả semaphore nội bộ mà
            // không chờ ai, nên đóng ngay sẽ làm nhịp đó ném ObjectDisposedException.
            //
            // Lỗi đó sẽ bị nuốt ở vòng lặp nên không gây hậu quả, nhưng nó tạo ra
            // một dòng nhật ký khó hiểu cho người truy vết sau này. Chờ quá một chu
            // kỳ poll thì nhịp cũ chắc chắn đã xong.
            //
            // Gỡ block là việc hiếm, nên độ trễ vài giây ở đây không đáng kể.
            var canDong = dangCo.Values.ToArray();
            ketQua.Removed = canDong.Length;
            if (canDong.Length > 0)
            {
                int cho = Math.Max(2000, _pollMs * 3);
                Task.Delay(cho).ContinueWith(_ =>
                {
                    foreach (var bo in canDong)
                    {
                        try { bo.Dispose(); } catch (Exception) { }
                    }
                });
            }

            if (devices.Count > 0)
                _pollMs = Math.Max(100, devices.Min(d => d.PollMs));

            ketQua.Total = tapMoi.Count;
            return ketQua;
        }

        // Quét một lượt dọn câu trả lời còn sót ở D1000 trên mọi PLC.
        //
        // Gọi một lần lúc khởi động. Dùng ĐÚNG hàng rào throttle của vòng poll để
        // không mở ồ ạt kết nối — 112 PLC cùng lúc sẽ làm cạn node như đã từng
        // xảy ra khi dò thủ công.
        public async Task<int> ClearStaleAnswersAsync()
        {
            int cleared = 0;
            var tasks = _connections.Select(async c =>
            {
                await _throttle.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (await c.ClearStaleAnswerAsync().ConfigureAwait(false))
                        Interlocked.Increment(ref cleared);
                }
                catch (Exception)
                {
                    // ClearStaleAnswerAsync đã tự ghi nhật ký. Một PLC hỏng không
                    // được làm hỏng cả lượt dọn.
                }
                finally
                {
                    _throttle.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return cleared;
        }

        public void StartLoop()
        {
            if (_loop != null || _connections.Length == 0) return;
            _loop = Task.Run(() => LoopAsync(_cts.Token));
        }

        private async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var started = DateTime.UtcNow;

                try
                {
                    var batch = _connections.Select(c => PollOneAsync(c, token)).ToArray();
                    await Task.WhenAll(batch).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // PlcConnection.PollAsync đã nuốt lỗi của riêng nó; tới đây chỉ
                    // còn lỗi của chính vòng lặp. Không được để nó kết thúc vòng.
                }

                var elapsed = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                int wait = Math.Max(50, _pollMs - elapsed);
                try
                {
                    await Task.Delay(wait, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task PollOneAsync(PlcConnection conn, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            await _throttle.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await conn.PollAsync().ConfigureAwait(false);
            }
            finally
            {
                _throttle.Release();
            }
        }

        public void Stop()
        {
            if (_loop == null) return;
            _cts.Cancel();
            try { _loop.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _loop = null;
        }

        public void Dispose()
        {
            Stop();
            foreach (var c in _connections) c.Dispose();
            _connections = new PlcConnection[0];
            _throttle.Dispose();
            _cts.Dispose();
        }
    }
}
