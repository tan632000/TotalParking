using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TotalParking.Models;

namespace TotalParking.Services.Led
{
    // Trạng thái sống của một bảng, giữ trong bộ nhớ. Không ghi DB mỗi nhịp —
    // cùng lý do với tầng PLC: đây là dữ liệu mất giá trị sau vài giây.
    public class LedPanelState
    {
        public LedPanel  Panel        { get; set; }
        public bool      Online       { get; set; }
        public DateTime? LastOkUtc    { get; set; }
        public string    LastError    { get; set; }
        public string    LastFrame    { get; set; }
        public string    LastAck      { get; set; }
        public long      SentCount    { get; set; }
        public long      FailCount    { get; set; }

        // Tuổi của lần publish thành công gần nhất. Đây là chỉ số quan trọng
        // nhất của cả tầng LED: board giữ nội dung cũ vĩnh viễn, nên "đã lâu
        // không publish được" đồng nghĩa với "bảng đang hiện số sai".
        public int? StaleSeconds
        {
            get
            {
                if (!LastOkUtc.HasValue) return null;
                return (int)(DateTime.UtcNow - LastOkUtc.Value).TotalSeconds;
            }
        }
    }

    // Đẩy số chỗ trống lên các bảng LED theo nhịp cố định.
    //
    // Nhịp phải ≤ 10 giây: spec yêu cầu tối thiểu một gói mỗi 10 giây, và board
    // đóng socket sau 30 giây im lặng. Nhịp mặc định 5 giây cho biên an toàn gấp
    // đôi — mất một nhịp vẫn chưa chạm ngưỡng.
    //
    // Một cổng hiển thị là MỘT khung tin. Bảng 3 hướng nhận 3 khung với
    // X1 = 0,1,2 chứ không phải một khung gộp.
    public class LedPublisher : IDisposable
    {
        public const int MaxIntervalMs = 10000;

        private readonly LedPanelRepository _repo = new LedPanelRepository();
        private readonly Dictionary<int, LedSocket>     _sockets = new Dictionary<int, LedSocket>();
        private readonly Dictionary<int, LedPanelState> _states  = new Dictionary<int, LedPanelState>();
        private readonly object _sync = new object();

        private readonly int _intervalMs;
        private readonly int _timeoutMs;

        private Timer _timer;
        private int   _busy;   // 0/1 — chặn hai nhịp chồng lên nhau

        public LedPublisher(int intervalMs, int timeoutMs)
        {
            _intervalMs = Math.Min(Math.Max(intervalMs, 1000), MaxIntervalMs);
            _timeoutMs  = timeoutMs;
        }

        public int IntervalMs { get { return _intervalMs; } }
        public bool IsRunning { get { return _timer != null; } }

        public IEnumerable<LedPanelState> States
        {
            get { lock (_sync) { return _states.Values.ToList(); } }
        }

        // Nạp danh mục bảng. Tách khỏi StartLoop cùng lý do với tầng PLC: công
        // cụ nghiệm thu cần danh sách bảng nhưng không cần vòng đẩy chạy.
        public void Load()
        {
            lock (_sync)
            {
                if (_states.Count > 0) return;
                foreach (var p in _repo.GetPanels())
                {
                    _states[p.PanelId] = new LedPanelState { Panel = p };
                }
            }
        }

        public void StartLoop()
        {
            lock (_sync)
            {
                if (_timer != null || _states.Count == 0) return;
                _timer = new Timer(_ => Tick(), null, 0, _intervalMs);
            }
        }

