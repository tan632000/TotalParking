using System;
using System.Net.Sockets;
using System.Threading;

namespace TotalParking.Services.Pgs
{
    // Một kết nối tới một ZCU. Chỉ ĐỌC — không gửi byte nào xuống thiết bị.
    //
    // ===================== VÌ SAO KHÔNG GỬI GÌ =====================
    // ZCU tự đóng socket sau đúng 60 giây, đo được ở cả năm thiết bị. Cách chống
    // lại thông thường là gửi gói giữ nhịp, nhưng đây là thiết bị điều khiển và
    // KHÔNG có tài liệu nào mô tả lệnh hợp lệ — gửi bừa một chuỗi byte có thể bị
    // hiểu thành lệnh cấu hình.
    //
    // Thay vào đó: nối lại ngay. Mỗi khung mang trạng thái đầy đủ của toàn bộ
    // cảm biến chứ không phải sai phân, nên khe hở một giây không mất dữ liệu.
    //
    // ===================== CHỐNG RUNG =====================
    // ZCU 2 sinh 4.121 lần đổi trong một ngày, có lúc hai lần trong một giây, cả
    // lúc nửa đêm không ai ra vào — cảm biến hỏng chứ không phải xe. Nối thẳng
    // lên bảng LED thì số sẽ nhảy loạn.
    //
    // Nên một khung chỉ được CHẤP NHẬN khi nội dung giữ nguyên đủ lâu. Trạng
    // thái đang chờ ổn định không được tính vào kết quả.
    public class PgsConnection : IDisposable
    {
        private readonly string _host;
        private readonly int    _port;
        private readonly int    _debounceMs;

        private TcpClient    _client;
        private NetworkStream _stream;
        private byte[]       _buf = new byte[8192];
        private int          _len;

        // Khung đã ổn định — thứ duy nhất phía ngoài được đọc.
        private PgsFrame _stable;
        // Khung đang chờ đủ thời gian ổn định.
        private PgsFrame _pending;
        private DateTime _pendingSince;

        public string   Host          { get { return _host; } }
        public int      ZcuId         { get; private set; }
        public bool     IsOnline      { get; private set; }
        public string   LastError     { get; private set; }
        public DateTime? LastFrameUtc { get; private set; }
        public long     FrameCount    { get; private set; }
        public long     ChangeCount   { get; private set; }
        public long     RejectedNoise { get; private set; }

        public PgsFrame Stable { get { return _stable; } }

        public PgsConnection(string host, int port, int debounceMs)
        {
            _host       = host;
            _port       = port;
            _debounceMs = Math.Max(0, debounceMs);
            ZcuId       = -1;
        }

        // ===================== GIÃN NHỊP KHI THIẾT BỊ IM TIẾNG =====================
        // Đo được ngày 18/09: 192.169.1.75 (CCU) nhận TCP nhưng không đẩy gói nào.
        // Vòng đọc cứ 4 giây lại nối rồi đóng, mà CCU không hoàn tất bắt tay đóng
        // nên socket kẹt ở FinWait2 — tích tụ tới 33 cái chỉ ở riêng địa chỉ đó.
        //
        // Nên: thiết bị nối được mà KHÔNG gửi khung nào thì giãn dần thời gian chờ
        // trước khi thử lại. Một thiết bị im tiếng không được phép ngốn một socket
        // mỗi vài giây.
        private bool     _gotFrameThisSession;
        private int      _silentStreak;
        private DateTime _nextTryUtc = DateTime.MinValue;
        private const int BackoffBaseMs = 5000;
        private const int BackoffMaxMs  = 120000;

        public void Poll(int readTimeoutMs)
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    if (DateTime.UtcNow < _nextTryUtc) return;
                    Connect(readTimeoutMs);
                    if (_client == null) return;
                }

                int n = _stream.Read(_buf, _len, _buf.Length - _len);
                if (n <= 0)
                {
                    // ZCU đóng socket theo chu kỳ 60 giây — chuyện bình thường,
                    // không phải lỗi. Đóng gọn rồi nhịp sau nối lại.
                    Drop(null);
                    return;
                }
                _len += n;
                Extract();

