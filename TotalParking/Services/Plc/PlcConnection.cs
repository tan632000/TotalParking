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
        // CardScanService thuoc hop dong cu (quyet dinh permit/hang tai). Luong moi
        // chi can tra "xe dang o block nao", nen dung CarLocatorService.
        private readonly CarLocatorService    _locator  = new CarLocatorService();
        private readonly PlcRequestRepository _requests = new PlcRequestRepository();
        private readonly WeightBandService    _bands    = new WeightBandService();

        // Gia tri da ghi xuong D1004 lan gan nhat. -1 = chua ghi lan nao, nen nhip
        // poll dau tien luon ghi mot lan — vua de dong bo, vua don gia tri con sot
        // tu lan chay truoc, cung ly do voi ClearStaleAnswerAsync.
        //
        // Khong co bien nay thi moi nhip poll lai ghi lai cung mot con so: 55 block
        // x 2 lenh/giay la luu luong ghi thuong truc xuong thiet bi that.
        private int _lastBandWritten = -1;

        private OmronFinsClient _client;
        private int      _failStreak;
        private DateTime _nextAttemptUtc = DateTime.MinValue;

        // Chỉ dùng ở chế độ dự phòng khi ladder chưa có bit báo lượt quẹt mới.
        // So sánh theo dãy word THÔ chứ không theo mã đã giải: khi bố cục D100
        // chưa đúng thì mã giải ra là null ở mọi vòng, và so sánh null với null
        // sẽ không chặn được gì — hệ quả là ghi xuống PLC mỗi 500ms.
        private string _lastSeenRaw;

        // Thoi diem ghi D1000 mot gia tri KHAC 0. null = dang khong co cau tra loi
        // nao cho. Dung de tu dat lai ve 0 sau ResetAfter.
        private DateTime? _answerAtUtc;

        // Sau bao lau thi xoa cau tra loi o D1000 ve 0.
        //
        // Cau tra loi tim xe la thong tin NHAT THOI: tai xe doc so block roi di.
        // De nguyen thi lan quet sau cua nguoi khac se thay so block cua nguoi
        // truoc neu vi ly do nao do SCADA chua kip ghi de.
        // Co xoa D1002 sau khi da tra loi khong.
        //
        // Day la cach TIEU THU YEU CAU: doc xong thi xoa, de luot quet sau duoc
        // nhan ra la yeu cau MOI chu khong phai gia tri con sot.
        //
        // Khong co no thi: khach quet lai DUNG the do se khong duoc tra loi, vi
        // D1002 khong doi va he thong bo qua de tranh ghi de moi nhip. Truoc khi
        // co auto-reset D1000 thi dieu do vo hai (dap an nam mai); gio thi thanh
        // han che that.
        //
        // LUU Y: day la lan dau SCADA GHI vao D1002 — truoc gio chi doc. Tat duoc
        // bang plc:clearFindCardAfterAnswer = false neu ladder khong chap nhan.
        private static bool ClearFindCardAfterAnswer
        {
            get
            {
                string v = System.Configuration.ConfigurationManager
                               .AppSettings["plc:clearFindCardAfterAnswer"];
                bool b;
                return !bool.TryParse(v, out b) || b;   // mac dinh BAT
            }
        }

        private static TimeSpan ResetAfter
        {
            get
            {
                int ms;
                if (!int.TryParse(
                        System.Configuration.ConfigurationManager.AppSettings["plc:findAnswerResetMs"],
                        out ms) || ms < 1000)
                    ms = 30000;
                return TimeSpan.FromMilliseconds(ms);
            }
        }

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

        // Ghi thẳng số block xuống D1000, bỏ qua bước tra thẻ. Dùng để nghiệm thu
        // tại hiện trường: xác nhận HMI của block nào phản ứng.
        public async Task WriteFindAnswerAsync(int blockNo)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false))
                    throw new InvalidOperationException("Chua ket noi duoc toi PLC: " + LastError);

                await WriteFindAnswerCoreAsync(blockNo).ConfigureAwait(false);
                MarkOk();
            }
            finally
            {
                _gate.Release();
            }
        }

        // Xoá D1002 về 0 sau khi đã trả lời, để lượt quẹt sau được nhận là mới.
        //
        // Đặt _lastSeenRaw = null luôn: giá trị cũ không còn ý nghĩa so sánh khi
        // thanh ghi đã bị xoá. Nếu giữ lại thì lượt quẹt cùng thẻ ngay sau đó vẫn
        // bị bỏ qua — đúng cái lỗi mà việc xoá này sinh ra để chữa.
        private async Task ClearFindCardAsync()
        {
            int len = Device.FindCardLen > 0 ? Device.FindCardLen : 2;
            var zeros = new ushort[len];

            await _client.WriteWordsAsync(
                PlcMemoryArea.DM, (ushort)Device.FindCardWord, zeros, Device.TimeoutMs)
                .ConfigureAwait(false);

            _lastSeenRaw = null;
            PlcAuditLog.Write(Device.IpAddress, Device.BlockNo,
                              "D" + Device.FindCardWord, 0, true,
                              "xoa yeu cau sau khi tra loi (" + len + " word)");
        }

        // Xoá D1000 về 0 khi câu trả lời đã quá hạn.
        //
        // Ghi chú cũ ở đây nói SCADA KHÔNG tự xoá D1000 vì ladder mới là bên quyết
        // định hiển thị bao lâu. Yêu cầu vận hành đã chốt khác: tự xoá sau 30 giây.
        // Lý do chấp nhận được — câu trả lời tìm xe là thông tin nhất thời, để nó
        // nằm mãi thì lượt quẹt sau của người khác có thể đọc phải số block của
        // người trước nếu SCADA chưa kịp ghi đè.
        private async Task ResetAnswerIfDueAsync()
        {
            if (!_answerAtUtc.HasValue) return;
            if (DateTime.UtcNow - _answerAtUtc.Value < ResetAfter) return;

            try
            {
                await WriteFindAnswerCoreAsync(CarLocatorService.NotFound).ConfigureAwait(false);
                // WriteFindAnswerCoreAsync đã tự đặt _answerAtUtc = null vì ghi 0.
            }
            catch (Exception ex)
            {
                // Xoá không được thì thôi, lượt poll sau thử lại. KHÔNG xoá
                // _answerAtUtc: giữ nguyên để còn thử tiếp, nếu không thì câu trả
                // lời cũ nằm lại vĩnh viễn mà không ai biết.
                PlcAuditLog.Error(Device.IpAddress, Device.BlockNo,
                                  "XOA D" + Device.FindAnswerWord, ex.Message);
            }
        }

        // Dọn câu trả lời còn sót lúc khởi động.
        //
        // _answerAtUtc nằm trong bộ nhớ, nên nếu ứng dụng khởi động lại (build,
        // IIS recycle, mất điện) trong khoảng 30 giây chờ reset thì hẹn giờ mất
        // hẳn — và D1000 giữ giá trị cũ vĩnh viễn cho tới lượt quẹt tiếp theo ở
        // chính block đó. Tài xế sau đọc phải số block của người trước.
        //
        // Giả định: khởi động lại thì không có lượt tìm xe nào đang dở. Hợp lý,
        // vì khởi động lại vốn đã làm mất mọi trạng thái đang xử lý.
        //
        // ĐỌC TRƯỚC, chỉ ghi khi khác 0: 112 PLC mà ghi mù cả loạt là 112 lệnh ghi
        // thừa xuống thiết bị mỗi lần app recycle.
        public async Task<bool> ClearStaleAnswerAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false)) return false;

                var w = await _client.ReadWordsAsync(
                    PlcMemoryArea.DM, (ushort)Device.FindAnswerWord, 1, Device.TimeoutMs)
                    .ConfigureAwait(false);

                if (w == null || w.Length == 0 || w[0] == 0) return false;

                await _client.WriteWordsAsync(
                    PlcMemoryArea.DM, (ushort)Device.FindAnswerWord,
                    new ushort[] { 0 }, Device.TimeoutMs).ConfigureAwait(false);

                _answerAtUtc = null;
                MarkOk();
                PlcAuditLog.Write(Device.IpAddress, Device.BlockNo,
                                  "D" + Device.FindAnswerWord, 0, true,
                                  "don luc khoi dong, gia tri cu = " + w[0]);
                return true;
            }
            catch (Exception ex)
            {
                PlcAuditLog.Error(Device.IpAddress, Device.BlockNo,
                                  "DON D" + Device.FindAnswerWord, ex.Message);
                return false;
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

                if (_failStreak > 0)
                {
                    PlcAuditLog.Recovered(Device.IpAddress, Device.BlockNo, "KET NOI");
                    PlcAuditLog.Recovered(Device.IpAddress, Device.BlockNo, "QUET O");
                }

                IsOnline    = true;
                LastError   = null;
                _failStreak = 0;
                // Giá trị D100 nhớ từ phiên kết nối trước không còn tin được.
                _lastSeenRaw = null;
                return true;
            }
            catch (Exception ex)
            {
                PlcAuditLog.Error(Device.IpAddress, Device.BlockNo, "KET NOI", ex.Message);
                Fail(ex.Message, dropConnection: true);
                return false;
            }
        }

        // Một nhịp trao đổi: đọc D1002, tra xe, ghi số block xuống D1000.
        //
        // HỢP ĐỒNG MỚI (khách xác nhận), thay cho D100 / D402 / W75.0:
        //   D1002  đọc   mã thẻ khách vừa quẹt để TÌM XE
        //   D1000  ghi   số block nơi xe đó đang thực sự đỗ, 0 = không tìm thấy
        //
        // Nguồn tra cứu là plc_slot_state — chính các PLC đã báo ô nào giữ thẻ nào
        // khi gửi xe thành công. Không dùng parking_session: bảng đó ghi ý định của
        // SCADA, còn thanh ghi ô ghi sự thật của thiết bị.
        private async Task ExchangeAsync()
        {
            // Bước 0 — câu trả lời cũ đã hết hạn thì xoá về 0.
            //
            // Làm TRƯỚC mọi thứ khác, và không phụ thuộc D1002 còn giữ thẻ hay
            // không: đây là hẹn giờ tính từ lúc GHI, đúng như yêu cầu vận hành.
            await ResetAnswerIfDueAsync().ConfigureAwait(false);

            // Bước 0b — băng tải trọng: đọc D106, trả 1/2/3 xuống D1004.
            //
            // Làm ở đây, TRƯỚC bước kiểm tra bit yêu cầu, là có chủ ý: D106 được
            // ghi ở MỌI lượt quẹt thẻ — gửi xe lẫn tìm xe — còn bit yêu cầu và
            // D1002 chỉ nói về lượt tìm xe. Đặt sau bước 1 thì lượt gửi xe sẽ bị
            // `return` sớm bỏ qua, và D1004 không bao giờ được cập nhật.
            await UpdateWeightBandAsync().ConfigureAwait(false);

            // Bước 1 — có lượt quẹt mới không?
            if (Device.HasRequestBit)
            {
                bool pending = await _client.GetBitStateAsync(
                    ParseArea(Device.RequestBitArea), Device.RequestBit, Device.TimeoutMs)
                    .ConfigureAwait(false);

                MarkOk();
                if (!pending) return;
            }

            // Bước 2 — đọc mã thẻ ở D1002.
            int wordCount = Device.FindCardLen > 0 ? Device.FindCardLen : 2;

            ushort[] words = await _client.ReadWordsAsync(
                PlcMemoryArea.DM, (ushort)Device.FindCardWord, (ushort)wordCount, Device.TimeoutMs)
                .ConfigureAwait(false);

            MarkOk();

            // Theo dõi D1002 TRƯỚC nhánh rỗng: lúc ladder xoá thẻ khỏi thanh ghi
            // cũng là một thay đổi đáng ghi — nếu chỉ ghi ở nhánh có thẻ thì nhật
            // ký chỉ thấy lúc thẻ xuất hiện, không bao giờ thấy lúc nó biến mất.
            PlcRegisterLog.Track(Device.IpAddress, Device.BlockNo,
                                 "D" + Device.FindCardWord,
                                 CardCodeDecoder.ToRawHex(words),
                                 CardCodeDecoder.IsEmpty(words, wordCount)
                                     ? "khong co yeu cau tim xe"
                                     : ("ma the: " + (Decode(words) ?? "khong giai ma duoc")));

            if (CardCodeDecoder.IsEmpty(words, wordCount))
            {
                // Không có ai đang tìm xe ở block này.
                //
                // Không xoá D1000 ở đây. Việc xoá do hẹn giờ ở Bước 0 lo — tính
                // từ lúc GHI, không phải từ lúc D1002 trống. Xoá ngay khi D1002
                // trống sẽ cắt mất câu trả lời trước mắt người đang đọc nó, vì
                // ladder có thể xoá D1002 ngay sau khi SCADA vừa trả lời.
                _lastSeenRaw = null;
                return;
            }

            string rawHex   = CardCodeDecoder.ToRawHex(words);
            string cardCode = Decode(words);

            // Chi ghi nhat ky khi D1002 DOI gia tri.
            //
            // Ban dau ghi moi lan doc thay khac 0 -> mot the nam nguyen trong thanh
            // ghi sinh ra 2 dong/giay, va 30 dong dau tien cua nhat ky deu la cung
            // mot lan quet. Da thay dung hien tuong do luc test block 96.
            if (rawHex != _lastSeenRaw)
            {
                PlcAuditLog.Read(Device.IpAddress, Device.BlockNo,
                                 "D" + Device.FindCardWord, rawHex,
                                 "ma the: " + (cardCode ?? "khong giai ma duoc"));
            }

            // Không có bit yêu cầu: rơi về so sánh giá trị, nếu không thì mỗi nhịp
            // poll lại ghi đè D1000 cùng một giá trị. Hạn chế đã biết — cùng một
            // khách quẹt lại chính thẻ đó thì D1002 không đổi và lượt thứ hai bị bỏ
            // qua. Đây là lý do nên có bit yêu cầu ở ladder.
            if (!Device.HasRequestBit)
            {
                if (rawHex == _lastSeenRaw) return;
                _lastSeenRaw = rawHex;
            }

            var receivedAt = DateTime.Now;
            LastScanUtc = DateTime.UtcNow;
            LastCard    = cardCode;

            // Bước 3 — tra xem xe đang đỗ ở block nào.
            //
            // Không tìm thấy trả về 0. Quan trọng là KHÔNG để nguyên giá trị cũ:
            // khách quẹt thẻ lạ sẽ thấy số block của người trước đó và đi tới block
            // không có xe mình.
            int blockNo = cardCode == null
                ? CarLocatorService.NotFound
                : _locator.FindBlockNo(cardCode);

            // Bước 4 và 5 trong try/finally để lượt quẹt LUÔN được ghi nhật ký, kể
            // cả khi lệnh ghi xuống PLC thất bại. Ghi log chỉ ở nhánh thành công là
            // bỏ mất đúng những lượt cần xem nhất: đã đọc được thẻ mà không trả lời
            // được HMI. Dấu hiệu trong bảng: answered_at IS NULL.
            DateTime? answeredAt = null;
            try
            {
                // Bước 4 — trả lời: ghi số block xuống D1000.
                await WriteFindAnswerCoreAsync(blockNo).ConfigureAwait(false);
                answeredAt = DateTime.Now;

                // Bước 5 — tiêu thụ yêu cầu. Làm SAU khi đã trả lời: nếu SCADA chết
                // giữa chừng thì yêu cầu vẫn còn và được xử lý lại từ đầu, thay vì
                // mất hẳn.
                if (Device.HasRequestBit)
                {
                    await _client.SetBitStateAsync(
                        ParseArea(Device.RequestBitArea), Device.RequestBit,
                        BitState.Off, Device.TimeoutMs).ConfigureAwait(false);
                }
                else if (ClearFindCardAfterAnswer)
                {
                    // Không có bit báo lượt quẹt -> xoá chính D1002.
                    await ClearFindCardAsync().ConfigureAwait(false);
                }

                MarkOk();
            }
            finally
            {
                SafeLogFind(rawHex, cardCode, receivedAt, blockNo, answeredAt);
            }
        }

        // Đọc mã thẻ ở D106, tra hạng tải, ghi băng 1/2/3 xuống D1004.
        //
        // ======================= HỢP ĐỒNG =======================
        //   D106 trống          -> D1004 = 0
        //   D106 có thẻ đã biết -> D1004 = 1 (<2200) / 2 (2200-2600) / 3 (>2600)
        //   D106 có thẻ lạ      -> D1004 = 0
        //
        // Thẻ lạ và không có thẻ cùng ra 0 là cố ý: cả hai đều là "không đủ thông
        // tin". Xem khối chú thích trong WeightBandService.
        //
        // ======================= CHỈ GHI KHI ĐỔI =======================
        // Vòng poll chạy mỗi 500ms. Ghi mù mỗi nhịp là 2 lệnh ghi/giây/block,
        // nhân 55 block — tải ghi thường trực xuống thiết bị thật mà không đổi gì.
        // Nên so với giá trị đã ghi lần trước và chỉ ghi khi khác.
        //
        // ======================= NUỐT LỖI =======================
        // Băng tải trọng là thông tin phụ trợ. Đọc/ghi hỏng thì ghi nhật ký rồi
        // đi tiếp, KHÔNG được ném lên trên — ném sẽ kéo sập cả lượt tìm xe của
        // block này, tức làm hỏng chức năng đang chạy tốt vì một chức năng mới.
        private async Task UpdateWeightBandAsync()
        {
            try
            {
                int len = Device.ScanCardLen > 0 ? Device.ScanCardLen : 2;

                ushort[] words = await _client.ReadWordsAsync(
                    PlcMemoryArea.DM, (ushort)Device.ScanCardWord, (ushort)len, Device.TimeoutMs)
                    .ConfigureAwait(false);

                MarkOk();

                bool empty = CardCodeDecoder.IsEmpty(words, len);
                string card = empty ? null : Decode(words);

                // Ghi nhận mọi thay đổi của D106, kể cả khi băng không đổi: hai mã
                // thẻ khác nhau cùng hạng tải cho ra cùng một băng, nhưng đó vẫn là
                // hai lượt quẹt khác nhau và nhật ký phải thấy được.
                PlcRegisterLog.Track(Device.IpAddress, Device.BlockNo,
                                     "D" + Device.ScanCardWord,
                                     CardCodeDecoder.ToRawHex(words),
                                     empty ? "khong co the" : ("ma the: " + (card ?? "khong giai ma duoc")));

                int band = empty ? WeightBandService.Unknown : _bands.BandFor(card);

                if (band == _lastBandWritten) return;

                await _client.WriteWordsAsync(
                    PlcMemoryArea.DM, (ushort)Device.WeightBandWord,
                    new[] { (ushort)band }, Device.TimeoutMs).ConfigureAwait(false);

                _lastBandWritten = band;
                MarkOk();

                PlcRegisterLog.Track(Device.IpAddress, Device.BlockNo,
                                     "D" + Device.WeightBandWord,
                                     band.ToString(), "bang tai trong SCADA ghi xuong");

                PlcAuditLog.Write(Device.IpAddress, Device.BlockNo,
                                  "D" + Device.WeightBandWord, band, true,
                                  "bang tai trong tu D" + Device.ScanCardWord
                                  + " = " + CardCodeDecoder.ToRawHex(words));
            }
            catch (Exception ex)
            {
                // Ghi lại _lastBandWritten về -1 để lượt sau ghi lại từ đầu: nếu
                // không, một lệnh ghi hỏng sẽ bị nhớ nhầm là đã ghi thành công.
                _lastBandWritten = -1;
                PlcAuditLog.Error(Device.IpAddress, Device.BlockNo,
                                  "BANG TAI D" + Device.WeightBandWord, ex.Message);
            }
        }

        // Ghi SO BLOCK noi xe dang dau xuong D1000.
        //
        // Thay cho cap D402 + W75.0 cua hop dong cu. Khac biet ve ban chat: W75.0
        // chi tra duoc dung/sai (1 bit), con day tra ve so block — tra loi duoc
        // cau "xe dang o dau" chu khong chi "co phai o day khong".
        //
        // Mot lenh ghi duy nhat nen khong con van de thu tu nhu D402-truoc-W75.0.
        private async Task WriteFindAnswerCoreAsync(int blockNo)
        {
            if (blockNo < 0) blockNo = 0;
            if (blockNo > ushort.MaxValue) blockNo = ushort.MaxValue;

            string reg = "D" + Device.FindAnswerWord;
            try
            {
                await _client.WriteWordsAsync(
                    PlcMemoryArea.DM, (ushort)Device.FindAnswerWord,
                    new[] { (ushort)blockNo }, Device.TimeoutMs)
                    .ConfigureAwait(false);

                PlcRegisterLog.Track(Device.IpAddress, Device.BlockNo, reg,
                                     blockNo.ToString(),
                                     blockNo == 0 ? "khong tim thay xe"
                                                  : "xe dang o block " + blockNo);
            }
            catch (Exception ex)
            {
                // Ghi that bai cung phai vao nhat ky: mot lenh ghi khong toi noi
                // la thu can biet nhat khi truy vet, va no khong de lai dau vet
                // nao khac.
                PlcAuditLog.Write(Device.IpAddress, Device.BlockNo, reg, blockNo, false, ex.Message);
                throw;
            }

            // IP lay tu Device.IpAddress — DIA CHI THAT da gui goi tin toi, khong
            // phai suy lai tu block_no.
            PlcAuditLog.Write(Device.IpAddress, Device.BlockNo, reg, blockNo, true);

            // Hen gio xoa: chi khi vua ghi mot gia tri KHAC 0. Ghi 0 thi khong hen
            // lai, neu khong se thanh vong xoa-roi-hen-xoa vo tan.
            _answerAtUtc = blockNo != 0 ? DateTime.UtcNow : (DateTime?)null;
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
                    // Không tra được DB thì cũng không dò được bố cục. Thôi dò,
                    // rơi xuống nhánh mặc định bên dưới.
                    break;
                }
            }

            // Không bố cục nào cho ra thẻ ĐÃ ĐĂNG KÝ -> vẫn trả về mã theo bố cục
            // mặc định, KHÔNG trả null.
            //
            // Sửa một lỗi thật: trước đây nhánh này trả null, nên thẻ test ngoài
            // hiện trường (không có trong parking_card) bị coi là "không giải mã
            // được" và D1000 luôn nhận 0 — trong khi vòng quét ô đọc CÙNG giá trị
            // đó lại giải mã ra bình thường. Hai bộ giải mã cho hai kết quả khác
            // nhau trên cùng một dãy byte là thứ không được phép tồn tại.
            //
            // Layout vẫn KHÔNG được chốt ở đây: chốt bố cục dựa trên một mã chưa
            // xác nhận thì một bố cục sai vẫn có xác suất cho ra mã trùng thẻ người
            // khác. Việc lọc rác đã chuyển sang CarLocatorService, nơi dùng quy tắc
            // "một mã thẻ chỉ nằm ở đúng một block".
            return CardCodeDecoder.TryDecode(words, CardCodeLayout.Binary32Lo);
        }

        // Ghi nhật ký không được phép làm hỏng vòng trao đổi: HMI đã nhận câu trả
        // lời rồi, mất một dòng log không đáng để ném lỗi và kéo theo reconnect.
        // Ghi nhat ky mot luot TIM XE.
        //
        // Dung lai bang plc_request nhung y nghia hai cot doi theo hop dong moi:
        //   result_permit = tim thay xe hay khong
        //   result_class  = SO BLOCK tra ve (khong con la 2200/2600)
        // Doi y nghia cot ma khong doi ten la mot mon no ky thuat co y: doi ten cot
        // can DDL va lam hong du lieu cu, trong khi bang nay chi de truy vet.
        private void SafeLogFind(string rawHex, string cardCode, DateTime receivedAt,
                                 int blockNo, DateTime? answeredAt)
        {
            try
            {
                var d = new CardScanDecision
                {
                    Permit           = blockNo != CarLocatorService.NotFound,
                    WeightClassValue = blockNo,
                    IsRetrieval      = true
                };
                if (blockNo == CarLocatorService.NotFound)
                    d.RejectReason = RejectReason.InvalidCard;

                _requests.Log(Device.BlockId, rawHex, cardCode, receivedAt, d, answeredAt);
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
