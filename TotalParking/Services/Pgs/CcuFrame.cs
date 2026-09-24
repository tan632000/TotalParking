using System;

namespace TotalParking.Services.Pgs
{
    // Vì sao khung bị loại. Tách riêng từng lý do thay vì một cờ "hỏng": khung
    // nhịp sống bị loại là chuyện bình thường mỗi 5 giây, còn khung sai CRC là
    // dấu hiệu đường truyền có vấn đề. Gộp hai thứ đó vào một bộ đếm thì không
    // ai phân biệt được "đang chạy tốt" với "đang mất dữ liệu".
    public enum CcuLyDoLoai
    {
        KhongLoai = 0,   // khung dữ liệu hợp lệ
        SaiKhung,        // thiếu $ hoặc * hoặc #
        SaiCrc,
        NhipSong,        // $CCU,01,OK — CCU còn sống, không phải dữ liệu
        KhongPhaiDuLieu, // $CCU,xx khác 02
        ThieuTruong      // đúng $CCU,02 nhưng không đủ trường
    }

    // Một gói báo cáo trạng thái của MỘT ZCU, do CCU đẩy về cổng 2000.
    //
    // ===================== CẤU TRÚC, THEO TÀI LIỆU NHÀ CUNG CẤP =====================
    // docs/Giao-thức-giao-tiếp-giữa-máy-tính-và-thiết-bị-thu-thập-trung-tâm-CCU.pdf
    // mục 2.2.2:
    //
    //   $CCU,02,X1,X2,X3,Y1...Y32,Z1...Z32*CRC#
    //
    //   X1        số ZCU có trong hệ thống, 0..16
    //   X2        địa chỉ định danh ID của ZCU, 0..15
    //   X3        trạng thái kết nối của ZCU: 0 mất, 1 đang nối
    //   Y1..Y32   32 cảm biến kênh RS485 thứ nhất
    //   Z1..Z32   32 cảm biến kênh RS485 thứ hai
    //
    // Mỗi ký tự trạng thái: 0 trống, 1 có xe, 2 lỗi, 3 không lắp.
    //
    // Khác hẳn PgsFrame cũ: ở đây KHÔNG có gì phải suy luận. Trước đó chúng ta tự
    // dò mẫu bit của khung nhị phân 83 byte vì tưởng không có đặc tả; hoá ra tài
    // liệu nằm sẵn trong docs/ và mô tả một đường khác hẳn, đọc thẳng ra ký tự.
    public class CcuFrame
    {
        public const int SoCamBienMoiKenh = 32;

        // Tài liệu mục 1: "1 thiết bị CCU quản lý tối đa 16 thiết bị ZCU".
        // Dùng 16 chứ không phải 5 con đang lắp: thêm ZCU ngoài hiện trường là
        // việc cấu hình (mục 4.3), không ai báo cho phần mềm biết.
        public const int SoZcuToiDa = 16;

        public int  SoZcuTrongHeThong { get; private set; }   // X1
        public int  ZcuId             { get; private set; }   // X2
        public bool ZcuDangKetNoi     { get; private set; }   // X3

        public int Trong     { get; private set; }
        public int CoXe      { get; private set; }
        public int Loi       { get; private set; }
        public int KhongLap  { get; private set; }

        // Ký tự lạ ngoài 0..3. Không im lặng bỏ qua: nếu firmware đổi bảng mã thì
        // đây là chỗ duy nhất phát hiện được.
        public int KhongHieu { get; private set; }

        public int DaLap { get { return Trong + CoXe + Loi; } }

        // CRC theo tài liệu mục 2.1: XOR mọi byte NẰM GIỮA '$' và '*'.
        public static byte TinhCrc(string than)
        {
            byte c = 0;
            for (int i = 0; i < than.Length; i++) c ^= (byte)than[i];
            return c;
        }

