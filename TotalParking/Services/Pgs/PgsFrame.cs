using System;

namespace TotalParking.Services.Pgs
{
    // Khung dữ liệu do bộ điều khiển vùng (ZCU) tự đẩy về, cổng 2000.
    //
    // ===================== CẤU TRÚC, GIẢI TỪ DỮ LIỆU THẬT =====================
    // Nhà cung cấp PGS không đưa đặc tả gói tin — bốn tài liệu họ gửi đều là
    // hướng dẫn lắp đặt và cấu hình phần mềm. Cấu trúc dưới đây suy ra từ hơn
    // một ngày ghi khung thật (App_Data/pgs_frames.log):
    //
    //   byte 0      0xFE            mở khung
    //   byte 1-4    32 40 01 30     cố định ở cả 5 ZCU
    //   byte 5      id ZCU × 8      quan sát được 0, 8, 16, 24, 32 -> id 0..4
    //   byte 6      0x08            cố định
    //   byte 7-78   72 byte thân    trạng thái cảm biến
    //   byte 79-80  2 byte          đổi theo mỗi lần thân đổi -> nhiều khả năng
    //                               là checksum hoặc số thứ tự gói
    //   byte 81     0x00
    //   byte 82     0xFF            đóng khung
    //
    // ===================== MỘT BIT MỘT CẢM BIẾN =====================
    // Mọi lần trạng thái đổi trong nhật ký đều lật ĐÚNG MỘT BIT:
    //     byte 51: 40 -> 00      byte 49: 00 -> 01
    //     byte 45: 00 -> 10      byte  0: 00 -> 02
    //
    // Phần thân có mẫu nền lặp `40 20 10 08 04 02 01 00` — một bit chạy dần sang
    // phải, byte thứ tám bằng 0. Cảm biến nào KHÁC mẫu nền là đang ở trạng thái
    // khác thường.
    //
    // ===================== CÒN CHƯA BIẾT =====================
    // Bit nào ứng với ô đỗ nào, và bit bật nghĩa là "có xe" hay "trống". Hai câu
    // đó chỉ giải được bằng bảng ánh xạ của nhà cung cấp, hoặc một lần đỗ xe vào
    // ô đã biết trước rồi xem bit nào đổi.
    //
    // Nên lớp này CHỈ đếm và phơi ra, KHÔNG kết luận số chỗ trống.
    public class PgsFrame
    {
        public const int Length     = 83;
        public const byte Start     = 0xFE;
        public const byte End       = 0xFF;
        public const int BodyOffset = 7;
        public const int BodyLength = 72;

        // Mẫu nền của phần thân. Pha khác nhau giữa các ZCU nên phải tự dò.
        private static readonly byte[] Base = { 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01, 0x00 };

        public int    ZcuId  { get; private set; }
        public byte[] Body   { get; private set; }
        public int    Phase  { get; private set; }

        // Số byte và số bit lệch khỏi mẫu nền.
        public int DeviantBytes { get; private set; }
        public int DeviantBits  { get; private set; }

        // ===================== BA TRẠNG THÁI CẢM BIẾN =====================
        // Mỗi cảm biến có một vị trí bit riêng trong mẫu nền và nhận đúng ba giá
        // trị. Kiểm trên dữ liệu thật ngày 18/09: 313/316 byte khớp, ba ngoại lệ
        // đều là byte 71 (bộ đếm gói) hoặc byte đệm.
        //
        //   bit ở đúng vị trí nền   -> CHƯA LẮP / LỖI
        //   không có bit            -> KHÔNG CÓ XE
        //   bit dịch phải một nấc   -> CÓ XE
        //
        // Vì sao gán như vậy:
        //   - Khách nói mặc định cấu hình 64 cảm biến mỗi ZCU nhưng lắp ít hơn
        //     nhiều, nên "đa số trả về lỗi". Trạng thái nền chiếm 80% -> là lỗi.
        //   - Nhật ký cho thấy trạng thái "bit dịch" có lúc chỉ kéo dài 5-6 giây.
        //     Một ô KHÔNG THỂ trống trong 6 giây rồi lại đầy; nhưng một chiếc xe
        //     chạy ngang qua cảm biến thì đúng bằng đó. Nên "bit dịch" = có xe.
        //   - Cũng có lần kéo dài 51 phút (ZCU 4 byte 38, 11:26 tới 12:17) —
        //     đúng kiểu một lượt đỗ thật.
        //
        // CHIỀU NÀY CHƯA ĐƯỢC XÁC NHẬN BẰNG PHÉP THỬ VẬT LÝ. Suy luận vững nhưng
        // vẫn là suy luận: cần một lần đỗ xe vào ô đã biết trước để chốt.
        public int NotInstalled { get; private set; }
        public int Free         { get; private set; }
        public int Occupied     { get; private set; }
        public int Unknown      { get; private set; }

