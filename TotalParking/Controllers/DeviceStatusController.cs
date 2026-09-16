using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // Trạng thái sống của thiết bị ngoại vi, cho khối "Trạng thái truyền thông"
    // ở trang Điều hướng.
    //
    // Chỉ đọc và chỉ mở TCP rồi đóng — xem DeviceProbeService.
    public class DeviceStatusController : Controller
    {
        private static readonly DeviceProbeService _probe = new DeviceProbeService();

        // GET /DeviceStatus/Summary
        public ActionResult Summary()
        {
            try
            {
                var groups = _probe.GetSummary();

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("HH:mm:ss"),
                    groups = groups.Select(g => new
                    {
                        key       = g.Key,
                        name      = g.Name,
                        // "unmonitored" | "empty" | "down" | "partial" | "ok"
                        // Giao diện phải phân biệt unmonitored (xám) với down (đỏ):
                        // một cái là hệ thống không biết, cái kia là thiết bị hỏng.
                        health    = g.Health,
                        monitored = g.Monitored,
                        total     = g.Total,
                        online    = g.Online,
                        detail    = g.Detail,
                        last_seen = g.LastSeen.HasValue
                                        ? g.LastSeen.Value.ToString("dd-MM-yyyy HH:mm:ss")
                                        : null,
                        offline   = g.Offline.ToArray()
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        private ActionResult Json2(int status, object payload)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return new ContentResult
            {
                ContentType     = "application/json",
                Content         = JsonConvert.SerializeObject(payload),
                ContentEncoding = Encoding.UTF8
            };
        }
    }
}
