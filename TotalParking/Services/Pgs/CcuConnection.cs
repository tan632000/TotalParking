using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TotalParking.Services.Pgs
{
    // Ảnh chụp trạng thái một ZCU, dựng từ gói CCU gần nhất của nó.
    public class CcuZcuState
    {
        public int  ZcuId         { get; set; }
        public bool DangKetNoi    { get; set; }

        public int Trong    { get; set; }
        public int CoXe     { get; set; }
        public int Loi      { get; set; }
        public int KhongLap { get; set; }
        public int DaLap    { get { return Trong + CoXe + Loi; } }

        // Ký tự trạng thái ngoài 0..3. Phải đi hết đường lên tới endpoint, không
        // được dừng ở đây: nếu firmware đổi bảng mã thì bốn con số kia chỉ lặng
        // lẽ nhỏ đi, và không còn tín hiệu nào khác để nhận ra.
        public int KhongHieu { get; set; }

        public DateTime LanCuoiCoGoiUtc  { get; set; }

        // Lần cuối CCU báo ZCU này ĐANG kết nối. Khác hẳn LanCuoiCoGoiUtc: CCU
        // vẫn đều đặn đẩy gói cho một ZCU đã chết (kèm X3 = 0), nên đo tuổi bằng
        // gói cuối thì mọi ZCU đều "mới tinh" kể cả con hỏng từ hôm qua.
        public DateTime? LanCuoiKetNoiUtc { get; set; }

        public long SoGoi { get; set; }

        public int TuoiGoiGiay
        {
            get { return (int)(DateTime.UtcNow - LanCuoiCoGoiUtc).TotalSeconds; }
        }

        public int? TuoiKetNoiGiay
        {
            get
            {
                if (!LanCuoiKetNoiUtc.HasValue) return null;
                return (int)(DateTime.UtcNow - LanCuoiKetNoiUtc.Value).TotalSeconds;
            }
        }
    }

    // Một kết nối tới CCU. CHỈ gửi lệnh giữ nhịp, không gửi lệnh cấu hình nào.
    //
    // ===================== VÌ SAO PHẢI GỬI =====================
    // Đây là chỗ đường cũ hiểu sai. ZCU tự đẩy khung về, nên nối vào rồi im lặng
    // là đủ. CCU thì ngược lại: tài liệu mục 2.2.1 nói máy tính đóng vai CLIENT
    // và phải gửi $CCU,01,LIVE*42# khoảng 5 giây một lần; quá 30 giây không nhận
    // được gì thì CCU "không tự động update dữ liệu và đóng socket đang hoạt động".
    //
    // Đo ngày 18/09 kết luận ".75 nhận TCP nhưng không đẩy gói nào" — quan sát
    // đúng, kết luận sai. Nó đang chờ được chào.
    //
    // ===================== VÌ SAO KHÔNG CHỐNG RUNG =====================
    // Đường ZCU cũ giữ khung 10 giây mới công bố, vì khung nhị phân không có gì
    // kiểm lỗi nên phải lọc nhiễu bằng thời gian. Ở đây mỗi khung đã có CRC, nên
    // khung qua được là khung thật. Giữ lại bộ chống rung thì một cảm biến lỗi
    // nhấp nháy nhanh hơn 10 giây sẽ khoá cứng cả ZCU: mỗi lần nội dung đổi lại
    // đặt đồng hồ về 0, và trạng thái công bố không bao giờ đổi nữa.
    public class CcuConnection : IDisposable
    {
        private readonly string _host;
        private readonly int    _port;
        private readonly int    _liveIntervalMs;
        private readonly int    _zcuQuaHanMs;

        private TcpClient     _client;
        private NetworkStream _stream;
        private readonly byte[] _buf = new byte[16384];
        private int  _len;
        private DateTime _liveKeUtc;
        private DateTime _goiDuLieuCuoiUtc;

        private static readonly byte[] LenhLive =
            Encoding.ASCII.GetBytes("$CCU,01,LIVE*42#");

        // Bảng ZCU dùng chung giữa luồng đọc và luồng web. Khoá khi đọc/ghi:
        // đây là cấu trúc duy nhất hai luồng cùng chạm.
        private readonly object _khoa = new object();
        private readonly Dictionary<int, CcuZcuState> _zcus = new Dictionary<int, CcuZcuState>();

        public string Host { get { return _host; } }
        public int    Port { get { return _port; } }

        public bool      IsOnline  { get; private set; }
        public string    LastError { get; private set; }
        public DateTime? CcuTraLoiUtc { get; private set; }   // từ $CCU,01,OK

        public long GoiDuLieu      { get; private set; }
        public long KhungSaiCrc    { get; private set; }
        public long KhungNhipSong  { get; private set; }
        public long KhungBoQuaKhac { get; private set; }

        // Giãn nhịp khi nối được mà không có dữ liệu. Không dùng thang tăng dần
        // như đường ZCU: CCU chỉ có một con, nối lại sau 5 giây không tốn gì.
        private DateTime _nextTryUtc = DateTime.MinValue;
        private const int ThuLaiMs = 5000;

        // Quá 30 giây không có gói dữ liệu nào thì coi như phiên hỏng và nối lại.
        // Ngưỡng lấy đúng theo tài liệu mục 2.2.1. KHÔNG dùng timeout đọc làm dấu
        // hiệu chết: gói về theo vòng quay từng ZCU, khe rỗng vài giây là bình
        // thường (đo được 2,50 giây giữa hai gói của cùng một ZCU).
        private const int ImLangToiDaMs = 30000;

        public CcuConnection(string host, int port, int liveIntervalMs, int zcuQuaHanMs)
        {
            _host           = host;
            _port           = port;
            _liveIntervalMs = Math.Max(1000, liveIntervalMs);
            _zcuQuaHanMs    = Math.Max(1000, zcuQuaHanMs);
        }

        public IList<CcuZcuState> Snapshot()
        {
            lock (_khoa)
            {
                return _zcus.Values
                            .Select(z => new CcuZcuState
                            {
                                ZcuId            = z.ZcuId,
                                DangKetNoi       = z.DangKetNoi,
                                Trong            = z.Trong,
                                CoXe             = z.CoXe,
                                Loi              = z.Loi,
                                KhongLap         = z.KhongLap,
                                KhongHieu        = z.KhongHieu,
                                LanCuoiCoGoiUtc  = z.LanCuoiCoGoiUtc,
                                LanCuoiKetNoiUtc = z.LanCuoiKetNoiUtc,
                                SoGoi            = z.SoGoi
                            })
                            .OrderBy(z => z.ZcuId)
                            .ToList();
            }
        }

        public int ZcuQuaHanGiay { get { return _zcuQuaHanMs / 1000; } }

        // Một nhịp của vòng đọc. Gọi liên tục từ luồng riêng của CCU.
        public void Poll(int readTimeoutMs)
        {
            try
            {
                if (_client == null || !_client.Connected)
                {
                    if (DateTime.UtcNow < _nextTryUtc) return;
                    Connect(readTimeoutMs);
                    if (_client == null) return;
                }

                GuiLiveNeuDenHan();

                // GuiLiveNeuDenHan có thể đã Drop khi ghi hỏng, khi đó _stream về
                // null. Không kiểm ở đây thì dòng dưới ném NullReference và lỗi
                // socket thật bị thay bằng một thông báo vô nghĩa — đúng lúc cần
                // biết nguyên nhân nhất.
                if (_stream == null) return;

                int n;
                try
                {
                    n = _stream.Read(_buf, _len, _buf.Length - _len);
                }
                catch (System.IO.IOException ex)
                {
                    // CHỈ hết thời gian chờ đọc mới là chuyện bình thường: gói về
                    // theo vòng quay từng ZCU nên khe rỗng vài giây là đương nhiên.
                    //
                    // Mọi IOException khác (peer RST, đứt cáp, socket bị huỷ) là
                    // hỏng thật. Gộp chung thì vòng lặp quay lại Read rồi ném tiếp
                    // ~50 lần mỗi giây, trong khi endpoint vẫn báo ccu_online = true
                    // suốt tới 30 giây.
                    var se = ex.InnerException as SocketException;
                    if (se == null || se.SocketErrorCode != SocketError.TimedOut)
                    {
                        Drop(ex.Message);
                        return;
                    }
                    KiemImLang();
                    return;
                }

                if (n <= 0) { Drop("CCU dong ket noi"); return; }

                _len += n;
                TachKhung();
                KiemImLang();

                if (_len > _buf.Length - 512)
                {
                    // Đệm đầy mà không tách được khung nào -> dòng byte rác.
                    // Bỏ hết: mỗi gói mang trạng thái đầy đủ nên không mất gì.
                    _len = 0;
                }
            }
            catch (Exception ex) { Drop(ex.Message); }
        }

        private void GuiLiveNeuDenHan()
        {
            if (DateTime.UtcNow < _liveKeUtc) return;
            try
            {
                _stream.Write(LenhLive, 0, LenhLive.Length);
                _liveKeUtc = DateTime.UtcNow.AddMilliseconds(_liveIntervalMs);
            }
            catch (Exception ex) { Drop(ex.Message); }
        }

        private void KiemImLang()
        {
            if (_goiDuLieuCuoiUtc == DateTime.MinValue) return;
            if ((DateTime.UtcNow - _goiDuLieuCuoiUtc).TotalMilliseconds > ImLangToiDaMs)
                Drop("CCU ngung day du lieu qua " + (ImLangToiDaMs / 1000) + " giay");
        }

        private void Connect(int timeoutMs)
        {
            try
            {
                _client = new TcpClient();
                var ar = _client.BeginConnect(_host, _port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    _client.Close(); _client = null;
                    Fail("het thoi gian ket noi");
                    _nextTryUtc = DateTime.UtcNow.AddMilliseconds(ThuLaiMs);
                    return;
                }
                _client.EndConnect(ar);

                // Thời gian chờ đọc ngắn hơn nhịp LIVE, nếu không thì một khe
                // rỗng dài sẽ chiếm trọn vòng và lệnh LIVE bị gửi trễ.
                _client.ReceiveTimeout = Math.Max(500, _liveIntervalMs / 2);
                _stream = _client.GetStream();
                _len = 0;
                _liveKeUtc        = DateTime.MinValue;   // gửi LIVE ngay
                _goiDuLieuCuoiUtc = DateTime.UtcNow;
                IsOnline  = true;
                LastError = null;
            }
            catch (Exception ex)
            {
                if (_client != null) { _client.Close(); _client = null; }
                Fail(ex.Message);
                _nextTryUtc = DateTime.UtcNow.AddMilliseconds(ThuLaiMs);
            }
        }

        // Tách các khung "$...#" khỏi dòng byte rồi nạp từng khung.
        private void TachKhung()
        {
            int doc = 0;
            while (true)
            {
                int mo = Array.IndexOf(_buf, (byte)'$', doc, _len - doc);
                if (mo < 0) break;
                int dong = Array.IndexOf(_buf, (byte)'#', mo + 1, _len - mo - 1);
                if (dong < 0) { doc = mo; break; }

                string khung = Encoding.ASCII.GetString(_buf, mo, dong - mo + 1);
                Nap(khung);
                doc = dong + 1;
            }

            if (doc > 0)
            {
                Array.Copy(_buf, doc, _buf, 0, _len - doc);
                _len -= doc;
            }
        }

        public void Nap(string khung)
        {
            CcuFrame f;
            CcuLyDoLoai lyDo;
            if (CcuFrame.TryParse(khung, out f, out lyDo))
            {
                GoiDuLieu++;
                _goiDuLieuCuoiUtc = DateTime.UtcNow;
                GhiZcu(f);
                return;
            }

            switch (lyDo)
            {
                case CcuLyDoLoai.SaiCrc:   KhungSaiCrc++;   break;
                case CcuLyDoLoai.NhipSong:
                    KhungNhipSong++;
                    CcuTraLoiUtc = DateTime.UtcNow;
                    break;
                default: KhungBoQuaKhac++; break;
            }
        }

        private void GhiZcu(CcuFrame f)
        {
            lock (_khoa)
            {
                CcuZcuState z;
                if (!_zcus.TryGetValue(f.ZcuId, out z))
                {
                    z = new CcuZcuState { ZcuId = f.ZcuId };
                    _zcus[f.ZcuId] = z;
                }

                z.DangKetNoi      = f.ZcuDangKetNoi;
                z.Trong           = f.Trong;
                z.CoXe            = f.CoXe;
                z.Loi             = f.Loi;
                z.KhongLap        = f.KhongLap;
                z.KhongHieu       = f.KhongHieu;
                z.LanCuoiCoGoiUtc = DateTime.UtcNow;
                z.SoGoi++;

                if (f.ZcuDangKetNoi) z.LanCuoiKetNoiUtc = DateTime.UtcNow;
            }
        }

        private void Fail(string msg)
        {
            IsOnline  = false;
            LastError = msg;
        }

        private void Drop(string msg)
        {
            // Đóng đàng hoàng: gửi FIN chứ không RST. Đo được CCU hoàn tất bắt
            // tay đóng bình thường, khác với ZCU — nên không cần Linger 0 ở đây.
            try { if (_stream != null) _stream.Close(); } catch { }
            try { if (_client != null) _client.Close(); } catch { }
            _stream = null;
            _client = null;
            _len = 0;
            _goiDuLieuCuoiUtc = DateTime.MinValue;
            _nextTryUtc = DateTime.UtcNow.AddMilliseconds(ThuLaiMs);
            if (msg != null) Fail(msg);
        }

        public void Dispose() { Drop(null); }
    }
}