        // Byte cuối của phần thân đổi giá trị mỗi lần nội dung đổi, kể cả khi chỉ
        // một cảm biến thay đổi — đó là bộ đếm gói hoặc checksum, không phải cảm
        // biến. Tính nó vào thì mọi lượt đổi đều bị đếm thừa một "cảm biến".
        private const int CounterByte = BodyLength - 1;

        public static bool TryParse(byte[] buf, int offset, out PgsFrame frame)
        {
            frame = null;
            if (buf == null || offset < 0 || offset + Length > buf.Length) return false;
            if (buf[offset] != Start || buf[offset + Length - 1] != End) return false;

            var body = new byte[BodyLength];
            Array.Copy(buf, offset + BodyOffset, body, 0, BodyLength);

            var f = new PgsFrame
            {
                ZcuId = buf[offset + 5] / 8,
                Body  = body
            };
            f.Measure();
            frame = f;
            return true;
        }

        // Dò pha của mẫu nền rồi đếm lệch. Dò thay vì đặt cứng: pha khác nhau
        // giữa các ZCU (đo được đều là 4, nhưng không có gì bảo đảm nó cố định,
        // và đoán sai pha thì mọi byte đều bị tính là lệch).
        private void Measure()
        {
            int bestPhase = 0, bestCount = int.MaxValue;
            for (int ph = 0; ph < 8; ph++)
            {
                int n = 0;
                for (int i = 0; i < BodyLength; i++)
                    if (Body[i] != Base[(i + ph) % 8]) n++;
                if (n < bestCount) { bestCount = n; bestPhase = ph; }
            }

            Phase = bestPhase;
            DeviantBytes = bestCount;

            int bits = 0;
            for (int i = 0; i < BodyLength; i++)
            {
                int x = Body[i] ^ Base[(i + bestPhase) % 8];
                while (x != 0) { bits += x & 1; x >>= 1; }
            }
            DeviantBits = bits;

            NotInstalled = Free = Occupied = Unknown = 0;
            for (int i = 0; i < BodyLength; i++)
            {
                if (i == CounterByte) continue;          // bộ đếm gói, không phải cảm biến

                int b = Body[i];
                int baseBit = Base[(i + bestPhase) % 8];

                // Byte đệm: mẫu nền không có bit nào ở đây, không ứng với cảm biến.
                if (baseBit == 0)
                {
                    if (b != 0) Unknown++;
                    continue;
                }

                if (b == baseBit)              NotInstalled++;
                else if (b == 0)               Free++;
                else if (b == (baseBit >> 1))  Occupied++;
                else                           Unknown++;
            }
        }

        // Số vị trí cảm biến thật sự có thiết bị đang báo về.
        public int Installed { get { return Free + Occupied; } }

        // So sánh phần thân với khung trước. Trả về số bit khác nhau — dùng để
        // phát hiện cảm biến nhiễu: một lần đổi thật chỉ lật một bit.
        public int BitsChangedFrom(PgsFrame prev)
        {
            if (prev == null || prev.Body == null) return -1;
            int n = 0;
            for (int i = 0; i < BodyLength; i++)
            {
                int x = Body[i] ^ prev.Body[i];
                while (x != 0) { n += x & 1; x >>= 1; }
            }
            return n;
        }

        public string BodyHex()
        {
            var sb = new System.Text.StringBuilder(BodyLength * 2);
            foreach (var b in Body) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}
