using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // Snapshot trạng thái điều hướng cho trang mặt bằng (Home/FloorPlan).
    //
    // Một endpoint duy nhất trả cả sức chứa lẫn danh sách xe vừa vào, thay vì
    // hai lời gọi riêng: trang cần hai thứ đó khớp nhau tại cùng một thời điểm.
    // Gọi riêng thì sẽ có lúc bảng xe đã hiện xe mới mà con số mật độ vẫn là của
    // nhịp trước — giao diện hiển thị một trạng thái chưa từng tồn tại.
    public class MonitorController : Controller
    {
        private static readonly VehicleRoutingRepository _routings =
            new VehicleRoutingRepository();

        // Số dòng xe gần nhất. Đủ để nhìn thấy luồng, không đủ để thành báo cáo —
        // tra cứu lịch sử là việc của tab Báo cáo.
        private const int RecentLimit = 12;

        // GET /Monitor/RoutingState
        public ActionResult RoutingState()
        {
            try
            {
                var zones  = _routings.GetZoneCapacity();
                var recent = _routings.GetRecent(RecentLimit);

                // Xe mới nhất mà thật sự có zone — đây là zone được tô sáng trên
                // sơ đồ. Xe bị REJECTED / NO_CAPACITY không tô gì, vì tô một zone
                // cho chiếc xe không vào được zone đó là thông tin sai.
                var latest = recent.FirstOrDefault(v => v.ZoneId.HasValue);

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("HH:mm:ss"),
                    latest_zone     = latest == null ? (int?)null : latest.ZoneId,
                    latest_event_id = latest == null ? null : latest.EventId,
                    zones = zones.Select(z => new
                    {
                        zone_id    = z.ZoneId,
                        code       = z.Code,
                        gate_rank  = z.GateRank,
                        total      = z.Total,
                        in_use     = z.InUse,
                        free_mech  = z.FreeMechanical,
                        free_tier0 = z.FreeTier0,
                        free_ground = z.FreeGround,
                        used_pct   = z.Total == 0 ? 0 : (int)Math.Round(z.UsedRatio * 100)
                    }).ToArray(),
                    recent = recent.Select(v => new
                    {
                        event_id     = v.EventId,
                        at           = v.ReceivedAt.ToString("HH:mm:ss"),
                        vehicle      = Describe(v.Make, v.Model),
                        category     = v.Category,
                        lane         = v.Lane,
                        weight_class = v.WeightClass,
                        length_mm    = v.LengthMm,
                        outcome      = v.Outcome,
                        reason       = v.Reason,
                        zone_id      = v.ZoneId,
                        zone_code    = v.ZoneCode
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                // Chưa chạy 05/07/08_*.sql thì các view chưa tồn tại. Trả lỗi có
                // nội dung thay vì để trang mặt bằng nhận HTML lỗi của IIS rồi
                // chết ở JSON.parse.
                return Json2(503, new { error = ex.Message });
            }
        }

        private static string Describe(string make, string model)
        {
            if (string.IsNullOrEmpty(make) && string.IsNullOrEmpty(model)) return null;
            return (make + " " + model).Trim();
        }

        private ActionResult Json2(int status, object payload)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return new ContentResult
            {
                ContentType = "application/json",
                Content = JsonConvert.SerializeObject(payload),
                ContentEncoding = Encoding.UTF8
            };
        }
    }
}
