using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services.Pgs;

namespace TotalParking.Controllers
{
    // Giám sát tầng cảm biến đỗ thường, đọc qua CCU.
    //
    // ===================== VÌ SAO KHÔNG CÓ SỐ TỔNG GỘP =====================
    // Tài liệu nhà cung cấp (mục 2.2.2) cảnh báo: "Trạng thái cảm biến sẽ được
    // giữ nguyên khi ZCU mất kết nối". Nghĩa là CCU vẫn đều đặn đẩy số của một
    // ZCU đã chết, và số đó là ảnh chụp cũ không rõ từ bao giờ.
    //
    // Nếu cộng chung vào một con số thì số đóng băng trông y hệt số mới — kiểu
    // sai khó phát hiện nhất, vì không có gì bất thường để mà chú ý. Nên ở đây
    // hai nhóm tách hẳn nhau và KHÔNG có con số gộp nào để ai đó trích ra dùng.
    public class PgsStatusController : Controller
    {
        // GET /PgsStatus
        public ActionResult Index()
        {
            var ccu = PgsHost.Ccu;

            if (ccu == null)
            {
                return Json2(200, new
                {
                    now     = DateTime.Now.ToString("HH:mm:ss"),
                    enabled = PgsHost.Enabled,
                    running = PgsHost.IsRunning,
                    ghi_chu = "Chua cau hinh pgs:ccuHost hoac pgs:enabled = false."
                });
            }

            var zcus = ccu.Snapshot();
            int quaHan = ccu.ZcuQuaHanGiay;

            // Chốt mốc thời gian MỘT LẦN cho cả lượt trả về.
            //
            // TuoiGoiGiay gọi DateTime.UtcNow mỗi lần đọc, nên hai lượt duyệt cách
            // nhau vài mili giây có thể cho hai kết quả khác nhau. Một ZCU đứng
            // đúng mốc ngưỡng sẽ lọt vào CẢ nhóm sống lẫn nhóm đóng băng, và số
            // của nó bị cộng hai lần — đúng thứ AC-02 cấm.
            var moc = DateTime.UtcNow;
            var traLoi = ccu.CcuTraLoiUtc;
            var tuoiGoi = zcus.ToDictionary(
                z => z.ZcuId,
                z => (int)(moc - z.LanCuoiCoGoiUtc).TotalSeconds);
            var tuoiKetNoi = zcus.ToDictionary(
                z => z.ZcuId,
                z => z.LanCuoiKetNoiUtc.HasValue
                        ? (int)(moc - z.LanCuoiKetNoiUtc.Value).TotalSeconds
                        : (int?)null);

            // Một ZCU chỉ được tính vào nhóm sống khi CCU nói nó đang kết nối
            // VÀ gói của nó còn mới. Hai điều kiện, không phải một: CCU có thể
            // bị rút mạng qua switch mà không đóng socket, khi đó X3 cuối cùng
            // vẫn là 1 trong khi thực tế đã đứng hình từ lâu.
            Func<CcuZcuState, bool> conSong = z => z.DangKetNoi && tuoiGoi[z.ZcuId] <= quaHan;
            var song = zcus.Where(conSong).ToList();
            var chet = zcus.Where(z => !conSong(z)).ToList();

            return Json2(200, new
            {
                now              = DateTime.Now.ToString("HH:mm:ss"),
                enabled          = PgsHost.Enabled,
                running          = PgsHost.IsRunning,
                ccu_host         = ccu.Host + ":" + ccu.Port,
                ccu_online       = ccu.IsOnline,
                ccu_loi          = ccu.LastError,
                // Chép ra biến cục bộ trước khi dùng: DateTime? là struct, ghi
                // không nguyên tử, và luồng nền có thể ghi giữa hai lần đọc.
                ccu_tra_loi_cach_day_giay = traLoi.HasValue
                    ? (int)(moc - traLoi.Value).TotalSeconds
                    : (int?)null,
                nguong_qua_han_giay = quaHan,

                goi_du_lieu        = ccu.GoiDuLieu,
                khung_sai_crc      = ccu.KhungSaiCrc,
                khung_nhip_song    = ccu.KhungNhipSong,
                khung_bo_qua_khac  = ccu.KhungBoQuaKhac,

                dang_ket_noi = Gop(song),
                dong_bang    = Gop(chet),

                zcus = zcus.Select(z => new
                {
                    zcu_id        = z.ZcuId,
                    dang_ket_noi  = z.DangKetNoi,
                    qua_han       = tuoiGoi[z.ZcuId] > quaHan,
                    tuoi_goi_s    = tuoiGoi[z.ZcuId],
                    // Giây kể từ lần cuối CCU báo ZCU này ĐANG kết nối. null =
                    // chưa lần nào kết nối kể từ khi site khởi động.
                    tuoi_ket_noi_s = tuoiKetNoi[z.ZcuId],
                    so_goi        = z.SoGoi,
                    khong_co_xe   = z.Trong,
                    co_xe         = z.CoXe,
                    loi           = z.Loi,
                    khong_lap     = z.KhongLap,
                    da_lap        = z.DaLap,
                    // Khác 0 nghĩa là firmware trả ký tự ngoài bảng 0..3 đã biết.
                    khong_hieu    = z.KhongHieu
                }).ToArray()
            });
        }

