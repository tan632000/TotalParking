using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TotalParking.Services.Plc
{
    // Client FINS/TCP cho PLC Omron. Chuyển thể từ PlcConnect/Services/OmronFinsClient.cs
    // của dự án PLC-Connect, đã kiểm chứng trên chính CP2E-N60DR-A tại 192.168.0.10.
    //
    // Bản gốc chỉ dùng gói NuGet OmronFinsNetStandard cho ba kiểu dữ liệu (PlcMemory,
    // BitState, FinsError) chứ không dùng để đóng gói khung tin — phần khung tin nó
    // tự viết. Nên ở đây khai báo lại ba kiểu đó trong PlcTypes.cs và bỏ hẳn gói NuGet,
    // thay vì thêm một dependency chỉ để lấy hai enum.
    //
    // BỐN THAY ĐỔI so với bản gốc, đều là lỗi chỉ lộ ra khi chạy lâu và nhiều PLC:
    //
    //   1. Đọc thiếu byte thì NÉM LỖI, không trả về phần đọc dở. Bản gốc bắt
    //      OperationCanceledException rồi trả số byte đã đọc; phần byte còn lại của
    //      khung nằm lại trong stream và làm lệch mọi lần đọc sau. Với ứng dụng bấm
    //      nút thủ công thì hiếm gặp, với vòng poll 500ms thì là chuyện thường ngày.
    //   2. SID tăng dần và được đối chiếu ở phản hồi. Bản gốc để 0x00 cố định nên
    //      không phát hiện được khung tin của lượt trước.
    //   3. Timeout truyền vào từng lệnh, không hard-code 3000ms trong thân hàm.
    //   4. Đọc word trả về ushort[]. Bản gốc trả short[] có dấu, và ghép hai word
    //      thành 32 bit bằng phép dịch trên số âm sẽ sign-extend làm hỏng giá trị —
    //      đúng trường hợp mã thẻ a0d22940 có word cao 0xA0D2.
    //
    // Lớp này KHÔNG tự đồng bộ hoá: một khung FINS là một cặp ghi-rồi-đọc không thể
    // xen kẽ. Việc khoá do PlcConnection lo.
    public class OmronFinsClient : IDisposable
    {
        private const int TcpHeaderLength   = 16;
        private const int HandshakeReqLength = 20;
        private const int HandshakeResLength = 24;

        private TcpClient     _tcpClient;
        private NetworkStream _stream;
        private byte _pcNode;
        private byte _plcNode;
        private byte _sid;

        public bool IsConnected
        {
            get { return _tcpClient != null && _tcpClient.Connected && _stream != null; }
        }

        public byte PcNode  { get { return _pcNode; } }
        public byte PlcNode { get { return _plcNode; } }

        public OmronFinsClient(byte pcNode, byte plcNode)
        {
            _pcNode  = pcNode;
            _plcNode = plcNode;
        }

        // Bắt tay FINS/TCP: gửi 20 byte xin cấp node, nhận 24 byte trả lời.
        // PLC có quyền cấp node khác node ta đề nghị, nên hai giá trị node được
        // ghi đè bằng thứ PLC trả về — đó là lý do plc_node trong DB chỉ là giá trị
        // đề nghị chứ không phải sự thật.
        public async Task ConnectAsync(string ipAddress, int port, int timeoutMs)
        {
            Close();

            _tcpClient = new TcpClient();
            var connectTask = _tcpClient.ConnectAsync(ipAddress, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)).ConfigureAwait(false) != connectTask)
            {
                Close();
                throw new TimeoutException(
                    string.Format("Ket noi TCP toi PLC {0}:{1} qua thoi gian ({2}ms).",
                                  ipAddress, port, timeoutMs));
            }

            // Task đã xong nhưng có thể xong bằng lỗi — await lại để lỗi nổi lên.
            await connectTask.ConfigureAwait(false);

            _stream = _tcpClient.GetStream();

            var req = new byte[HandshakeReqLength];
            req[0] = (byte)'F'; req[1] = (byte)'I'; req[2] = (byte)'N'; req[3] = (byte)'S';
            WriteInt32(req, 4, 12);   // Length = Command 4 + Error 4 + ClientNode 4
            WriteInt32(req, 8, 0);    // Command 0 = Node Address Request
            WriteInt32(req, 12, 0);   // Error code
            WriteInt32(req, 16, _pcNode);

            await _stream.WriteAsync(req, 0, req.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);

            var res = new byte[HandshakeResLength];
            await ReadExactAsync(res, HandshakeResLength, timeoutMs, "bat tay").ConfigureAwait(false);

            uint errorCode = ReadUInt32(res, 12);
            if (errorCode != 0)
            {
                Close();
                throw new FinsFramingException(
                    string.Format("PLC tu choi bat tay FINS/TCP, ma loi 0x{0:X8}.", errorCode));
            }

            byte assignedClient = res[19];
            byte assignedServer = res[23];
            if (assignedClient > 0) _pcNode  = assignedClient;
            if (assignedServer > 0) _plcNode = assignedServer;
        }

        // Đọc `count` word liên tiếp. Trả ushort để phía gọi ghép nhiều word thành
        // số 32 bit mà không dính sign-extend.
        public async Task<ushort[]> ReadWordsAsync(
            PlcMemoryArea area, ushort startAddress, ushort count, int timeoutMs)
        {
            EnsureConnected();

            var frame = new byte[18];
            byte sid = BuildFinsHeader(frame, 0x01, 0x01);
            frame[12] = WordAreaCode(area);
            frame[13] = (byte)(startAddress >> 8);
            frame[14] = (byte)(startAddress & 0xFF);
            frame[15] = 0x00;                       // bit offset, không dùng khi đọc word
            frame[16] = (byte)(count >> 8);
            frame[17] = (byte)(count & 0xFF);

            byte[] body = await SendAndReceiveAsync(frame, sid, timeoutMs).ConfigureAwait(false);

            // Dữ liệu bắt đầu ngay sau End Code: 10 byte header + 2 byte MRC/SRC + 2 byte End Code.
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
            var frame = new byte[18 + count * 2];
            byte sid = BuildFinsHeader(frame, 0x01, 0x02);
            frame[12] = WordAreaCode(area);
            frame[13] = (byte)(startAddress >> 8);
            frame[14] = (byte)(startAddress & 0xFF);
            frame[15] = 0x00;
            frame[16] = (byte)(count >> 8);
            frame[17] = (byte)(count & 0xFF);

            for (int i = 0; i < count; i++)
            {
                int idx = 18 + i * 2;
                frame[idx]     = (byte)(data[i] >> 8);
                frame[idx + 1] = (byte)(data[i] & 0xFF);
            }

            await SendAndReceiveAsync(frame, sid, timeoutMs).ConfigureAwait(false);
        }

        // bitAddress dạng "75.0" — word 75, bit 0.
        public async Task<bool> GetBitStateAsync(PlcMemoryArea area, string bitAddress, int timeoutMs)
        {
            EnsureConnected();

            ushort word;
            byte bit;
            ParseBitAddress(bitAddress, out word, out bit);

            var frame = new byte[18];
            byte sid = BuildFinsHeader(frame, 0x01, 0x01);
            frame[12] = BitAreaCode(area);
            frame[13] = (byte)(word >> 8);
            frame[14] = (byte)(word & 0xFF);
            frame[15] = bit;
            frame[16] = 0x00;
            frame[17] = 0x01;

            byte[] body = await SendAndReceiveAsync(frame, sid, timeoutMs).ConfigureAwait(false);
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
            ParseBitAddress(bitAddress, out word, out bit);

            var frame = new byte[19];
            byte sid = BuildFinsHeader(frame, 0x01, 0x02);
            frame[12] = BitAreaCode(area);
            frame[13] = (byte)(word >> 8);
            frame[14] = (byte)(word & 0xFF);
            frame[15] = bit;
            frame[16] = 0x00;
            frame[17] = 0x01;
            frame[18] = (byte)(state == BitState.On ? 0x01 : 0x00);

            await SendAndReceiveAsync(frame, sid, timeoutMs).ConfigureAwait(false);
        }

        // Gửi một khung và đọc trọn vẹn khung trả lời. Mọi sai lệch khung tin đều
        // ném FinsFramingException để phía trên đóng kết nối — xem PlcTypes.cs.
        private async Task<byte[]> SendAndReceiveAsync(byte[] finsFrame, byte sid, int timeoutMs)
        {
            byte[] packet = BuildTcpPacket(finsFrame);
            await _stream.WriteAsync(packet, 0, packet.Length).ConfigureAwait(false);
            await _stream.FlushAsync().ConfigureAwait(false);

            var header = new byte[TcpHeaderLength];
            await ReadExactAsync(header, TcpHeaderLength, timeoutMs, "TCP header").ConfigureAwait(false);

            if (header[0] != (byte)'F' || header[1] != (byte)'I' ||
                header[2] != (byte)'N' || header[3] != (byte)'S')
            {
                throw new FinsFramingException("Phan hoi khong bat dau bang 'FINS'.");
            }

            uint tcpError = ReadUInt32(header, 12);
            if (tcpError != 0)
            {
                throw new FinsFramingException(
                    string.Format("FINS/TCP header bao loi 0x{0:X8}.", tcpError));
            }

            // Length đếm từ byte thứ 8 của gói, nên phần còn lại = Length - 8.
            long payloadLength = ReadUInt32(header, 4);
            long remaining = payloadLength - 8;
            if (remaining < 14 || remaining > 4096)
            {
                throw new FinsFramingException(
                    string.Format("Do dai phan hoi FINS khong hop le: {0}.", payloadLength));
            }

            var body = new byte[remaining];
            await ReadExactAsync(body, (int)remaining, timeoutMs, "FINS body").ConfigureAwait(false);

            // SID nằm ở byte 9 của khung FINS. Lệch nghĩa là đang đọc phải phản hồi
            // của lượt trước — kết nối đã lệch pha, đóng lại chứ không dùng tiếp.
            if (body[9] != sid)
            {
                throw new FinsFramingException(
                    string.Format("SID phan hoi ({0}) khac SID yeu cau ({1}).", body[9], sid));
            }

            byte mainCode = body[12];
            byte subCode  = body[13];
            if (mainCode != 0 || subCode != 0)
            {
                throw new FinsException(mainCode, subCode,
                    string.Format("PLC tra ve End Code 0x{0:X2}{1:X2}.", mainCode, subCode));
            }

            return body;
        }

        // Trả về SID đã dùng, để đối chiếu khi nhận phản hồi.
        private byte BuildFinsHeader(byte[] frame, byte mrc, byte src)
        {
            frame[0] = 0x80;      // ICF: command, cần phản hồi
            frame[1] = 0x00;      // RSV
            frame[2] = 0x02;      // GCT
            frame[3] = 0x00;      // DNA: mạng nội bộ
            frame[4] = _plcNode;  // DA1
            frame[5] = 0x00;      // DA2: CPU unit
            frame[6] = 0x00;      // SNA
            frame[7] = _pcNode;   // SA1
            frame[8] = 0x00;      // SA2

            // SID bỏ qua 0 để giá trị mặc định của một mảng mới không trùng SID hợp lệ.
            _sid = (byte)(_sid == 255 ? 1 : _sid + 1);
            frame[9] = _sid;

            frame[10] = mrc;
            frame[11] = src;
            return _sid;
        }

        private static byte[] BuildTcpPacket(byte[] finsFrame)
        {
            var packet = new byte[TcpHeaderLength + finsFrame.Length];
            packet[0] = (byte)'F'; packet[1] = (byte)'I';
            packet[2] = (byte)'N'; packet[3] = (byte)'S';
            WriteInt32(packet, 4, 8 + finsFrame.Length);
            WriteInt32(packet, 8, 2);   // Command 2 = FINS Frame Send
            WriteInt32(packet, 12, 0);  // Error code
            Buffer.BlockCopy(finsFrame, 0, packet, TcpHeaderLength, finsFrame.Length);
            return packet;
        }

        // Đọc đủ `count` byte hoặc ném lỗi. Không có đường nào trả về ít hơn:
        // trả về phần đọc dở là cách chắc chắn nhất để làm hỏng kết nối lâu dài.
        private async Task ReadExactAsync(byte[] buffer, int count, int timeoutMs, string what)
        {
            int total = 0;
            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    while (total < count)
                    {
                        int read = await _stream
                            .ReadAsync(buffer, total, count - total, cts.Token)
                            .ConfigureAwait(false);
                        if (read == 0)
                        {
                            throw new FinsFramingException(
                                string.Format("PLC dong ket noi khi dang doc {0} ({1}/{2} byte).",
                                              what, total, count));
                        }
                        total += read;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw new FinsFramingException(
                        string.Format("Qua thoi gian khi doc {0} ({1}/{2} byte sau {3}ms).",
                                      what, total, count, timeoutMs));
                }
                catch (IOException ex)
                {
                    throw new FinsFramingException(
                        string.Format("Loi doc {0}: {1}", what, ex.Message));
                }
            }
        }

        private static void ParseBitAddress(string bitAddress, out ushort word, out byte bit)
        {
            if (string.IsNullOrWhiteSpace(bitAddress))
                throw new ArgumentException("Dia chi bit rong.", "bitAddress");

            string[] parts = bitAddress.Trim().Split('.');
            if (!ushort.TryParse(parts[0], out word))
                throw new ArgumentException("Dia chi bit khong hop le: " + bitAddress, "bitAddress");

            bit = 0;
            if (parts.Length > 1 && !byte.TryParse(parts[1], out bit))
                throw new ArgumentException("Dia chi bit khong hop le: " + bitAddress, "bitAddress");

            if (bit > 15)
                throw new ArgumentException("Chi so bit phai trong 0..15: " + bitAddress, "bitAddress");
        }

        // Mã vùng nhớ khi truy cập theo WORD.
        private static byte WordAreaCode(PlcMemoryArea area)
        {
            switch (area)
            {
                case PlcMemoryArea.DM:  return 0x82;
                case PlcMemoryArea.CIO: return 0xB0;
                case PlcMemoryArea.WR:  return 0xB1;
                case PlcMemoryArea.HR:  return 0xB2;
                case PlcMemoryArea.AR:  return 0xB3;
                default: throw new ArgumentOutOfRangeException("area");
            }
        }

        // Mã vùng nhớ khi truy cập theo BIT — khác hẳn mã word của cùng vùng.
        private static byte BitAreaCode(PlcMemoryArea area)
        {
            switch (area)
            {
                case PlcMemoryArea.DM:  return 0x02;
                case PlcMemoryArea.CIO: return 0x30;
                case PlcMemoryArea.WR:  return 0x31;
                case PlcMemoryArea.HR:  return 0x32;
                case PlcMemoryArea.AR:  return 0x33;
                default: throw new ArgumentOutOfRangeException("area");
            }
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset]     = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static uint ReadUInt32(byte[] buffer, int offset)
        {
            return ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) |
                   ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
        }

        private void EnsureConnected()
        {
            if (!IsConnected) throw new InvalidOperationException("Chua ket noi toi PLC.");
        }

        public void Close()
        {
            try { if (_stream != null) _stream.Close(); } catch { }
            try { if (_tcpClient != null) _tcpClient.Close(); } catch { }
            _stream    = null;
            _tcpClient = null;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
