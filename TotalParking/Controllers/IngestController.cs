using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TotalParking.Models;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // Tiếp nhận dữ liệu xe từ app Camera AI (docs/camera-led-routing-design.md §3).
    // Camera AI là client chạy cùng máy, chỉ gọi qua loopback. Hai path bắt buộc nằm ở gốc site
    // vì app camera chỉ cấu hình được IP và port, không cấu hình được path.
    //
    // Giai đoạn 1: chỉ nhận và ghi log. Phân loại (§4), phân bổ block (§5) và điều hướng LED (§7)
    // là các giai đoạn sau.
    public class IngestController : Controller
    {
        private const string LogFolder = "~/App_Data/ingest";

        private static readonly object LogLock = new object();

        // Không BOM: log là NDJSON append-only, ba byte BOM ở đầu file làm dòng đầu tiên
        // không parse được bằng trình đọc JSON chặt.
        private static readonly Encoding LogEncoding = new UTF8Encoding(false);

        // File log có thể bị chiếm trong thời gian ngắn: lúc app pool recycle (lịch 03:00) worker
        // cũ và worker mới cùng sống tới 90 giây, và antivirus cũng quét file vừa ghi. Cả hai đều
        // tự hết sau vài chục ms, nên thử lại rẻ hơn nhiều so với trả 500 và bắt camera gửi lại.
        private const int WriteAttempts = 3;
        private const int WriteRetryDelayMs = 50;

        // Phơi ra cho /api/monitor/state ở giai đoạn 6. Chỉ ghi bộ nhớ, không I/O.
        private static readonly VehicleEventRepository   _events     = new VehicleEventRepository();
        private static readonly VehicleProfileRepository _profiles   = new VehicleProfileRepository();
        private static readonly VehicleClassifier        _classifier = new VehicleClassifier();

        public static DateTime? LastHealthUtc;
        public static DateTime? LastVehicleUtc;

        // Camera AI gọi 5 giây/lần, 24/7 — khoảng 17.280 lần mỗi ngày. Không lock, không chạm
        // đĩa, không chạm socket LED: một /health chậm bị camera hiểu là hệ thống đang chết.
        [HttpGet]
        public ActionResult Health()
        {
            if (!IsLoopbackClient(Request)) return Forbidden();

            LastHealthUtc = DateTime.UtcNow;
            return JsonText(200, "{\"status\":\"ok\"}");
        }

        [HttpPost]
        public ActionResult Vehicle()
        {
            if (!IsLoopbackClient(Request))
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Ingest: tu choi POST /vehicle tu " + Request.UserHostAddress + " (khong phai loopback).");
                return Forbidden();
            }

            string body;
            try
            {
                Request.InputStream.Position = 0;
                using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                {
                    // Đọc UTF-8: trường category có dấu tiếng Việt.
                    body = reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Ingest: khong doc duoc body. " + ex);
                return JsonText(500, "{\"status\":\"error\"}");
            }

            // Parse dễ dãi: không có type dùng chung với repo camera, nên trường lạ phải bỏ qua
            // chứ không được làm ta sập.
            JObject payload = null;
            string eventId = null;
            try
            {
                payload = JObject.Parse(body);
                eventId = (string)payload["event_id"];
            }
            catch (JsonException)
            {
                // Body hỏng là bug phía camera, gửi lại cũng không sửa được. 400 để lộ ra sớm.
            }

            if (string.IsNullOrEmpty(eventId))
            {
                System.Diagnostics.Trace.TraceWarning("Ingest: POST /vehicle thieu event_id.");
                return JsonText(400, "{\"status\":\"error\"}");
            }

            try
            {
                AppendLog(eventId, body);
            }
            catch (Exception ex)
            {
                // Tài liệu: trả 2xx nghĩa là "đã nhận và lưu xong". Chưa lưu được thì phải để
                // camera gửi lại, và một lỗi quyền ghi App_Data cần lộ ra thật to.
                System.Diagnostics.Trace.TraceError("Ingest: khong ghi duoc log su kien " + eventId + ". " + ex);
                return JsonText(500, "{\"status\":\"error\"}");
            }

            // NDJSON ghi truoc: no la bang chung tho, phai con lai ke ca khi DB hong.
            // Sau do moi ghi DB, va DB moi la thu quyet dinh 2xx — tai lieu dinh nghia
            // 2xx la "da nhan va luu xong". Hong DB thi tra 500 de camera gui lai;
            // event_id la khoa chinh nen lan gui lai khong tao ban ghi thu hai.
            var vehicle = ToVehicleEvent(payload, eventId, body);

            try
            {
                _events.Insert(vehicle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "Ingest: khong ghi duoc su kien " + eventId + " vao database. " + ex);
                return JsonText(500, "{\"status\":\"error\"}");
            }

            // Phan loai la du lieu dan xuat: tinh lai duoc bat cu luc nao tu su kien
            // tho da luu. Nen loi o day KHONG duoc lam hong 2xx — bat camera gui lai
            // ca chiec xe chi vi mot phep tinh co the chay lai la sai huong.
            try
            {
                _profiles.Save(_classifier.Classify(vehicle));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "Ingest: khong phan loai/luu duoc ho so cho " + eventId + ". " + ex);
            }

            LastVehicleUtc = DateTime.UtcNow;
            return JsonText(200, "{\"status\":\"ok\"}");
        }

        // Chuan hoa payload. Camera dung 0 va "Unknown" lam gia tri "khong biet";
        // ca hai deu thanh null o day de moi truy van ve sau khong phai nho luat do.
        private static VehicleEvent ToVehicleEvent(JObject p, string eventId, string rawBody)
        {
            return new VehicleEvent
            {
                EventId         = eventId,
                ReceivedAt      = DateTime.Now,
                CameraTimestamp = Moment(p, "timestamp"),
                Make            = Text(p, "make"),
                Model           = Text(p, "model"),
                YearRange       = Text(p, "year"),
                LengthMm        = Measure(p, "length_mm"),
                WidthMm         = Measure(p, "width_mm"),
                HeightMm        = Measure(p, "height_mm"),
                WeightKg        = Measure(p, "weight_kg"),
                Category        = Text(p, "category"),
                ImagePath       = Text(p, "image_path"),
                RawBody         = rawBody
            };
        }

        private static string Text(JObject p, string name)
        {
            var token = p[name];
            if (token == null || token.Type == JTokenType.Null) return null;

            var value = token.ToString().Trim();
            if (value.Length == 0) return null;
            if (string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase)) return null;
            return value;
        }

        private static int? Measure(JObject p, string name)
        {
            var token = p[name];
            if (token == null || token.Type == JTokenType.Null) return null;

            int value;
            if (!int.TryParse(token.ToString().Trim(),
                              NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return null;

            return value == 0 ? (int?)null : value;
        }

        private static DateTime? Moment(JObject p, string name)
        {
            var value = Text(p, name);
            if (value == null) return null;

            DateTime parsed;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                  DateTimeStyles.None, out parsed))
                return parsed;

            return null;
        }

        // NDJSON, một dòng một sự kiện. Body thô được giữ nguyên trong một chuỗi JSON để phân xử
        // khi hai repo tranh chấp "bên nào sai" (§3.5).
        private void AppendLog(string eventId, string body)
        {
            var folder = HostingEnvironment.MapPath(LogFolder);
            var path = Path.Combine(folder, "vehicle-" + DateTime.Now.ToString("yyyyMMdd") + ".log");

            var line = JsonConvert.SerializeObject(new
            {
                received_at = DateTime.Now.ToString("o"),
                remote_addr = Request.UserHostAddress,
                event_id = eventId,
                raw = body
            });

            lock (LogLock)
            {
                Directory.CreateDirectory(folder);

                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        System.IO.File.AppendAllText(path, line + Environment.NewLine, LogEncoding);
                        return;
                    }
                    catch (IOException)
                    {
                        // Chỉ thử lại với IOException. Lỗi quyền là UnauthorizedAccessException,
                        // không phải IOException, nên nó vẫn ném ngay — thử lại cũng vô ích và ta
                        // muốn một cấu hình quyền sai lộ ra thật nhanh.
                        if (attempt >= WriteAttempts) throw;
                        Thread.Sleep(WriteRetryDelayMs);
                    }
                }
            }
        }

        // Site có thể được bind thêm ra mạng để phục vụ giao diện SCADA. Hai endpoint này thì
        // không: Camera AI chạy cùng máy, nên mọi thứ đến từ ngoài loopback đều là giả mạo.
        // Đặt kiểm tra ở đây thay vì dựa vào binding, để nó không vỡ khi binding bị chỉnh sau này.
        private static bool IsLoopbackClient(HttpRequestBase request)
        {
            var addr = request.UserHostAddress;
            if (string.IsNullOrEmpty(addr)) return false;

            // IIS có thể trả địa chỉ IPv4 ánh xạ sang IPv6, dạng "::ffff:127.0.0.1".
            const string mappedPrefix = "::ffff:";
            if (addr.StartsWith(mappedPrefix, StringComparison.OrdinalIgnoreCase))
                addr = addr.Substring(mappedPrefix.Length);

            IPAddress ip;
            return IPAddress.TryParse(addr, out ip) && IPAddress.IsLoopback(ip);
        }

        private ContentResult Forbidden()
        {
            return JsonText(403, "{\"status\":\"forbidden\"}");
        }

        private ContentResult JsonText(int statusCode, string json)
        {
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            return Content(json, "application/json", Encoding.UTF8);
        }
    }
}