                // Giữ đệm gọn: dữ liệu cũ không dùng tới nữa vì mỗi khung là
                // trạng thái đầy đủ.
                if (_len > _buf.Length - PgsFrame.Length)
                {
                    int keep = Math.Min(_len, PgsFrame.Length * 2);
                    Array.Copy(_buf, _len - keep, _buf, 0, keep);
                    _len = keep;
                }
            }
            catch (System.IO.IOException) { Drop(null); }   // hết thời gian chờ đọc
            catch (Exception ex) { Drop(ex.Message); }
        }

        private void Connect(int timeoutMs)
        {
            try
            {
                _client = new TcpClient();
                var ar = _client.BeginConnect(_host, _port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    _client.Close(); _client = null;
                    Fail("het thoi gian ket noi");
                    return;
                }
                _client.EndConnect(ar);
                _client.ReceiveTimeout = timeoutMs;
                _stream = _client.GetStream();
                _len = 0;
                _gotFrameThisSession = false;
                IsOnline = true;
                LastError = null;
            }
            catch (Exception ex)
            {
                if (_client != null) { _client.Close(); _client = null; }
                Fail(ex.Message);
            }
        }

        private void Extract()
        {
            int i = 0;
            while (i + PgsFrame.Length <= _len)
            {
                if (_buf[i] != PgsFrame.Start) { i++; continue; }

                PgsFrame f;
                if (!PgsFrame.TryParse(_buf, i, out f)) { i++; continue; }

                ZcuId = f.ZcuId;
                FrameCount++;
                _gotFrameThisSession = true;
                LastFrameUtc = DateTime.UtcNow;
                Accept(f);
                i += PgsFrame.Length;
            }
            if (i > 0)
            {
                Array.Copy(_buf, i, _buf, 0, _len - i);
                _len -= i;
            }
        }

        // Lọc chống rung: chỉ nhận khung mới khi nội dung giữ nguyên đủ lâu.
        private void Accept(PgsFrame f)
        {
            if (_stable == null)
            {
                _stable = f;                 // khung đầu tiên nhận luôn
                return;
            }

            if (f.BitsChangedFrom(_stable) == 0)
            {
                _pending = null;             // trùng khung ổn định -> không có gì đổi
                return;
            }

            if (_pending == null || f.BitsChangedFrom(_pending) != 0)
            {
                // Nội dung vừa đổi, hoặc đổi tiếp khi đang chờ -> đếm là nhiễu và
                // bắt đầu đếm giờ lại từ đầu.
                if (_pending != null) RejectedNoise++;
                _pending = f;
                _pendingSince = DateTime.UtcNow;
                return;
            }

            if ((DateTime.UtcNow - _pendingSince).TotalMilliseconds >= _debounceMs)
            {
                _stable = _pending;
                _pending = null;
                ChangeCount++;
            }
        }

        private void Fail(string msg)
        {
            IsOnline = false;
            LastError = msg;
        }

        private void Drop(string msg)
        {
            // Phiên này có nhận được khung nào không. Có thì coi là thiết bị lành
            // và xoá chuỗi im lặng; không thì giãn nhịp thử lại.
            if (_gotFrameThisSession)
            {
                _silentStreak = 0;
                _nextTryUtc = DateTime.MinValue;
            }
            else
            {
                _silentStreak++;
                int wait = BackoffBaseMs;
                for (int i = 1; i < _silentStreak && wait < BackoffMaxMs; i++) wait *= 2;
                _nextTryUtc = DateTime.UtcNow.AddMilliseconds(Math.Min(wait, BackoffMaxMs));
            }
            _gotFrameThisSession = false;

            // Đóng THẲNG bằng Linger 0: gửi RST thay vì FIN. Thiết bị không hoàn
            // tất bắt tay đóng thì socket sẽ nằm mãi ở FinWait2; RST dứt điểm ngay.
            try
            {
                if (_client != null && _client.Client != null)
                    _client.Client.LingerState = new LingerOption(true, 0);
            }
            catch (Exception) { }

            try { if (_stream != null) _stream.Close(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }
            _stream = null;
            _client = null;
            _len = 0;
            if (msg != null) Fail(msg);
        }

        public void Dispose() { Drop(null); }
    }
}
