using System;
using System.Threading;
using System.Threading.Tasks;
using TotalParking.Models;

namespace TotalParking.Services.Plc
{
    // Một kết nối tới một PLC, kèm vòng trao đổi của block đó.
    //
    // Ba việc lớp này giải quyết mà OmronFinsClient không lo:
    //
    //   1. TUẦN TỰ HOÁ. Một khung FINS là một cặp ghi-rồi-đọc không thể xen kẽ.
    //      Vòng poll và luồng ghi trả lời chạy song song trên cùng một socket sẽ
    //      đan khung tin vào nhau và lệch pha vĩnh viễn. Mọi thao tác đi qua
    //      _gate nên tại một thời điểm chỉ có đúng một khung đang bay.
    //
    //   2. KẾT NỐI LẠI. PLC-Connect gốc chỉ đặt IsConnected = false rồi thôi —
    //      hợp lý với ứng dụng có người bấm nút, vô lý với 112 PLC chạy 24/7.
    //      Ở đây mất kết nối là tự thử lại, giãn cách tăng dần để một PLC tắt
    //      nguồn không biến thành vòng lặp quay số liên tục.
    //
    //   3. MỘT PLC HỎNG KHÔNG KÉO CẢ HỆ. Mọi lỗi được nuốt tại đây và chỉ đổi
    //      trạng thái của riêng block này.
    public class PlcConnection : IDisposable
    {
        private const int ReconnectBaseMs = 1000;
        private const int ReconnectMaxMs  = 30000;

        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly CardScanService      _scans    = new CardScanService();
        private readonly PlcRequestRepository _requests = new PlcRequestRepository();

        private OmronFinsClient _client;
        private int      _failStreak;
        private DateTime _nextAttemptUtc = DateTime.MinValue;

        // Chỉ dùng ở chế độ dự phòng khi ladder chưa có bit báo lượt quẹt mới.
        // So sánh theo dãy word THÔ chứ không theo mã đã giải: khi bố cục D100
        // chưa đúng thì mã giải ra là null ở mọi vòng, và so sánh null với null
        // sẽ không chặn được gì — hệ quả là ghi xuống PLC mỗi 500ms.
        private string _lastSeenRaw;

        public PlcDevice Device { get; private set; }

        public bool      IsOnline    { get; private set; }
        public DateTime? LastOkUtc   { get; private set; }
        public string    LastError   { get; private set; }
        public DateTime? LastScanUtc { get; private set; }
        public string    LastCard    { get; private set; }

        // Bố cục mã thẻ đang dùng. Bắt đầu bằng cấu hình trong DB; nếu chưa có thì
        // dò ra từ lượt quẹt đầu tiên khớp được một thẻ có thật.
        public CardCodeLayout? Layout { get; private set; }

        public PlcConnection(PlcDevice device)
        {
            Device = device;
            Layout = ParseLayout(device.CardLayout);
        }

