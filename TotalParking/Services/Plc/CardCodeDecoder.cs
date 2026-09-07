using System;
using System.Text;

namespace TotalParking.Services.Plc
{
    // Bố cục mã thẻ trong vùng nhớ PLC.
    //
    // parking_card.card_code là UID 4 byte lưu 8 ký tự hex (ví dụ a0d22940).
    // Một word PLC chỉ có 16 bit nên D100 đơn lẻ không đủ chỗ: 0xA0D22940 =
    // 2.698.088.256, còn một word tối đa 65535. Giá trị này bắt buộc trải trên
    // nhiều word, và bên lập trình PLC chưa xác nhận là dạng nào.
    public enum CardCodeLayout
    {
        // 2 word nhị phân, word cao trước: D100 = 0xA0D2, D101 = 0x2940
        Binary32Hi,
        // 2 word nhị phân, word thấp trước. Omron thường xếp double word theo
        // kiểu này nên nó là ứng viên nhiều khả năng nhất.
        Binary32Lo,
        // 4 word ASCII, mỗi word 2 ký tự, byte cao là ký tự trước.
        Ascii2PerWord,
        // 8 word ASCII, mỗi word 1 ký tự ở byte thấp.
        Ascii1PerWord
    }

    // Giải mã dãy word đọc từ D100 thành card_code dạng 8 ký tự hex thường.
    //
    // Bố cục thật chưa chốt (xem docs/parking-session-db-design.md mục 6.5), nên
    // lớp này hỗ trợ dò: thử lần lượt các bố cục và chọn bố cục nào cho ra một mã
    // CÓ THẬT trong bảng thẻ. Việc đối chiếu do phía gọi làm — ở đây chỉ giải mã.
    //
    // Dò là biện pháp cho giai đoạn thí điểm, không phải chế độ chạy lâu dài: một
    // bố cục sai vẫn có xác suất nhỏ cho ra mã trùng thẻ khác, và khi đó hệ thống
    // mở nhầm xe của người khác. Xác định xong thì ghi vào plc_device.card_layout.
    public static class CardCodeDecoder
    {
        // Số word cần đọc khi chưa biết bố cục: đủ cho bố cục dài nhất.
        public const int ProbeWordCount = 8;

        public static int WordCountFor(CardCodeLayout layout)
        {
            switch (layout)
            {
                case CardCodeLayout.Binary32Hi:
                case CardCodeLayout.Binary32Lo:    return 2;
                case CardCodeLayout.Ascii2PerWord: return 4;
                case CardCodeLayout.Ascii1PerWord: return 8;
                default: throw new ArgumentOutOfRangeException("layout");
            }
        }

        // Thứ tự dò: bố cục nhiều khả năng nhất trước.
        public static readonly CardCodeLayout[] ProbeOrder =
        {
            CardCodeLayout.Binary32Lo,
            CardCodeLayout.Binary32Hi,
            CardCodeLayout.Ascii2PerWord,
            CardCodeLayout.Ascii1PerWord
        };

        // Trả về mã 8 ký tự hex thường, hoặc null nếu dãy word không hợp lệ với
        // bố cục này. Không ném lỗi: phía gọi đang thử nhiều bố cục.
        public static string TryDecode(ushort[] words, CardCodeLayout layout)
        {
            if (words == null) return null;
            int need = WordCountFor(layout);
            if (words.Length < need) return null;

            switch (layout)
            {
                case CardCodeLayout.Binary32Hi:
                    return FromUInt32(((uint)words[0] << 16) | words[1]);

                case CardCodeLayout.Binary32Lo:
                    return FromUInt32(((uint)words[1] << 16) | words[0]);

                case CardCodeLayout.Ascii2PerWord:
                    return FromAscii(words, 4, 2);

                case CardCodeLayout.Ascii1PerWord:
                    return FromAscii(words, 8, 1);

                default:
                    return null;
            }
        }

        // Toàn 0 nghĩa là chưa có lượt quẹt nào, không phải mã thẻ 00000000.
        public static bool IsEmpty(ushort[] words, int count)
        {
            if (words == null) return true;
            int n = Math.Min(count, words.Length);
            for (int i = 0; i < n; i++)
            {
                if (words[i] != 0) return false;
            }
            return true;
        }

        // Dạng hex để ghi vào plc_request.raw_words — giữ nguyên dữ liệu thô
        // trước khi giải mã, để còn giải mã lại được nếu bố cục hoá ra khác.
        public static string ToRawHex(ushort[] words)
        {
            if (words == null || words.Length == 0) return "";
            var sb = new StringBuilder(words.Length * 5);
            for (int i = 0; i < words.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(words[i].ToString("X4"));
            }
            return sb.ToString();
        }

        private static string FromUInt32(uint value)
        {
            return value.ToString("x8");
        }

        private static string FromAscii(ushort[] words, int wordCount, int charsPerWord)
        {
            var sb = new StringBuilder(8);
            for (int i = 0; i < wordCount; i++)
            {
                if (charsPerWord == 2)
                {
                    if (!AppendHexChar(sb, (byte)(words[i] >> 8)))   return null;
                    if (!AppendHexChar(sb, (byte)(words[i] & 0xFF))) return null;
                }
                else
                {
                    if (!AppendHexChar(sb, (byte)(words[i] & 0xFF))) return null;
                }
            }
            return sb.Length == 8 ? sb.ToString().ToLowerInvariant() : null;
        }

        // Chỉ nhận ký tự hex. Byte lạ nghĩa là bố cục đang thử không đúng —
        // đó là tín hiệu để loại bố cục, không phải lỗi cần ném.
        private static bool AppendHexChar(StringBuilder sb, byte b)
        {
            char c = (char)b;
            bool isHex = (c >= '0' && c <= '9') ||
                         (c >= 'a' && c <= 'f') ||
                         (c >= 'A' && c <= 'F');
            if (!isHex) return false;
            sb.Append(c);
            return true;
        }
    }
}
