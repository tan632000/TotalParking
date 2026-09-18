using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services.Pgs;

namespace TotalParking.Controllers
{
    // Giám sát tầng cảm biến đỗ thường (PGS/ZCU).
    //
    // Chỉ số đáng chú ý nhất là `nhieu_bi_loai`: số lần một khung bị bộ lọc chống
    // rung từ chối. Con số đó cao ở một ZCU nghĩa là thiết bị đó có cảm biến hỏng,
    // không phải bãi xe bận. Đo được ZCU 2 sinh 4.121 lần đổi một ngày trong khi
    // ZCU 3 không đổi lần nào.
    public class PgsStatusController : Controller
    {
        // GET /PgsStatus
        public ActionResult Index()
        {
            var conns = PgsHost.Connections.ToList();

            return Json2(200, new
            {
                now         = DateTime.Now.ToString("HH:mm:ss"),
                enabled     = PgsHost.Enabled,
                running     = PgsHost.IsRunning,
                debounce_ms = PgsHost.DebounceMs,
                port        = PgsHost.Port,
                // Cộng ba trạng thái trên cả 5 ZCU.
                //
                // `co_xe` là con số quan trọng nhất, nhưng CHƯA được dùng để trừ
                // vào số trên bảng LED, vì hai lý do:
                //   1. Chiều của trạng thái (bit dịch = có xe) mới là suy luận,
                //      chưa có phép thử vật lý xác nhận.
                //   2. Chưa biết cảm biến nào thuộc khu nào, nên không chia được
                //      theo zone — mà bảng chỉ hướng cần số theo zone.
                //
                // Đối chiếu với thực tế ngoài bãi là cách nhanh nhất để kiểm cả hai.
                chua_lap_hoac_loi = conns.Where(c => c.Stable != null).Sum(c => c.Stable.NotInstalled),
                khong_co_xe       = conns.Where(c => c.Stable != null).Sum(c => c.Stable.Free),
                co_xe             = conns.Where(c => c.Stable != null).Sum(c => c.Stable.Occupied),
                da_lap            = conns.Where(c => c.Stable != null).Sum(c => c.Stable.Installed),
                khong_giai_duoc   = conns.Where(c => c.Stable != null).Sum(c => c.Stable.Unknown),
                zcus = conns.Select(c => new
                {
                    host      = c.Host,
                    zcu_id    = c.ZcuId,
                    online    = c.IsOnline,
                    error     = c.LastError,
                    // Giây kể từ khung cuối. ZCU tự đóng socket mỗi 60 giây rồi
                    // mình nối lại, nên giá trị này bình thường phải dưới vài giây.
                    stale_s   = c.LastFrameUtc.HasValue
                                  ? (int)(DateTime.UtcNow - c.LastFrameUtc.Value).TotalSeconds
                                  : (int?)null,
                    khung_da_doc  = c.FrameCount,
                    doi_da_nhan   = c.ChangeCount,
                    nhieu_bi_loai = c.RejectedNoise,
                    chua_lap_hoac_loi = c.Stable == null ? (int?)null : c.Stable.NotInstalled,
                    khong_co_xe       = c.Stable == null ? (int?)null : c.Stable.Free,
                    co_xe             = c.Stable == null ? (int?)null : c.Stable.Occupied,
                    da_lap            = c.Stable == null ? (int?)null : c.Stable.Installed,
                    khong_giai_duoc   = c.Stable == null ? (int?)null : c.Stable.Unknown,
                    deviant_bytes = c.Stable == null ? (int?)null : c.Stable.DeviantBytes,
                    phase         = c.Stable == null ? (int?)null : c.Stable.Phase,
                    body_hex      = c.Stable == null ? null : c.Stable.BodyHex()
                }).ToArray()
            });
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
