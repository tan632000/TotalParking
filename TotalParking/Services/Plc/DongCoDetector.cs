using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace TotalParking.Services.Plc
{
    // Nhận lượt chạy động cơ từ các lần đọc CIO 100..102.
    //
    // ===================== NGUỒN BẢNG BIT =====================
    // docs/NOTE FINAL.xlsx bản 13/10/2026 09:40, một sheet cho mỗi loại khối (số
    // ô = block.slot_count). Ô tô màu là động cơ được đếm. `Qwww.bb` = CIO word
    // www, bit bb. BLOCK 10 lấy cột C (cột B lệch từ dòng 9). Nam châm điện được
    // đếm như một động cơ (quyết định C1).
    //
    // ===================== QUY TẮC (ghi chú trong NOTE FINAL) =====================
    // Động cơ BẮT ĐẦU chạy khi bit của nó ON và contactor nguồn ON và (thuận ON
    // hoặc nghịch ON). KẾT THÚC khi bit của nó OFF — nguồn hay chiều tắt trước
    // không kết thúc lượt.
    //
    // ===================== KHÔNG ĐẾM LƯỢT KHÔNG THẤY TRỌN =====================
    // - Sau khi khởi động, mỗi động cơ phải được thấy OFF một lần rồi mới được bắt
    //   đầu lượt: lượt đang dở lúc ứng dụng khởi động lại (hay IIS recycle chồng
    //   lấn) sẽ bị cắt đầu và đếm trùng với tiến trình cũ.
    // - Khối đọc thất bại quá NguongMatDoc nhịp LIÊN TIẾP trong lúc động cơ chạy
    //   thì bỏ lượt đó: không biết nó kết thúc lúc nào, và một lượt 10 giây sẽ bị
    //   ghi thành 2 giờ. Đếm theo NHỊP CỦA CHÍNH KHỐI chứ không theo giây, để một
    //   nhịp kéo dài vì PLC khác chậm không làm mất lượt của khối này.
    //
    // Không an toàn đa luồng — PlcIoPoller gọi tuần tự sau khi đọc xong cả nhịp.
    public class DongCoDetector
    {
        public const int NguongMatDoc = 3;

        private const int WordGoc = 100;   // CIO 100 là phần tử đầu của mảng đọc

        public sealed class Bit
        {
            public readonly int Word, So;
            public Bit(int word, int so) { Word = word; So = so; }
            public bool Doc(ushort[] cio)
            {
                int i = Word - WordGoc;
                return i >= 0 && i < cio.Length && ((cio[i] >> So) & 1) == 1;
            }
            public override string ToString() { return "Q" + Word + "." + So.ToString("00"); }
        }

        public sealed class DongCo
        {
            public readonly string Ma; public readonly Bit Bit;
            public DongCo(string ma, Bit bit) { Ma = ma; Bit = bit; }
        }

        public sealed class LoaiKhoi
        {
            public readonly Bit Nguon;
            public readonly IList<DongCo> DongCo;
            public LoaiKhoi(Bit nguon, params DongCo[] dongCo) { Nguon = nguon; DongCo = dongCo; }
        }

        // ===================== LOẠI ĐỘNG CƠ VÀ THỜI GIAN CHUẨN =====================
        // Người dùng chốt 16/10/2026: thời gian hoạt động = số lần × thời gian chuẩn
        // của loại, KHÔNG lấy thời lượng đo được (độ phân giải đo bằng nhịp đọc, và
        // rào chắn đo từ lúc có nguồn + chiều chứ không từ lúc motor quay).
        // Nam châm chỉ đếm số lần, không có thời gian chuẩn.
        //
        // Liệt kê từng mã chứ không suy từ chữ số đầu: mã ngoài danh sách thì
        // không có loại, thay vì bị đoán nhầm thành trượt hay nâng.
        public sealed class LoaiDongCo
        {
            public readonly string Ma, Ten;
            public readonly int?   ChuanGiay;
            public LoaiDongCo(string ma, string ten, int? chuanGiay) { Ma = ma; Ten = ten; ChuanGiay = chuanGiay; }
        }

        public static readonly LoaiDongCo Truot   = new LoaiDongCo("TRUOT",    "Trượt",    20);
        public static readonly LoaiDongCo Nang    = new LoaiDongCo("NANG",     "Nâng",     24);
        public static readonly LoaiDongCo HangRao = new LoaiDongCo("HANG_RAO", "Hàng rào", 12);
        public static readonly LoaiDongCo NamCham = new LoaiDongCo("NAM_CHAM", "Nam châm", null);

        // Thứ tự hiển thị.
        public static readonly IList<LoaiDongCo> CacLoai = new[] { Truot, Nang, HangRao, NamCham };

        private static readonly IDictionary<string, LoaiDongCo> LoaiTheoMa = new Dictionary<string, LoaiDongCo>
        {
            { "102", Truot }, { "103", Truot }, { "112", Truot }, { "113", Truot },
            { "201", Nang }, { "202", Nang }, { "203", Nang },
            { "211", Nang }, { "212", Nang }, { "213", Nang },
            { "RAO_CHAN", HangRao },
            { "NAM_CHAM", NamCham }
        };

        // Null khi mã không có trong danh sách.
        public static LoaiDongCo PhanLoai(string motorCode)
        {
            LoaiDongCo l;
            return motorCode != null && LoaiTheoMa.TryGetValue(motorCode, out l) ? l : null;
        }

        public static readonly Bit Thuan  = new Bit(100, 2);
        public static readonly Bit Nghich = new Bit(100, 3);

        private static DongCo D(string ma, int word, int so) { return new DongCo(ma, new Bit(word, so)); }

        // slot_count -> bảng bit.
        public static readonly IDictionary<int, LoaiKhoi> BangBit = new Dictionary<int, LoaiKhoi>
        {
            { 3, new LoaiKhoi(new Bit(101, 1),
                    D("201", 100, 4), D("202", 100, 5), D("102", 100, 6),
                    D("RAO_CHAN", 100, 7), D("NAM_CHAM", 101, 0)) },
            { 5, new LoaiKhoi(new Bit(101, 3),
                    D("201", 100, 4), D("202", 100, 5), D("203", 100, 6), D("102", 100, 7),
                    D("103", 101, 0), D("RAO_CHAN", 101, 1), D("NAM_CHAM", 101, 2)) },
            { 6, new LoaiKhoi(new Bit(101, 6),
                    D("201", 100, 4), D("202", 100, 5), D("102", 100, 6), D("RAO_CHAN", 100, 7),
                    D("211", 101, 2), D("212", 101, 3), D("112", 101, 4), D("NAM_CHAM", 101, 5)) },
            { 10, new LoaiKhoi(new Bit(102, 2),
                    D("201", 100, 4), D("202", 100, 5), D("203", 100, 6), D("102", 100, 7),
                    D("103", 101, 0), D("RAO_CHAN", 101, 1), D("211", 101, 4), D("212", 101, 5),
                    D("213", 101, 6), D("112", 101, 7), D("113", 102, 0), D("NAM_CHAM", 102, 1)) }
        };

        private sealed class TrangThaiDongCo
        {
            public bool     DaThayOff;
            public bool     DangChay;
            public DateTime BatDau;
            public long     BatDauTick;
        }

        private sealed class TrangThaiKhoi
        {
            public int LoiLienTiep;
            public readonly Dictionary<string, TrangThaiDongCo> DongCo = new Dictionary<string, TrangThaiDongCo>();
        }

        private readonly IMotorRunStore _kho;
        private readonly IDictionary<int, int> _loaiKhoi;
        private readonly Dictionary<int, TrangThaiKhoi> _khoi = new Dictionary<int, TrangThaiKhoi>();
        private readonly HashSet<int> _daGhiLoaiLa = new HashSet<int>();

        // loaiKhoi: block_no -> slot_count (MotorRunRepository.DocLoaiKhoi).
        public DongCoDetector(IMotorRunStore kho, IDictionary<int, int> loaiKhoi)
        {
            _kho      = kho;
            _loaiKhoi = loaiKhoi;
        }

        // Một lần đọc CIO thành công. luc = giờ ghi vào DB; tick =
        // Stopwatch.GetTimestamp() cùng thời điểm, để đo thời lượng không phụ
        // thuộc đồng hồ hệ thống.
        public void QuanSat(int blockNo, ushort[] cio, DateTime luc, long tick)
        {
            LoaiKhoi loai = Loai(blockNo);
            if (loai == null || cio == null) return;

            TrangThaiKhoi kh = Khoi(blockNo);
            kh.LoiLienTiep = 0;

            bool duDieuKien = loai.Nguon.Doc(cio) && (Thuan.Doc(cio) || Nghich.Doc(cio));

            foreach (DongCo dc in loai.DongCo)
            {
                TrangThaiDongCo tt;
                if (!kh.DongCo.TryGetValue(dc.Ma, out tt))
                {
                    tt = new TrangThaiDongCo();
                    kh.DongCo[dc.Ma] = tt;
                }

                if (!dc.Bit.Doc(cio))
                {
                    if (tt.DangChay)
                    {
                        long ms = (long)((tick - tt.BatDauTick) * 1000.0 / Stopwatch.Frequency);
                        GhiLuot(blockNo, dc.Ma, tt.BatDau, luc, Math.Max(0L, ms));
                    }
                    tt.DangChay  = false;
                    tt.DaThayOff = true;
                    continue;
                }

                if (!tt.DangChay && tt.DaThayOff && duDieuKien)
                {
                    tt.DangChay   = true;
                    tt.BatDau     = luc;
                    tt.BatDauTick = tick;
                }
            }
        }

        // Một nhịp không đọc được CIO của khối (lỗi đọc, bỏ nhịp vì gate bận,
        // hoặc đọc được D3 mà CIO hỏng).
        public void DocLoi(int blockNo)
        {
            if (Loai(blockNo) == null) return;
            TrangThaiKhoi kh = Khoi(blockNo);
            if (kh.LoiLienTiep < int.MaxValue) kh.LoiLienTiep++;
            if (kh.LoiLienTiep <= NguongMatDoc) return;

            foreach (var p in kh.DongCo)
            {
                if (p.Value.DangChay)
                {
                    PlcAuditLog.Error(null, blockNo, "DONG CO K" + blockNo,
                        "Bo luot chay " + p.Key + " bat dau " + p.Value.BatDau.ToString("HH:mm:ss") +
                        ": mat doc qua " + NguongMatDoc + " nhip");
                }
                p.Value.DangChay  = false;
                p.Value.DaThayOff = false;
            }
        }

        private LoaiKhoi Loai(int blockNo)
        {
            int slot;
            LoaiKhoi loai;
            if (!_loaiKhoi.TryGetValue(blockNo, out slot)) return null;
            if (BangBit.TryGetValue(slot, out loai)) return loai;

            if (_daGhiLoaiLa.Add(blockNo))
            {
                PlcAuditLog.Error(null, blockNo, "DONG CO K" + blockNo,
                    "slot_count=" + slot + " khong co trong NOTE FINAL (3/5/6/10), bo qua khoi");
            }
            return null;
        }

        private TrangThaiKhoi Khoi(int blockNo)
        {
            TrangThaiKhoi kh;
            if (!_khoi.TryGetValue(blockNo, out kh))
            {
                kh = new TrangThaiKhoi();
                _khoi[blockNo] = kh;
            }
            return kh;
        }

        // DB lỗi thì lượt đó mất (đã kết thúc, không có chỗ giữ lại) — ghi nhật ký
        // để còn biết số đếm thiếu bao nhiêu.
        private void GhiLuot(int blockNo, string ma, DateTime batDau, DateTime ketThuc, long ms)
        {
            try
            {
                _kho.Ghi(blockNo, ma, batDau, ketThuc, ms);
            }
            catch (Exception ex)
            {
                PlcAuditLog.Error(null, blockNo, "DONG CO K" + blockNo,
                    "Khong ghi duoc luot chay " + ma + ": " + ex.Message);
            }
        }
    }

    // Chỗ lưu lượt chạy. Tách ra để kiểm chứng quy tắc bằng kho trong bộ nhớ.
    public interface IMotorRunStore
    {
        void Ghi(int blockNo, string motorCode, DateTime batDau, DateTime ketThuc, long thoiLuongMs);
    }

    public class MotorRunStore : IMotorRunStore
    {
        private readonly MotorRunRepository _repo = new MotorRunRepository();

        public void Ghi(int blockNo, string motorCode, DateTime batDau, DateTime ketThuc, long thoiLuongMs)
        {
            _repo.Ghi(blockNo, motorCode, batDau, ketThuc, thoiLuongMs);
        }
    }
}