        // Một nhịp poll. Không bao giờ ném lỗi ra ngoài: lỗi của một PLC là việc
        // của riêng nó.
        public async Task PollAsync()
        {
            if (!await _gate.WaitAsync(0).ConfigureAwait(false))
            {
                // Nhịp trước còn đang chạy. Bỏ nhịp này thay vì xếp hàng — hàng đợi
                // chỉ làm độ trễ dồn lên khi PLC chậm.
                return;
            }

            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false)) return;
                await ExchangeAsync().ConfigureAwait(false);
            }
            catch (FinsFramingException ex)
            {
                // Khung tin lệch: byte thừa còn nằm trong stream nên mọi lần đọc
                // sau đều sai. Đóng hẳn, lần poll sau kết nối lại từ đầu.
                Fail(ex.Message, dropConnection: true);
            }
            catch (FinsException ex)
            {
                // PLC trả End Code lỗi: kết nối vẫn tốt, lệnh sai. Giữ kết nối,
                // vì kết nối lại không sửa được địa chỉ thanh ghi sai.
                Fail(ex.Message, dropConnection: false);
            }
            catch (Exception ex)
            {
                Fail(ex.Message, dropConnection: true);
            }
            finally
            {
                _gate.Release();
            }
        }

        // ---------------------------------------------------------------- công cụ
        // Hai hàm dưới đây phục vụ việc nghiệm thu tại hiện trường, không phải
        // đường chạy bình thường. Chúng đi qua ĐÚNG _gate của vòng poll: một khung
        // FINS là cặp ghi-rồi-đọc không thể xen kẽ, nên một lệnh thủ công chen vào
        // giữa nhịp poll sẽ làm lệch khung tin của cả hai.

        // Đọc thô một dải word. Dùng để xác định bố cục D100: quẹt một thẻ đã biết
        // mã rồi đọc D100-D107 và nhìn dãy số.
        public async Task<ushort[]> ReadWordsRawAsync(int startWord, int count)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false))
                    throw new InvalidOperationException("Chua ket noi duoc toi PLC: " + LastError);

                var words = await _client.ReadWordsAsync(
                    PlcMemoryArea.DM, (ushort)startWord, (ushort)count, Device.TimeoutMs)
                    .ConfigureAwait(false);
                MarkOk();
                return words;
            }
            finally
            {
                _gate.Release();
            }
        }

        // Ghi thẳng cặp trả lời xuống PLC, bỏ qua bước tra thẻ. Giữ nguyên thứ tự
        // D402 trước, W75.0 sau — nếu thứ tự này sai thì lúc nghiệm thu cũng phải
        // lộ ra, chứ không phải chỉ đúng ở đường chạy thật.
        public async Task WriteAnswerAsync(int classValue, bool permit)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false))
                    throw new InvalidOperationException("Chua ket noi duoc toi PLC: " + LastError);

                await WriteAnswerCoreAsync(classValue, permit).ConfigureAwait(false);
                MarkOk();
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task<bool> EnsureConnectedAsync()
        {
            if (_client != null && _client.IsConnected) return true;
            if (DateTime.UtcNow < _nextAttemptUtc) return false;

            try
            {
                if (_client != null) _client.Dispose();
                _client = new OmronFinsClient(Device.PcNode, Device.PlcNode);
                await _client.ConnectAsync(Device.IpAddress, Device.Port, Device.TimeoutMs)
                             .ConfigureAwait(false);

                IsOnline    = true;
                LastError   = null;
                _failStreak = 0;
                // Giá trị D100 nhớ từ phiên kết nối trước không còn tin được.
                _lastSeenRaw = null;
                return true;
            }
            catch (Exception ex)
            {
                Fail(ex.Message, dropConnection: true);
                return false;
            }
        }

        private async Task ExchangeAsync()
        {
            // Bước 1 — có lượt quẹt mới không?
            if (Device.HasRequestBit)
            {
                bool pending = await _client.GetBitStateAsync(
                    ParseArea(Device.RequestBitArea), Device.RequestBit, Device.TimeoutMs)
                    .ConfigureAwait(false);

                MarkOk();
                if (!pending) return;
            }

            // Bước 2 — đọc mã thẻ. Chưa chốt bố cục thì đọc dư để còn dò.
            int wordCount = Layout.HasValue
                ? CardCodeDecoder.WordCountFor(Layout.Value)
                : CardCodeDecoder.ProbeWordCount;

            ushort[] words = await _client.ReadWordsAsync(
                PlcMemoryArea.DM, (ushort)Device.CardWord, (ushort)wordCount, Device.TimeoutMs)
                .ConfigureAwait(false);

            MarkOk();

            if (CardCodeDecoder.IsEmpty(words, wordCount))
            {
                // Chưa có thẻ nào được quẹt. Ở chế độ có bit yêu cầu thì đây là
                // bất thường (bit bật mà không có dữ liệu) nhưng vẫn không làm gì
                // hơn được — để nguyên cho lượt sau.
                return;
            }

            string rawHex   = CardCodeDecoder.ToRawHex(words);
            string cardCode = Decode(words);

            // Không có bit yêu cầu: rơi về so sánh giá trị. Hạn chế đã biết —
            // cùng một khách quẹt lại chính thẻ đó thì D100 không đổi và lượt thứ
            // hai bị bỏ qua. Đây là lý do nên có bit yêu cầu ở ladder.
            if (!Device.HasRequestBit)
            {
                if (rawHex == _lastSeenRaw) return;
                // Lần đọc đầu sau khi kết nối luôn được coi là lượt quẹt mới, kể cả
                // khi D100 chỉ đang giữ giá trị cũ từ trước lúc SCADA khởi động.
                // Không tránh được nếu không có bit yêu cầu: ở chế độ này không có
                // cách nào phân biệt "vừa quẹt" với "còn sót lại".
                _lastSeenRaw = rawHex;
            }

            var receivedAt = DateTime.Now;
            LastScanUtc = DateTime.UtcNow;
            LastCard    = cardCode;

            // Bước 3 — tra DB.
            CardScanDecision decision = cardCode == null
                ? CardScanDecision.Deny(RejectReason.UnknownCode)
                : _scans.Evaluate(Device.BlockId, cardCode);

            // Bước 4 và 5 nằm trong try/finally để lượt quẹt LUÔN được ghi nhật ký,
            // kể cả khi lệnh ghi xuống PLC thất bại. Ghi log chỉ ở nhánh thành công
            // là bỏ mất đúng những lượt cần xem nhất: đã đọc được thẻ nhưng không
            // trả lời được HMI.
            //
            // Dấu hiệu phân biệt trong bảng: answered_at IS NULL nghĩa là SCADA đọc
            // được thẻ mà chưa kịp/không thể trả lời.
            DateTime? answeredAt = null;
            try
            {
                // Bước 4 — trả lời.
                await WriteAnswerCoreAsync(decision.WeightClassValue, decision.Permit)
                    .ConfigureAwait(false);
                answeredAt = DateTime.Now;

                // Bước 5 — tắt cờ yêu cầu. Làm SAU khi đã trả lời xong: nếu SCADA
                // chết giữa chừng thì cờ vẫn bật và lượt quẹt này được xử lý lại từ
                // đầu, thay vì mất hẳn.
                if (Device.HasRequestBit)
                {
                    await _client.SetBitStateAsync(
                        ParseArea(Device.RequestBitArea), Device.RequestBit,
                        BitState.Off, Device.TimeoutMs).ConfigureAwait(false);
                }

                MarkOk();
            }
            finally
            {
                SafeLog(rawHex, cardCode, receivedAt, decision, answeredAt);
            }
        }

        // THỨ TỰ HAI LỆNH GHI LÀ BẮT BUỘC: D402 trước, W75.0 sau.
        //
        // W75.0 = 1 là tín hiệu "được phép đi tiếp". Nếu HMI thấy quyền trước khi
        // hạng tải kịp tới nơi, nó đọc phải giá trị D402 của khách trước. Với khách
        // 2600 mà D402 còn 2200 thì HMI mở cả pallet tầng trên, và tầng trên khả
        // năng cao sập.
        //
        // Gom vào một hàm để đường chạy thật và đường nghiệm thu thủ công dùng
        // chung đúng một thứ tự — nếu tách đôi thì chỉ cần một bên sửa là hai bên
        // lệch nhau mà không ai biết.
        private async Task WriteAnswerCoreAsync(int classValue, bool permit)
        {
            await _client.WriteWordsAsync(
                PlcMemoryArea.DM, (ushort)Device.ClassWord,
                new[] { (ushort)classValue }, Device.TimeoutMs)
                .ConfigureAwait(false);

            await _client.SetBitStateAsync(
                ParseArea(Device.PermitBitArea), Device.PermitBit,
                permit ? BitState.On : BitState.Off, Device.TimeoutMs)
                .ConfigureAwait(false);
        }

        private string Decode(ushort[] words)
        {
            if (Layout.HasValue) return CardCodeDecoder.TryDecode(words, Layout.Value);

            // Chưa chốt bố cục: thử lần lượt và lấy bố cục nào cho ra mã CÓ THẬT
            // trong bảng thẻ. Bố cục dò được chỉ nằm trong bộ nhớ và hiện ở
            // /PlcStatus; chốt lại là việc của người vận hành, bằng một câu
            //     UPDATE plc_device SET card_layout = '...', card_word_len = n
            // Cố ý không tự ghi: để chế độ dò chạy vĩnh viễn thì một bố cục sai
            // vẫn có xác suất nhỏ cho ra mã trùng thẻ khác, và khi đó hệ thống
            // mở nhầm xe của người khác.
            var cards = new ParkingCardRepository();
            foreach (var candidate in CardCodeDecoder.ProbeOrder)
            {
                string code = CardCodeDecoder.TryDecode(words, candidate);
                if (code == null) continue;

                try
                {
                    if (cards.FindByCardCode(code) != null)
                    {
                        Layout = candidate;
                        return code;
                    }
                }
                catch
                {
                    // Không tra được thẻ thì cũng không dò được bố cục. Bỏ qua,
                    // lượt sau thử lại.
                    return null;
                }
            }
            return null;
        }

        // Ghi nhật ký không được phép làm hỏng vòng trao đổi: HMI đã nhận câu trả
        // lời rồi, mất một dòng log không đáng để ném lỗi và kéo theo reconnect.
        private void SafeLog(string rawHex, string cardCode, DateTime receivedAt,
                             CardScanDecision decision, DateTime? answeredAt)
        {
            try
            {
                _requests.Log(Device.BlockId, rawHex, cardCode, receivedAt, decision, answeredAt);
            }
            catch { }
        }

        private void MarkOk()
        {
            IsOnline    = true;
            LastOkUtc   = DateTime.UtcNow;
            LastError   = null;
            _failStreak = 0;
        }

        private void Fail(string message, bool dropConnection)
        {
            IsOnline  = false;
            LastError = message;

            if (dropConnection && _client != null)
            {
                _client.Close();
            }

            _failStreak++;
            int delay = ReconnectBaseMs * (1 << Math.Min(_failStreak - 1, 5));
            _nextAttemptUtc = DateTime.UtcNow.AddMilliseconds(Math.Min(delay, ReconnectMaxMs));
        }

        private static PlcMemoryArea ParseArea(string area)
        {
            if (string.IsNullOrWhiteSpace(area)) return PlcMemoryArea.WR;
            switch (area.Trim().ToUpperInvariant())
            {
                case "DM":  return PlcMemoryArea.DM;
                case "CIO": return PlcMemoryArea.CIO;
                case "HR":  return PlcMemoryArea.HR;
                case "AR":  return PlcMemoryArea.AR;
                default:    return PlcMemoryArea.WR;
            }
        }

        private static CardCodeLayout? ParseLayout(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            CardCodeLayout parsed;
            return Enum.TryParse(name.Trim(), true, out parsed) ? parsed : (CardCodeLayout?)null;
        }

        public void Dispose()
        {
            if (_client != null) _client.Dispose();
            _gate.Dispose();
        }
    }
}
