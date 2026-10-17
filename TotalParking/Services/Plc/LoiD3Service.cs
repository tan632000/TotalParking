using System;
using System.Collections.Generic;
using MySqlConnector;

namespace TotalParking.Services.Plc
{
    // Lỗi khối báo qua thanh ghi D3 -> cảnh báo trong canh_bao.
    //
    // ===================== NGUỒN BẢNG MÃ =====================
    // docs/error.xls, cột "Language 3, State 1". Mã là số sau 故障 trong cột
    // "Label tag name". File ghi "dùng cho cả 3 5 6 10" — một bảng cho mọi loại
    // khối. Nhãn chép NGUYÊN VĂN, kể cả lỗi chính tả và 114/115 (nhãn có vẻ tráo
    // tầng so với bản tiếng Trung): sửa nhãn là việc của người cấp file.
    //
    // Mã không có trong bảng (đã thấy 108, 899) bị BỎ QUA theo quyết định của
    // người dùng — chỉ ghi nhật ký để còn tra về sau.
    //
    // ===================== CHỐNG RUNG =====================
    // Một mã chỉ được coi là "ổn định" khi đọc được N lần liên tiếp. Không có nó,
    // lỗi quá tải bật/tắt theo từng lần nâng sẽ mở rồi đóng một dòng mỗi giây.
    // Lần đọc lỗi không đếm và không xoá chuỗi đang đếm.
    //
    // ===================== HOÀ GIẢI THEO DB, KHÔNG THEO BỘ NHỚ =====================
    // Mỗi khi mã ổn định của một khối được xác lập, đọc khoá đang mở của chính
    // khối đó trong DB rồi làm cho khớp. Không so "mã cũ trong bộ nhớ": DB lỗi
    // giữa chừng sẽ làm bộ nhớ và DB lệch nhau mãi. Hoà giải hỏng thì khối giữ
    // trạng thái chưa hoà giải và lần đọc ổn định kế tiếp làm lại.
    //
    // Khối không đọc được thì không bị đụng tới — kể cả lúc vừa khởi động: một
    // cảnh báo đang mở chỉ đóng khi chính PLC của khối đó nói lỗi đã hết.
    public class LoiD3Service
    {
        public const string TienToKhoa = "PLC_D3:";
        public const string TienToMaLoi = "PLC-D3-";

        public static readonly IDictionary<int, string> BangMa = new Dictionary<int, string>
        {
            {   1, "Cảnh báo nút dừng khẩn cấp" },
            {   2, "Bảo vệ Phase" },
            {   3, "Cảnh báo quá hành trình" },
            {   5, "Lỗi vượt quá chiều cao" },
            {   6, "Cảm biến chiều dài sau" },
            {   7, "Cảm biến chiều dài trước" },
            {   9, "Quá thời gian trượt ngang" },
            {  10, "Chưa đủ thời gian trượt ngang" },
            {  11, "Quá thời gian nâng hạ" },
            {  12, "Không đủ thời gian nâng hạ" },
            {  14, "Móc chóng rơi chưa mở" },
            {  17, "Xâm nhập hệ thống" },
            {  18, "Cảnh báo trùng xích" },
            {  20, "Giới hạn trên, dưới cửa đóng cùng lúc" },
            {  22, "Cảnh báo quá giới hạn cửa" },
            {  74, "Quá thời gian trượt ( Tầng 1)" },
            {  75, "Quá thời gian tượt ( Tầng 2)" },
            {  79, "Không đủ thời gian trượt ( Tầng 1)" },
            {  80, "Không đủ thời gian trượt ( Tầng 2)" },
            {  84, "Giới hạn ngang đóng cùng lúc" },
            {  85, "Giới hạn trên, dưới đóng cùng lúc" },
            { 102, "Cảnh báo quá tải động cơ" },
            { 103, "Lỗi vị trí" },
            { 110, "Cửa tủ chưa đóng" },
            { 114, "Hành trình ngang tầng 2 đóng cùng lúc" },
            { 115, "Hành trình ngang tầng 1 đóng cùng lúc" },
            { 119, "Cảnh báo giới hạn tầng 2" }
        };

        public static string Khoa(int blockNo, int ma)
        {
            return TienToKhoa + blockNo + ":" + ma;
        }

        private sealed class TrangThai
        {
            public int  UngVien = -1;   // mã đang đếm
            public int  SoLan;          // số lần đọc liên tiếp ra UngVien
            public int  OnDinh = -1;    // mã ổn định đã hoà giải, -1 = chưa có
            public bool DaHoaGiai;
        }

        private readonly ICanhBaoD3Store _kho;
        private readonly int _nguong;
        private readonly Dictionary<int, TrangThai> _khoi = new Dictionary<int, TrangThai>();
        private readonly Dictionary<string, DateTime> _daGhiMaLa = new Dictionary<string, DateTime>();

        public LoiD3Service(ICanhBaoD3Store kho, int nguongChongRung)
        {
            _kho    = kho;
            _nguong = Math.Max(1, nguongChongRung);
        }

