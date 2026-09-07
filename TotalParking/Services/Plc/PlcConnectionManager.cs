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

        private readonly List<PlcConnection> _connections = new List<PlcConnection>();
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
            if (_connections.Count > 0) return;

            var devices = new PlcDeviceRepository().GetAll();
            foreach (var d in devices)
            {
                _connections.Add(new PlcConnection(d));
            }

            if (devices.Count > 0)
            {
                // Nhịp chung lấy theo PLC nhanh nhất. Từng PLC có poll_ms riêng
                // trong DB nhưng ở quy mô này một nhịp chung đơn giản hơn và đủ dùng.
                _pollMs = Math.Max(100, devices.Min(d => d.PollMs));
            }
        }

        public void StartLoop()
        {
            if (_loop != null || _connections.Count == 0) return;
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
            _connections.Clear();
            _throttle.Dispose();
            _cts.Dispose();
        }
    }
}
