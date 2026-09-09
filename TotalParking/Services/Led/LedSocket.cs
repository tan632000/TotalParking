using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TotalParking.Services.Led
{
    // Kết nối TCP tới một bảng LED.
    //
    // CHUYỂN THỂ TỪ led-control/LedSocket.cs. Giữ nguyên phần đã chứng minh là
    // đúng: khoá quanh socket, cách dò socket chết bằng Poll, và thử lại đúng
    // một lần khi gửi hỏng.
    //
    // Bổ sung so với bản gốc — đều là những thiếu sót mà bản gốc tự liệt kê ở
    // system-architecture.md mục 6, và chỉ trở thành vấn đề khi chạy tự động:
    //
    //   1. ĐỌC ACK. Bản gốc không có đường nhận, nên board từ chối khung tin mà
    //      không ai biết. Bấm tay thì nhìn bảng là thấy; chạy nền 24/7 thì không.
    //   2. Timeout lấy từ cấu hình thay vì cố định 3000ms.
    //   3. Port truyền vào constructor thay vì hard-code 2022.
    //
    // Giữ nguyên kiểu Socket đồng bộ của bản gốc thay vì đổi sang async: đó là
    // đúng đường code đã chạy được trên board thật, và với 4 bảng thì không có
    // lý do hiệu năng nào để đánh đổi.
    public class LedSocket : IDisposable
    {
        private readonly string _ip;
        private readonly int    _port;
        private readonly int    _timeoutMs;
        private readonly object _sync = new object();

        private Socket _socket;

        public LedSocket(string ip, int port, int timeoutMs)
        {
            _ip        = ip;
            _port      = port;
            _timeoutMs = timeoutMs;
        }

        public bool IsConnected
        {
            get { lock (_sync) { return IsSocketAlive(_socket); } }
        }

        // Gửi một khung và đọc ACK. Trả về nguyên văn ACK, ví dụ "$PORT,0,OK*2D#".
        // Ném lỗi nếu không gửi được hoặc không nhận được gì.
        public string SendAndReceive(string frame)
        {
            lock (_sync)
            {
                byte[] data = Encoding.ASCII.GetBytes(frame);

                try
                {
                    EnsureConnected();
                    _socket.Send(data);
                }
                catch (SocketException)
                {
                    // Board đóng socket sau 30 giây im lặng. Đóng hẳn rồi mở lại
                    // và gửi lần nữa — đúng cách bản gốc làm, và là thứ giữ cho
                    // ứng dụng chạy được dù chưa có keepalive.
                    CloseSocket();
                    EnsureConnected();
                    _socket.Send(data);
                }

                return Receive();
            }
        }

        private string Receive()
        {
            var buffer = new byte[256];
            int n = _socket.Receive(buffer);
            if (n <= 0)
            {
                throw new IOExceptionLed("Board dong ket noi khi dang cho ACK.");
            }
            return Encoding.ASCII.GetString(buffer, 0, n);
        }

        // ACK đúng dạng: $PORT,<port>,OK*<crc>#
        // Kiểm cả checksum, vì một chuỗi chứa "OK" chưa chắc là ACK hợp lệ.
        public static bool IsAckFor(string ack, int port)
        {
            if (string.IsNullOrEmpty(ack)) return false;

            int s = ack.IndexOf('$');
            int a = ack.IndexOf('*');
            int e = ack.IndexOf('#');
            if (s < 0 || a < s || e < a) return false;

            string payload = ack.Substring(s + 1, a - s - 1);
            string crcText = ack.Substring(a + 1, e - a - 1);

            if (payload != "PORT," + port + ",OK") return false;

            byte expected = LedHub.CheckSum(payload);
            byte actual;
            return byte.TryParse(crcText, System.Globalization.NumberStyles.HexNumber,
                                 System.Globalization.CultureInfo.InvariantCulture, out actual)
                   && actual == expected;
        }

        private void EnsureConnected()
        {
            if (IsSocketAlive(_socket)) return;

            CloseSocket();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                SendTimeout    = _timeoutMs,
                ReceiveTimeout = _timeoutMs
            };
            _socket.Connect(IPAddress.Parse(_ip), _port);
        }

        // Poll(SelectRead) trả true kèm Available == 0 nghĩa là đầu kia đã đóng.
        // Đây là thứ phát hiện được việc board tự ngắt sau 30 giây im lặng.
        private static bool IsSocketAlive(Socket s)
        {
            if (s == null) return false;
            try
            {
                bool closed = s.Poll(1000, SelectMode.SelectRead) && s.Available == 0;
                return s.Connected && !closed;
            }
            catch
            {
                return false;
            }
        }

        private void CloseSocket()
        {
            if (_socket == null) return;
            try { _socket.Shutdown(SocketShutdown.Both); } catch { }
            try { _socket.Close(); } catch { }
            _socket = null;
        }

        public void Dispose()
        {
            lock (_sync) { CloseSocket(); }
        }
    }

    // Tên riêng để không lẫn với System.IO.IOException — lỗi ở đây luôn là lỗi
    // giao tiếp với bảng LED, không phải lỗi đĩa.
    public class IOExceptionLed : Exception
    {
        public IOExceptionLed(string message) : base(message) { }
    }
}
