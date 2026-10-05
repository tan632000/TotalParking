using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Client FINS/UDP cho PLC Omron. Cùng khung lệnh với OmronFinsClient (TCP),
    // khác ở chỗ KHÔNG có phiên.
    //
    // ===================== VÌ SAO CÓ BẢN UDP =====================
    // FINS/TCP mỗi PLC chỉ có 3 khe. Mỗi lần máy chủ restart có thể để lại một
    // phiên bỏ rơi chiếm khe hàng giờ — sáng 04/10 bốn lần restart làm 86/112
    // block báo 0x00000020. UDP không có bắt tay nên không có khe nào để kẹt.
    // Xem docs/cau_hoi_plc_loi_0x20.md và specs/fins-udp/plan.md.
    //
    // ===================== ĐỊA CHỈ NODE SUY TỪ IP =====================
    // Đo trên CP2E tại bãi: DA1 phải là octet cuối IP của PLC, SA1 phải là octet
    // cuối IP máy gửi. plc_node / pc_node trong DB là giá trị cho TCP (PLC tự cấp
    // khi bắt tay) và dùng cho UDP thì bị từ chối (End Code 9005 / 2108).
    //
    // ===================== MỘT LƯỢT NHẬN DUY NHẤT =====================
    // UdpClient.ReceiveAsync trên .NET 4.5 KHÔNG huỷ được. Nếu mỗi lần chờ tạo
    // một lượt nhận mới rồi bỏ đi khi hết giờ, lượt bị bỏ vẫn còn đăng ký với
    // socket và sẽ nhận mất gói kế tiếp — tức phản hồi của lần gửi lại, hoặc của
    // lệnh sau. Nên chỉ giữ một lượt trong _pendingReceive và dùng lại nó cho tới
    // khi nó thật sự có gói.
    //
    // ===================== CHỈ GỬI LẠI LỆNH ĐỌC =====================
    // Gửi lại lệnh đọc là vô hại. Gửi lại lệnh ghi thì không: bit yêu cầu và
    // D1002 cũng do ladder ghi, nên một lệnh "xoá" đến lần hai có thể xoá mất
    // lượt quẹt thẻ mới của khách vừa ghi vào giữa hai lần gửi. Lệnh ghi hỏng thì
    // báo lỗi; PlcConnection đã tự thử lại ở nhịp poll sau.
    //
    // Lớp này KHÔNG tự đồng bộ hoá: PlcConnection._gate bảo đảm mỗi lúc chỉ một
    // lệnh đang bay, giống với client TCP.
    public class OmronFinsUdpClient : IFinsClient
    {
        private const int MinAttemptMs = 500;

        private UdpClient _udp;
        private Task<UdpReceiveResult> _pendingReceive;
        private byte _plcNode;   // DA1
        private byte _pcNode;    // SA1
        private byte _sid;

        public bool IsConnected { get { return _udp != null; } }

        public string Transport { get { return "udp"; } }

        // UDP không có kết nối thật: "Connect" chỉ cố định địa chỉ đích để socket
        // lọc bỏ gói từ máy khác. Vì vậy đọc thử một word để biết PLC có trả lời —
        // nếu bỏ bước này, PlcConnection sẽ báo online cho một PLC đã tắt nguồn
        // cho tới lệnh đầu tiên hỏng.
        public async Task ConnectAsync(string ipAddress, int port, int timeoutMs)
        {
            Close();

            try
            {
                IPAddress plc = IPAddress.Parse(ipAddress);

                // Cổng nguồn tạm do hệ điều hành cấp, KHÔNG bind 9600: PLC trả lời về
                // đúng cổng nguồn của từng gói (đo 05/10, hai socket song song 100/100).
                _udp = new UdpClient(AddressFamily.InterNetwork);
                _udp.Connect(plc, port);

                var local = (IPEndPoint)_udp.Client.LocalEndPoint;
                byte sa1 = local.Address.GetAddressBytes()[3];
                if (sa1 < 1 || sa1 > 254)
                {
                    throw new FinsFramingException(string.Format(
                        "Octet cuoi IP may chu ({0}) khong dung lam node FINS (1..254).", local.Address));
                }

                _pcNode  = sa1;
                _plcNode = plc.GetAddressBytes()[3];

                await ReadWordsAsync(PlcMemoryArea.DM, 0, 1, timeoutMs).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Close();
                // Mọi lỗi ở bước này đều nghĩa là "chưa nói chuyện được với PLC" —
                // kể cả End Code lỗi khi đọc thử, vì D0 luôn đọc được trên CP2E.
                if (ex is FinsFramingException) throw;
                throw new FinsFramingException(string.Format(
                    "Khong noi chuyen duoc voi PLC {0}:{1} qua FINS/UDP: {2}", ipAddress, port, ex.Message));
            }
        }

        public async Task<ushort[]> ReadWordsAsync(
            PlcMemoryArea area, ushort startAddress, ushort count, int timeoutMs)
        {
            EnsureConnected();

            var cmd = new byte[8];
            cmd[0] = 0x01; cmd[1] = 0x01;           // Memory Area Read
            cmd[2] = OmronFinsClient.WordAreaCode(area);
            cmd[3] = (byte)(startAddress >> 8);
            cmd[4] = (byte)(startAddress & 0xFF);
            cmd[5] = 0x00;
            cmd[6] = (byte)(count >> 8);
            cmd[7] = (byte)(count & 0xFF);

            byte[] body = await ExchangeAsync(cmd, true, timeoutMs).ConfigureAwait(false);

            // Bố cục phản hồi giống phần thân FINS của TCP: 10 byte header +
            // 2 byte MRC/SRC + 2 byte End Code, rồi tới dữ liệu.
            const int dataStart = 14;
            int needed = dataStart + count * 2;
            if (body.Length < needed)
            {
                throw new FinsFramingException(
                    string.Format("PLC tra ve {0} byte, can it nhat {1} cho {2} word.",
                                  body.Length, needed, count));
            }

            var result = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                int idx = dataStart + i * 2;
                result[i] = (ushort)((body[idx] << 8) | body[idx + 1]);
            }
            return result;
        }

        public async Task WriteWordsAsync(
            PlcMemoryArea area, ushort startAddress, ushort[] data, int timeoutMs)
        {
            EnsureConnected();
            if (data == null || data.Length == 0) return;

            var count = (ushort)data.Length;
            var cmd = new byte[8 + count * 2];
            cmd[0] = 0x01; cmd[1] = 0x02;           // Memory Area Write
            cmd[2] = OmronFinsClient.WordAreaCode(area);
            cmd[3] = (byte)(startAddress >> 8);
            cmd[4] = (byte)(startAddress & 0xFF);
            cmd[5] = 0x00;
            cmd[6] = (byte)(count >> 8);
            cmd[7] = (byte)(count & 0xFF);
            for (int i = 0; i < count; i++)
            {
                cmd[8 + i * 2]     = (byte)(data[i] >> 8);
                cmd[8 + i * 2 + 1] = (byte)(data[i] & 0xFF);
            }

            await ExchangeAsync(cmd, false, timeoutMs).ConfigureAwait(false);
        }

        public async Task<bool> GetBitStateAsync(PlcMemoryArea area, string bitAddress, int timeoutMs)
        {
            EnsureConnected();

            ushort word;
            byte bit;
            OmronFinsClient.ParseBitAddress(bitAddress, out word, out bit);

            var cmd = new byte[8];
            cmd[0] = 0x01; cmd[1] = 0x01;
            cmd[2] = OmronFinsClient.BitAreaCode(area);
            cmd[3] = (byte)(word >> 8);
            cmd[4] = (byte)(word & 0xFF);
            cmd[5] = bit;
            cmd[6] = 0x00;
            cmd[7] = 0x01;

            byte[] body = await ExchangeAsync(cmd, true, timeoutMs).ConfigureAwait(false);
            if (body.Length < 15)
            {
                throw new FinsFramingException("Phan hoi doc bit thieu byte du lieu.");
            }
            return body[14] != 0;
        }

        public async Task SetBitStateAsync(
            PlcMemoryArea area, string bitAddress, BitState state, int timeoutMs)
        {
            EnsureConnected();

            ushort word;
            byte bit;
            OmronFinsClient.ParseBitAddress(bitAddress, out word, out bit);

            var cmd = new byte[9];
            cmd[0] = 0x01; cmd[1] = 0x02;
            cmd[2] = OmronFinsClient.BitAreaCode(area);
            cmd[3] = (byte)(word >> 8);
            cmd[4] = (byte)(word & 0xFF);
            cmd[5] = bit;
            cmd[6] = 0x00;
            cmd[7] = 0x01;
            cmd[8] = (byte)(state == BitState.On ? 0x01 : 0x00);

            await ExchangeAsync(cmd, false, timeoutMs).ConfigureAwait(false);
        }

        // Gửi một lệnh (MRC/SRC + tham số) và trả về toàn bộ khung phản hồi.
        //
        // resend = true chỉ cho lệnh ĐỌC: tối đa 2 lần gửi, mỗi lần một SID mới và
        // chờ một nửa thời hạn. Lệnh GHI gửi đúng một lần và chờ trọn thời hạn.
        private async Task<byte[]> ExchangeAsync(byte[] command, bool resend, int timeoutMs)
        {
            int attempts  = resend ? 2 : 1;
            int attemptMs = resend ? Math.Max(MinAttemptMs, timeoutMs / 2) : timeoutMs;

            // Lượt nhận còn treo từ lệnh trước có thể đã hỏng (ví dụ ICMP port
            // unreachable tới giữa hai lệnh). Báo lỗi TRƯỚC khi gửi: gửi rồi mới
            // phát hiện thì một lệnh ghi đã tới PLC lại bị ghi nhận là hỏng.
            if (_pendingReceive != null && _pendingReceive.IsFaulted)
            {
                Exception cu = _pendingReceive.Exception.GetBaseException();
                _pendingReceive = null;
                throw new FinsFramingException("Loi nhan FINS/UDP tu truoc: " + cu.Message);
            }

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                byte sid = NextSid();
                byte[] frame = BuildFrame(sid, command);

                try
                {
                    await _udp.SendAsync(frame, frame.Length).ConfigureAwait(false);
                }
                catch (SocketException ex)
                {
                    throw new FinsFramingException("Loi gui FINS/UDP: " + ex.Message);
                }

                byte[] reply = await ReceiveSidAsync(sid, attemptMs).ConfigureAwait(false);
                if (reply != null) return CheckEndCode(reply);
            }

            throw new FinsFramingException(string.Format(
                "PLC khong tra loi qua FINS/UDP sau {0} lan gui ({1}ms moi lan).", attempts, attemptMs));
        }

        // Chờ gói có đúng SID trong `waitMs`. Gói SID khác là phản hồi trễ của
        // lần gửi trước hoặc lệnh trước — bỏ qua và chờ tiếp trong cùng thời hạn.
        // Trả null khi hết giờ; lượt nhận đang chờ được GIỮ LẠI cho lần sau.
        private async Task<byte[]> ReceiveSidAsync(byte sid, int waitMs)
        {
            // Stopwatch chứ không DateTime: đồng hồ hệ thống nhảy (NTP) không được
            // làm co giãn thời gian chờ.
            var dongHo = System.Diagnostics.Stopwatch.StartNew();

            while (true)
            {
                int remaining = waitMs - (int)dongHo.ElapsedMilliseconds;
                if (remaining <= 0) return null;

                if (_pendingReceive == null)
                {
                    // ReceiveAsync trên .NET 4.x gọi BeginReceive ngay, nên một lỗi
                    // ICMP đang nằm trong hàng đợi của socket được ném ĐỒNG BỘ ở đây
                    // (đo: SocketException 10054) chứ không qua task.
                    try
                    {
                        _pendingReceive = _udp.ReceiveAsync();
                    }
                    catch (SocketException ex)
                    {
                        throw new FinsFramingException("Loi nhan FINS/UDP: " + ex.Message);
                    }
                    catch (ObjectDisposedException)
                    {
                        throw new FinsFramingException("Socket FINS/UDP da dong khi dang nhan.");
                    }
                }

                using (var cts = new CancellationTokenSource())
                {
                    Task delay = Task.Delay(remaining, cts.Token);
                    Task first = await Task.WhenAny(_pendingReceive, delay).ConfigureAwait(false);
                    if (first != _pendingReceive) return null;
                    cts.Cancel();
                }

                Task<UdpReceiveResult> done = _pendingReceive;
                _pendingReceive = null;

                byte[] data;
                try
                {
                    data = (await done.ConfigureAwait(false)).Buffer;
                }
                catch (SocketException ex)
                {
                    // Ví dụ 10054: Windows báo ICMP port unreachable trên socket UDP
                    // đã Connect. Không còn tin được đường này nữa.
                    throw new FinsFramingException("Loi nhan FINS/UDP: " + ex.Message);
                }
                catch (ObjectDisposedException)
                {
                    throw new FinsFramingException("Socket FINS/UDP da dong khi dang nhan.");
                }

                if (data.Length < 14)
                {
                    throw new FinsFramingException(
                        string.Format("Phan hoi FINS/UDP qua ngan: {0} byte.", data.Length));
                }

                if (data[9] == sid) return data;
            }
        }

        private static byte[] CheckEndCode(byte[] reply)
        {
            byte mainCode = reply[12];
            byte subCode  = reply[13];
            if (mainCode != 0 || subCode != 0)
            {
                throw new FinsException(mainCode, subCode,
                    string.Format("PLC tra ve End Code 0x{0:X2}{1:X2}.", mainCode, subCode));
            }
            return reply;
        }

        private byte[] BuildFrame(byte sid, byte[] command)
        {
            var frame = new byte[10 + command.Length];
            frame[0] = 0x80;      // ICF: command, cần phản hồi
            frame[1] = 0x00;      // RSV
            frame[2] = 0x02;      // GCT
            frame[3] = 0x00;      // DNA: mạng nội bộ
            frame[4] = _plcNode;  // DA1
            frame[5] = 0x00;      // DA2: CPU unit
            frame[6] = 0x00;      // SNA
            frame[7] = _pcNode;   // SA1
            frame[8] = 0x00;      // SA2
            frame[9] = sid;
            Buffer.BlockCopy(command, 0, frame, 10, command.Length);
            return frame;
        }

        // Bỏ qua 0, giống client TCP.
        private byte NextSid()
        {
            _sid = (byte)(_sid == 255 ? 1 : _sid + 1);
            return _sid;
        }

        private void EnsureConnected()
        {
            if (!IsConnected) throw new InvalidOperationException("Chua ket noi toi PLC.");
        }

        public void Close()
        {
            // Lượt nhận đang chờ sẽ kết thúc bằng lỗi khi socket đóng. Quan sát lỗi
            // đó để nó không thành ngoại lệ "unobserved" trôi nổi.
            var pending = _pendingReceive;
            _pendingReceive = null;
            if (pending != null)
            {
                pending.ContinueWith(t => { var ignored = t.Exception; },
                                     TaskContinuationOptions.OnlyOnFaulted);
            }

            try { if (_udp != null) _udp.Close(); } catch { }
            _udp = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