        private static object Gop(IList<CcuZcuState> ds)
        {
            return new
            {
                so_zcu      = ds.Count,
                zcu_ids     = ds.Select(z => z.ZcuId).ToArray(),
                khong_co_xe = ds.Sum(z => z.Trong),
                co_xe       = ds.Sum(z => z.CoXe),
                loi         = ds.Sum(z => z.Loi),
                khong_lap   = ds.Sum(z => z.KhongLap),
                khong_hieu  = ds.Sum(z => z.KhongHieu),
                da_lap      = ds.Sum(z => z.DaLap)
            };
        }

        // POST /PgsStatus/TuKiem  — chỉ nhận từ máy cục bộ.
        //
        // Nạp một chuỗi khung vào ĐÚNG bộ giải mã mà vòng chạy dùng, rồi trả về
        // kết quả phân loại. Có nó thì phép kiểm chứng mới chạm được vào mã C#:
        // nếu không, một script bên ngoài chỉ kiểm được hàm CRC của chính nó, và
        // một bộ giải mã bỏ qua CRC hoàn toàn vẫn "đạt".
        //
        // Không chạm thiết bị, không đổi trạng thái đang công bố: mỗi khung được
        // giải bằng CcuFrame.TryParse rồi vứt đi.
        [HttpPost]
        public ActionResult TuKiem(string khung)
        {
            if (!Request.IsLocal) return Json2(403, new { loi = "Chi cho phep tu may cuc bo." });

            var dsKhung = new List<string>();
            if (!string.IsNullOrEmpty(khung)) dsKhung.Add(khung);

            // Cho phép gửi nhiều khung một lần: mỗi dòng một khung.
            string body = null;
            try
            {
                Request.InputStream.Position = 0;
                using (var r = new System.IO.StreamReader(Request.InputStream, Encoding.UTF8))
                    body = r.ReadToEnd();
            }
            catch (Exception) { }

            if (!string.IsNullOrWhiteSpace(body))
            {
                // Chặn số khung: body mặc định được phép tới 4 MB, mà mỗi khung
                // lại được trả lại nguyên văn trong JSON. Loopback nên rủi ro
                // gần bằng 0, nhưng một dòng chặn thì đóng hẳn.
                foreach (var dong in body.Split('\n').Take(200))
                {
                    string d = dong.Trim();
                    if (d.StartsWith("$")) dsKhung.Add(d);
                }
            }

            var ketQua = dsKhung.Select(k =>
            {
                CcuFrame f;
                CcuLyDoLoai lyDo;
                bool ok = CcuFrame.TryParse(k, out f, out lyDo);
                return new
                {
                    khung   = k,
                    hop_le  = ok,
                    ly_do   = lyDo.ToString(),
                    zcu_id  = ok ? f.ZcuId : (int?)null,
                    dang_ket_noi = ok ? f.ZcuDangKetNoi : (bool?)null,
                    khong_co_xe  = ok ? f.Trong : (int?)null,
                    co_xe        = ok ? f.CoXe : (int?)null,
                    loi          = ok ? f.Loi : (int?)null,
                    khong_lap    = ok ? f.KhongLap : (int?)null,
                    khong_hieu   = ok ? f.KhongHieu : (int?)null
                };
            }).ToArray();

            return Json2(200, new { so_khung = ketQua.Length, ket_qua = ketQua });
        }

        private ActionResult Json2(int status, object body)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(body, Formatting.Indented),
                           "application/json", Encoding.UTF8);
        }
    }
}
