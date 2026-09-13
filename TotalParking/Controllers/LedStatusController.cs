using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;
using TotalParking.Services.Led;

namespace TotalParking.Controllers
{
    // Giám sát và nghiệm thu tầng LED.
    //
    //   Index  trạng thái từng bảng + số chỗ trống đang được đẩy
    //   Send   gửi một khung tuỳ ý, phục vụ nghiệm thu
    //   Blank  xoá một bảng về trạng thái trống
    //
    // Chỉ số quan trọng nhất ở Index là `stale_seconds`. Board giữ nội dung cuối
    // vĩnh viễn và không có watchdog, nên một tấm bảng "online = false" vẫn đang
    // hiện số — chỉ là số của mấy phút trước. Không có cột này thì không cách nào
    // phân biệt bảng đang đúng với bảng đang nói dối.
    public class LedStatusController : Controller
    {
        private static readonly LedPanelRepository _repo = new LedPanelRepository();

        // Ngưỡng coi là đáng ngờ: 3 nhịp liên tiếp không đẩy được.
        private const int StaleWarnSeconds = 30;

        // GET /LedStatus
        public ActionResult Index()
        {
            var pub = LedHost.Publisher;

            if (pub == null)
            {
                return Json2(200, new
                {
                    enabled = LedHost.Enabled,
                    running = false,
                    note = "Chua nap duoc danh muc bang LED: kiem tra connection string "
                         + "va da chay 09_led_panel.sql chua."
                });
            }

            object capacity;
            try
            {
                var c = _repo.GetCapacity();
                capacity = new
                {
                    free_l5m      = c.FreeL5m,      total_l5m      = c.TotalL5m,
                    free_l48m     = c.FreeL48m,     total_l48m     = c.TotalL48m,
                    free_standard = c.FreeStandard, total_standard = c.TotalStandard,
                    free_total    = c.FreeTotal,
                    // Phien dang mo nhung chua biet block — khong tru vao bo dem nao.
                    used_unassigned = c.UsedUnassigned
                };
            }
            catch (Exception ex)
            {
                capacity = new { error = ex.Message };
            }

            return Json2(200, new
            {
                enabled     = LedHost.Enabled,
                running     = pub.IsRunning,
                interval_ms = pub.IntervalMs,
                manual_send_allowed = LedHost.AllowManualSend,
                now = DateTime.Now.ToString("HH:mm:ss"),
                capacity,
                panels = pub.States.Select(s => new
                {
                    code     = s.Panel.Code,
                    endpoint = s.Panel.Endpoint,
                    kind     = s.Panel.Kind,
                    online   = s.Online,
                    // null = chua bao gio day duoc. Lon hon 30s = dang nghi ngo.
                    stale_seconds = s.StaleSeconds,
                    suspect  = !s.StaleSeconds.HasValue || s.StaleSeconds.Value > StaleWarnSeconds,
                    sent     = s.SentCount,
                    failed   = s.FailCount,
                    last_frame = s.LastFrame,
                    last_ack   = s.LastAck,
                    error      = s.LastError,
                    ports = s.Panel.Ports.Select(p => new
                    {
                        index     = p.PortIndex,
                        scope     = p.Scope,
                        zone_list = p.ZoneList,
                        active    = p.IsActive,
                        // Cong ZONES chua biet dan toi dau thi bi bo qua khi day.
                        skipped   = p.Scope == "ZONES" && string.IsNullOrWhiteSpace(p.ZoneList)
                    }).ToArray()
                }).ToArray()
            });
        }

        // POST /LedStatus/Send
        //   panel=50&port=0&dir=1&color=2&state=0&n1=12&n2=34&n3=56
        //
        // Gửi thẳng một khung, bỏ qua số liệu thật. Dùng để nghiệm thu bảng ngoài
        // hiện trường: xác nhận đúng bảng, đúng cổng, đúng vị trí ba con số.
        [HttpPost]
        public ActionResult Send(string panel, int port = 0, int dir = 0, int color = 2,
                                 int state = 0, int n1 = 0, int n2 = 0, int n3 = 0)
        {
            if (!IsLoopback()) return Json2(403, new { error = "Chi chap nhan tu loopback." });
            if (!LedHost.AllowManualSend)
                return Json2(403, new { error = "led:allowManualSend = false trong Web.config." });

            var pub = LedHost.Publisher;
            if (pub == null) return Json2(503, new { error = "Tang LED chua nap duoc." });

            var target = pub.States.FirstOrDefault(s => s.Panel.Code == panel);
            if (target == null) return Json2(404, new { error = "Khong co bang LED '" + panel + "'." });

            if (port < 0 || port > 3) return Json2(400, new { error = "port phai trong 0..3" });

            var hub = new LedHub { Port = port };
            hub.Arrow.Direction = (LedDirection)Clamp(dir, 0, 3);
            hub.Arrow.Color     = (LedColor)Clamp(color, 0, 3);
            hub.Arrow.State     = (LedArrowState)Clamp(state, 0, 1);
            hub.Position1.Value = n1;
            hub.Position2.Value = n2;
            hub.Position3.Value = n3;

            try
            {
                string ack = pub.SendRaw(target.Panel.PanelId, hub);
                return Json2(200, new
                {
                    panel, port,
                    frame = hub.GetCommand(),
                    ack,
                    ack_valid = LedSocket.IsAckFor(ack, port),
                    note = "Thu tu ba so: n1 = L<5M, n2 = L<4.8M, n3 = Standard."
                });
            }
            catch (Exception ex)
            {
                return Json2(502, new { error = ex.Message });
            }
        }

        // POST /LedStatus/Blank  — xoá một bảng về trạng thái trống.
        [HttpPost]
        public ActionResult Blank(string panel, int port = 0)
        {
            if (!IsLoopback()) return Json2(403, new { error = "Chi chap nhan tu loopback." });
            if (!LedHost.AllowManualSend)
                return Json2(403, new { error = "led:allowManualSend = false trong Web.config." });

            var pub = LedHost.Publisher;
            if (pub == null) return Json2(503, new { error = "Tang LED chua nap duoc." });

            var target = pub.States.FirstOrDefault(s => s.Panel.Code == panel);
            if (target == null) return Json2(404, new { error = "Khong co bang LED '" + panel + "'." });

            try
            {
                string ack = pub.SendRaw(target.Panel.PanelId, LedHub.Blank(port));
                return Json2(200, new { panel, port, ack, blanked = true });
            }
            catch (Exception ex)
            {
                return Json2(502, new { error = ex.Message });
            }
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        private bool IsLoopback()
        {
            if (Request == null || Request.UserHostAddress == null) return false;
            IPAddress ip;
            return IPAddress.TryParse(Request.UserHostAddress, out ip) && IPAddress.IsLoopback(ip);
        }

        private ActionResult Json2(int status, object payload)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return new ContentResult
            {
                ContentType = "application/json",
                Content = JsonConvert.SerializeObject(payload, Formatting.Indented),
                ContentEncoding = Encoding.UTF8
            };
        }
    }
}
