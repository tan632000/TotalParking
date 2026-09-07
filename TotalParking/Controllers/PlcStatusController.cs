using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Models;
using TotalParking.Services.Plc;

namespace TotalParking.Controllers
{
    // Công cụ chẩn đoán và nghiệm thu tầng PLC. Ba việc:
    //
    //   Index  trạng thái sống của vòng poll — đây là cách duy nhất nhìn được nó
    //          đang làm gì, vì trạng thái kết nối cố ý KHÔNG ghi xuống DB mỗi
    //          nhịp (docs/parking-session-db-design.md mục 3.6).
    //   Read   đọc thô D100 để xác định bố cục mã thẻ.
    //   Write  ghi thẳng D402 + W75.0, bỏ qua bước tra thẻ.
    //
    // Write là đường duy nhất trong toàn hệ thống ghi xuống PLC mà KHÔNG bắt
    // nguồn từ một lượt quẹt thẻ thật. Nó tồn tại vì nếu không có nó thì không
    // cách nào nghiệm thu phía HMI trước lúc ladder sẵn sàng — nhưng vì thế nó
    // phải bị khoá chặt hơn mọi endpoint khác trong dự án.
    public class PlcStatusController : Controller
    {
        // GET /PlcStatus
        public ActionResult Index()
        {
            var manager = PlcHost.Manager;

            object payload;
            if (manager == null)
            {
                payload = new
                {
                    poll_enabled = PlcHost.Enabled,
                    poll_running = false,
                    note = "Khong nap duoc cau hinh PLC: kiem tra connection string, "
                         + "va da chay 05/06/07_*.sql chua."
                };
            }
            else
            {
                payload = new
                {
                    // Hai cờ khác nhau: poll_enabled là plc:enabled trong Web.config,
                    // poll_running là vòng lặp có đang chạy thật hay không. Khi tắt
                    // poll thì /PlcStatus/Read VẪN dùng được — nó tự mở kết nối.
                    poll_enabled = PlcHost.Enabled,
                    poll_running = manager.IsRunning,
                    manual_write_allowed = PlcHost.AllowManualWrite,
                    now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    blocks = manager.Connections.Select(c => new
                    {
                        block_no  = c.Device.BlockNo,
                        endpoint  = c.Device.Endpoint,
                        online    = c.IsOnline,
                        last_ok   = ToLocal(c.LastOkUtc),
                        last_scan = ToLocal(c.LastScanUtc),
                        last_card = c.LastCard,
                        // null = chua chot bo cuc D100, dang do. Xem CardCodeDecoder.
                        layout    = c.Layout.HasValue ? c.Layout.Value.ToString() : null,
                        error     = c.LastError,
                        registers = new
                        {
                            card   = "D" + c.Device.CardWord,
                            @class = "D" + c.Device.ClassWord,
                            permit = c.Device.PermitBitArea + c.Device.PermitBit,
                            request = c.Device.HasRequestBit
                                ? c.Device.RequestBitArea + c.Device.RequestBit
                                : null
                        }
                    }).ToArray()
                };
            }

            return new ContentResult
            {
                ContentType = "application/json",
                Content = JsonConvert.SerializeObject(payload, Formatting.Indented),
                ContentEncoding = Encoding.UTF8
            };
        }

        // GET /PlcStatus/Read?block=1&start=100&count=8
        //
        // Đọc thô một dải word. Đây là cách xác định bố cục D100: quẹt một thẻ đã
        // biết mã — ví dụ CP.30001 = a0d22940 — rồi gọi endpoint này và nhìn dãy số.
        // Chỉ đọc, không đổi gì trong PLC.
        [HttpGet]
        public async Task<ActionResult> Read(int block, int start = 100, int count = 8)
        {
            if (!IsLoopbackClient()) return Forbidden();

            var conn = Resolve(block);
            if (conn == null) return Json2(404, new { error = "Khong co block " + block });

            if (count < 1 || count > 64)
                return Json2(400, new { error = "count phai trong 1..64" });

            try
            {
                var words = await conn.ReadWordsRawAsync(start, count);
                return Json2(200, new
                {
                    block_no = block,
                    address  = "D" + start + "-D" + (start + count - 1),
                    hex      = CardCodeDecoder.ToRawHex(words),
                    dec      = words.Select(w => (int)w).ToArray(),
                    // Thử cả bốn bố cục để đối chiếu bằng mắt với mã thẻ đã biết.
                    // Bố cục nào ra đúng mã thì ghi vào plc_device.card_layout.
                    decoded  = CardCodeDecoder.ProbeOrder.ToDictionary(
                                   l => l.ToString(),
                                   l => CardCodeDecoder.TryDecode(words, l))
                });
            }
            catch (Exception ex)
            {
                return Json2(502, new { error = ex.Message });
            }
        }

        // POST /PlcStatus/Write   body: block=1&class=2200&permit=1
        //
        // Ghi thẳng cặp trả lời xuống PLC, bỏ qua bước tra thẻ. Dùng để nghiệm thu
        // phía HMI trước khi ladder có bit báo lượt quẹt: xác nhận HMI thật sự ẩn
        // pallet tầng trên khi D402 = 2600, và báo lỗi khi W75.0 = 0.
        //
        // Đây là thao tác GHI XUỐNG THIẾT BỊ THẬT nên khoá ba lớp: chỉ POST, chỉ
        // từ loopback, và phải bật riêng plc:allowManualWrite. Không gộp vào
        // plc:enabled — chạy vòng poll là việc thường ngày, còn cưỡng bức thanh ghi
        // thì không.
        [HttpPost]
        public async Task<ActionResult> Write(int block, int @class, bool permit)
        {
            if (!IsLoopbackClient()) return Forbidden();

            if (!PlcHost.AllowManualWrite)
                return Json2(403, new
                {
                    error = "plc:allowManualWrite = false trong Web.config."
                });

            if (@class != WeightClassWord.Max2200 && @class != WeightClassWord.Max2600)
                return Json2(400, new { error = "class phai la 2200 hoac 2600" });

            var conn = Resolve(block);
            if (conn == null) return Json2(404, new { error = "Khong co block " + block });

            try
            {
                await conn.WriteAnswerAsync(@class, permit);
                return Json2(200, new
                {
                    block_no = block,
                    wrote = new
                    {
                        // Thứ tự liệt kê đúng thứ tự đã ghi: D402 trước, W75.0 sau.
                        d402  = @class,
                        w75_0 = permit ? 1 : 0
                    },
                    note = "Da ghi. Kiem tra tren HMI: 2600 phai an pallet tang tren."
                });
            }
            catch (Exception ex)
            {
                return Json2(502, new { error = ex.Message });
            }
        }

        private static PlcConnection Resolve(int blockNo)
        {
            var manager = PlcHost.Manager;
            return manager == null ? null : manager.Find(blockNo);
        }

        // Cùng nguyên tắc với IngestController: mọi endpoint chạm phần cứng chỉ
        // nhận lời gọi từ chính máy đang chạy SCADA.
        private bool IsLoopbackClient()
        {
            if (Request == null || Request.UserHostAddress == null) return false;
            IPAddress ip;
            return IPAddress.TryParse(Request.UserHostAddress, out ip) &&
                   IPAddress.IsLoopback(ip);
        }

        private ActionResult Forbidden()
        {
            return Json2(403, new { error = "Chi chap nhan loi goi tu loopback." });
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

        private static string ToLocal(DateTime? utc)
        {
            return utc.HasValue
                ? utc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : null;
        }
    }
}