        // Một lần đọc thành công của một khối. Người gọi KHÔNG gọi hàm này khi đọc
        // hỏng: đọc hỏng không phải bằng chứng gì về lỗi của khối.
        //
        // Không an toàn đa luồng — PlcIoPoller gọi tuần tự sau khi đọc xong cả nhịp.
        public void QuanSat(int blockNo, int? zoneId, int d3)
        {
            TrangThai tt;
            if (!_khoi.TryGetValue(blockNo, out tt))
            {
                tt = new TrangThai();
                _khoi[blockNo] = tt;
            }

            if (d3 == tt.UngVien)
            {
                if (tt.SoLan < int.MaxValue) tt.SoLan++;
            }
            else
            {
                tt.UngVien = d3;
                tt.SoLan   = 1;
            }

            if (tt.SoLan < _nguong) return;
            if (tt.OnDinh == d3 && tt.DaHoaGiai) return;

            tt.OnDinh    = d3;
            tt.DaHoaGiai = HoaGiai(blockNo, zoneId, d3);

            if (d3 != 0 && !BangMa.ContainsKey(d3)) GhiMaLa(blockNo, d3);
        }

        private bool HoaGiai(int blockNo, int? zoneId, int d3)
        {
            string nhan;
            string can = BangMa.TryGetValue(d3, out nhan) ? Khoa(blockNo, d3) : null;

            try
            {
                bool daCo = false;
                foreach (string k in _kho.KhoaDangMo(blockNo))
                {
                    if (k == can) { daCo = true; continue; }
                    _kho.Dong(k);
                }

                if (can != null && !daCo) _kho.Mo(blockNo, zoneId, d3, nhan, can);
                return true;
            }
            catch (Exception ex)
            {
                // Số khối nằm trong op: PlcAuditLog.Error chống lặp theo ip|op + nội
                // dung, mà ip ở đây là null cho mọi khối.
                PlcAuditLog.Error(null, blockNo, "LOI D3 K" + blockNo,
                                  "Khong hoa giai duoc canh bao D3=" + d3 + ": " + ex.Message);
                return false;
            }
        }

        // Mỗi khối/mã một dòng mỗi giờ: một khối kẹt ở 899 cả ngày không được
        // biến nhật ký thành 86.400 dòng.
        private void GhiMaLa(int blockNo, int d3)
        {
            string k = blockNo + ":" + d3;
            DateTime lan;
            if (_daGhiMaLa.TryGetValue(k, out lan) && DateTime.UtcNow - lan < TimeSpan.FromHours(1)) return;
            _daGhiMaLa[k] = DateTime.UtcNow;
            PlcAuditLog.Error(null, blockNo, "LOI D3 K" + blockNo,
                              "Ma D3=" + d3 + " khong co trong docs/error.xls, bo qua");
        }
    }

    // Chỗ lưu cảnh báo D3. Tách ra để kiểm chứng chuyển trạng thái được bằng kho
    // trong bộ nhớ, không cần MySQL.
    public interface ICanhBaoD3Store
    {
        // Các khoá PLC_D3:<blockNo>:<mã> đang mở của đúng một khối.
        IList<string> KhoaDangMo(int blockNo);
        void Mo(int blockNo, int? zoneId, int ma, string nhan, string khoa);
        void Dong(string khoa);
    }

    public class CanhBaoD3Store : ICanhBaoD3Store
    {
        private readonly CanhBaoRepository _repo = new CanhBaoRepository();

        // Lệnh đóng tách thành delegate để kiểm chứng được tham số người xác nhận
        // mà không chạm DB thật: bỏ quên NguoiXacNhanTuDong là lỗi chỉ lộ ra trên
        // giao diện (dòng đã đóng vẫn hiện "Chờ xác nhận").
        private readonly Func<string, string, int> _dongTheoKhoa;

        public CanhBaoD3Store()
        {
            _dongTheoKhoa = (khoa, nguoi) => _repo.DongTheoKhoa(khoa, nguoi);
        }

        public CanhBaoD3Store(Func<string, string, int> dongTheoKhoa)
        {
            _dongTheoKhoa = dongTheoKhoa;
        }

        public IList<string> KhoaDangMo(int blockNo)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();
                // Dấu ":" cuối giữ "PLC_D3:2:" khỏi khớp nhầm khoá của khối 21.
                return _repo.DocKhoaDangMo(conn, LoiD3Service.TienToKhoa + blockNo + ":");
            }
        }

        public void Mo(int blockNo, int? zoneId, int ma, string nhan, string khoa)
        {
            _repo.Ghi(new CanhBao
            {
                Nguon   = "hardware",
                MucDo   = "high",
                MaLoi   = LoiD3Service.TienToMaLoi + ma,
                BlockNo = blockNo,
                ZoneId  = zoneId,
                ThietBi = "PLC khối " + blockNo + " (D3=" + ma + ")",
                MoTa    = nhan
            }, khoa);
        }

        // Đóng kèm tự xác nhận: lỗi đã do chính PLC báo hết, không ai cần bấm xác
        // nhận nữa — để nó nằm ở ô "chờ xác nhận" thì trang Cảnh báo hiện nó y như
        // đang lỗi. Cùng cách với CanhBaoPlcService khi PLC nối lại.
        public void Dong(string khoa)
        {
            _dongTheoKhoa(khoa, CanhBaoRepository.NguoiXacNhanTuDong);
        }
    }
}