        // Giải một khung đã tách sẵn, dạng "$...*XX#".
        //
        // Trả false cho mọi khung không phải dữ liệu, kèm lý do cụ thể ở lyDo.
        // Người gọi tự quyết định đếm lý do nào vào đâu — lớp này không phán xét.
        public static bool TryParse(string khung, out CcuFrame frame, out CcuLyDoLoai lyDo)
        {
            frame = null;
            lyDo  = CcuLyDoLoai.SaiKhung;

            if (string.IsNullOrEmpty(khung)) return false;
            if (khung[0] != '$' || khung[khung.Length - 1] != '#') return false;

            int sao = khung.LastIndexOf('*');
            if (sao < 1 || sao >= khung.Length - 1) return false;

            string than = khung.Substring(1, sao - 1);
            string crcVanBan = khung.Substring(sao + 1, khung.Length - sao - 2);
            if (crcVanBan.Length == 0) return false;

            // So không phân biệt hoa/thường. Đo được firmware ở bãi này gửi chữ
            // HOA (91/91 khung ngày 24/09), nhưng mã mẫu của chính nhà cung cấp
            // (mục 3.1) thử cả "%.2x" lẫn "%.2X" — tức là họ cũng không chắc.
            string crcMong = TinhCrc(than).ToString("X2");
            if (!string.Equals(crcVanBan, crcMong, StringComparison.OrdinalIgnoreCase))
            {
                lyDo = CcuLyDoLoai.SaiCrc;
                return false;
            }

            // Tách bằng CẢ dấu phẩy: thân không còn chứa '*' nên chỉ cần ','.
            // Không cắt theo vị trí cố định — ID ZCU từ 10 trở lên dài hai ký tự
            // (bảng ký tự mục 2.2.2 liệt kê tới "16"), cắt cứng là lệch hết trường.
            string[] p = than.Split(',');
            if (p.Length < 2) { lyDo = CcuLyDoLoai.SaiKhung; return false; }

            if (p[1] == "01")
            {
                // $CCU,01,OK — trả lời lệnh giữ nhịp. Chỉ có 3 trường; đọc tới
                // trường thứ 5 là ném IndexOutOfRange. Đây là khung thật, xuất
                // hiện mỗi lần gửi LIVE (đo được 10 khung trong 40 giây).
                lyDo = CcuLyDoLoai.NhipSong;
                return false;
            }

            if (p[1] != "02") { lyDo = CcuLyDoLoai.KhongPhaiDuLieu; return false; }

            if (p.Length < 7) { lyDo = CcuLyDoLoai.ThieuTruong; return false; }

            int x1, x2, x3;
            if (!int.TryParse(p[2], out x1) ||
                !int.TryParse(p[3], out x2) ||
                !int.TryParse(p[4], out x3))
            {
                lyDo = CcuLyDoLoai.ThieuTruong;
                return false;
            }

            if (x2 < 0 || x2 >= SoZcuToiDa) { lyDo = CcuLyDoLoai.ThieuTruong; return false; }
            if (p[5].Length != SoCamBienMoiKenh || p[6].Length != SoCamBienMoiKenh)
            {
                lyDo = CcuLyDoLoai.ThieuTruong;
                return false;
            }

            var f = new CcuFrame
            {
                SoZcuTrongHeThong = x1,
                ZcuId             = x2,
                ZcuDangKetNoi     = x3 == 1
            };
            f.Dem(p[5]);
            f.Dem(p[6]);

            frame = f;
            lyDo  = CcuLyDoLoai.KhongLoai;
            return true;
        }

        private void Dem(string kenh)
        {
            for (int i = 0; i < kenh.Length; i++)
            {
                switch (kenh[i])
                {
                    case '0': Trong++;    break;
                    case '1': CoXe++;     break;
                    case '2': Loi++;      break;
                    case '3': KhongLap++; break;
                    default:  KhongHieu++; break;
                }
            }
        }
    }
}