        private void Tick()
        {
            // Nhịp trước còn chạy thì bỏ nhịp này. Xếp hàng chỉ làm độ trễ dồn
            // lên khi một bảng chậm, mà nội dung khung tin thì luôn là số mới nhất
            // — bỏ một nhịp không mất thông tin gì.
            if (Interlocked.Exchange(ref _busy, 1) == 1) return;

            try
            {
                LedCapacity capacity;
                try
                {
                    capacity = _repo.GetCapacity();
                }
                catch (Exception ex)
                {
                    // Mất DB thì KHÔNG đẩy gì cả. Đẩy số cũ là nói dối tài xế;
                    // để bảng giữ số cũ cũng là nói dối, nhưng ít nhất
                    // StaleSeconds sẽ tăng lên và giám sát bắt được.
                    lock (_sync)
                    {
                        foreach (var s in _states.Values) s.LastError = "DB: " + ex.Message;
                    }
                    return;
                }

                foreach (var state in States)
                {
                    PublishPanel(state, capacity);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void PublishPanel(LedPanelState state, LedCapacity capacity)
        {
            var ports = state.Panel.Ports.Where(p => p.IsActive).ToList();
            if (ports.Count == 0) return;

            foreach (var port in ports)
            {
                // Cổng khai báo ZONES mà chưa biết dẫn tới zone nào thì bỏ qua.
                // Đẩy số toàn bãi lên một mũi tên chỉ về một hướng cụ thể là chỉ
                // sai — tệ hơn là không chỉ gì.
                if (port.Scope == LedPortScope.Zones && string.IsNullOrWhiteSpace(port.ZoneList))
                {
                    continue;
                }

                var hub   = LedFrameBuilder.Build(port, capacity);
                var frame = hub.GetCommand();

                try
                {
                    string ack = SocketFor(state.Panel).SendAndReceive(frame);

                    state.LastFrame = frame;
                    state.LastAck   = ack;
                    state.SentCount++;

                    if (LedSocket.IsAckFor(ack, port.PortIndex))
                    {
                        state.Online    = true;
                        state.LastOkUtc = DateTime.UtcNow;
                        state.LastError = null;
                    }
                    else
                    {
                        // Gửi được nhưng board trả về thứ không phải ACK hợp lệ.
                        // KHÔNG tính là thành công: LastOkUtc giữ nguyên nên
                        // StaleSeconds tiếp tục tăng và giám sát thấy được.
                        state.FailCount++;
                        state.LastError = "ACK khong hop le: " + ack;
                    }
                }
                catch (Exception ex)
                {
                    state.Online    = false;
                    state.FailCount++;
                    state.LastError = ex.Message;
                    DropSocket(state.Panel.PanelId);
                }
            }
        }

        private LedSocket SocketFor(LedPanel panel)
        {
            lock (_sync)
            {
                LedSocket s;
                if (!_sockets.TryGetValue(panel.PanelId, out s))
                {
                    s = new LedSocket(panel.IpAddress, panel.Port, _timeoutMs);
                    _sockets[panel.PanelId] = s;
                }
                return s;
            }
        }

        private void DropSocket(int panelId)
        {
            lock (_sync)
            {
                LedSocket s;
                if (_sockets.TryGetValue(panelId, out s))
                {
                    s.Dispose();
                    _sockets.Remove(panelId);
                }
            }
        }

        // Gửi thẳng một khung tuỳ ý — phục vụ nghiệm thu tại hiện trường.
        public string SendRaw(int panelId, LedHub hub)
        {
            LedPanelState state;
            lock (_sync)
            {
                if (!_states.TryGetValue(panelId, out state))
                    throw new InvalidOperationException("Khong co bang LED id " + panelId);
            }

            string frame = hub.GetCommand();
            string ack   = SocketFor(state.Panel).SendAndReceive(frame);
            state.LastFrame = frame;
            state.LastAck   = ack;
            return ack;
        }

        // Xoá mọi bảng về trạng thái trống. Gọi khi tắt có trật tự.
        //
        // Board GIỮ NGUYÊN nội dung cuối cùng vĩnh viễn và không có watchdog.
        // Không xoá thì sau khi ứng dụng dừng, bảng tiếp tục quảng cáo số chỗ
        // trống của lúc nó còn sống — dẫn tài xế vào khu đã đầy, và không có
        // dấu hiệu nào cho thấy con số đó đã chết.
        //
        // Chỉ cứu được trường hợp tắt có trật tự: app pool recycle, deploy,
        // stop service. Mất điện đột ngột thì không có cách nào từ phía phần mềm.
        public void BlankAll()
        {
            foreach (var state in States)
            {
                foreach (var port in state.Panel.Ports.Where(p => p.IsActive))
                {
                    try
                    {
                        SocketFor(state.Panel).SendAndReceive(LedHub.Blank(port.PortIndex).GetCommand());
                    }
                    catch
                    {
                        // Đang tắt — không còn ai để báo lỗi, và thử tiếp bảng
                        // sau vẫn có ích hơn là dừng lại ở bảng đầu tiên hỏng.
                    }
                }
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_timer == null) return;
                _timer.Dispose();
                _timer = null;
            }
        }

        public void Dispose()
        {
            Stop();
            lock (_sync)
            {
                foreach (var s in _sockets.Values) s.Dispose();
                _sockets.Clear();
                _states.Clear();
            }
        }
    }
}
