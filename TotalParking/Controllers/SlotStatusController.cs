using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services.Plc;

namespace TotalParking.Controllers
{
    // Trạng thái từng ô đỗ đọc từ PLC.
    //
    // CHỈ ĐỌC PLC. Có ghi xuống DB (bảng plc_slot_state) nhưng không ghi gì xuống
    // thiết bị, nên gọi bao nhiêu lần cũng không tác động tới bãi xe.
    public class SlotStatusController : Controller
    {
        private static readonly SlotOccupancyReader _reader = new SlotOccupancyReader();

        // GET /SlotStatus  — đọc từ DB, không chạm PLC.
        public ActionResult Index(int? block)
        {
            try
            {
                var slots = _reader.Load(block);
                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    word_count = _reader.SlotWordCount,
                    // Trang thai vong quet tu dong. scan_running = false nghia la
                    // du lieu duoi day chi dung tai thoi diem quet tay gan nhat.
                    scan_enabled = SlotScanHost.Enabled,
                    scan_running = SlotScanHost.IsRunning,
                    scan_every_ms = SlotScanHost.IntervalMs,
                    scan_last_at = SlotScanHost.LastRunAt.HasValue
                                       ? SlotScanHost.LastRunAt.Value.ToString("HH:mm:ss") : null,
                    scan_last = SlotScanHost.LastSummary,
                    scan_error = SlotScanHost.LastError,
                    total_slots = slots.Count,
                    occupied = slots.Count(s => s.IsOccupied),
                    // Thanh ghi khac 0 nhung khong khop the nao — can nguoi xem,
                    // KHONG tinh la o da dung.
                    suspect = slots.Count(s => s.IsSuspect),
                    free = slots.Count(s => !s.IsOccupied && !s.IsSuspect),
                    // Ô nào chưa từng đọc thì read_at = null. Phân biệt với "đã đọc
                    // và thấy trống" — hai thứ khác nhau hoàn toàn.
                    never_read = slots.Count(s => !s.ReadAt.HasValue),
                    by_zone = slots.GroupBy(s => s.ZoneId).OrderBy(g => g.Key).Select(g => new
                    {
                        zone_id  = g.Key,
                        slots    = g.Count(),
                        occupied = g.Count(s => s.IsOccupied)
                    }).ToArray(),
                    slots = slots.Select(s => new
                    {
                        zone_id   = s.ZoneId,
                        block_no  = s.BlockNo,
                        slot      = s.SlotIndex,
                        register  = s.Register,
                        card_code = s.CardCode,
                        card_known = s.CardKnown,
                        suspect   = s.IsSuspect,
                        raw       = s.RawWords,
                        occupied  = s.IsOccupied,
                        read_at   = s.ReadAt.HasValue ? s.ReadAt.Value.ToString("HH:mm:ss") : null
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // POST /SlotStatus/Scan  — đọc thật từ PLC rồi cập nhật DB.
        //
        // POST chứ không GET: nó đi ra thiết bị và ghi DB, nên không được để trình
        // duyệt hay công cụ quét link gọi nhầm.
        [HttpPost]
        public async Task<ActionResult> Scan()
        {
            var manager = PlcHost.Manager;
            if (manager == null)
                return Json2(503, new { error = "Chua nap duoc cau hinh PLC." });

            try
            {
                var r = await _reader.ScanAsync(manager);
                return Json2(200, new
                {
                    now = DateTime.Now.ToString("HH:mm:ss"),
                    blocks_tried = r.BlocksTried,
                    blocks_read  = r.BlocksRead,
                    slots_read   = r.SlotsRead,
                    occupied     = r.Occupied,
                    suspect      = r.Suspect,
                    suspect_slots = r.SuspectSlots.Select(kv => new { slot = kv.Key, value = kv.Value }).ToArray(),
                    changed      = r.Changed,
                    failures     = r.Failures.Select(kv => new { block_no = kv.Key, error = kv.Value }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /SlotStatus/Audit?lines=200&only=WRITE
        //
        // Doc duoi file nhat ky doc/ghi thanh ghi. Moi dong mang IP THAT da gui goi
        // tin toi — dung de truy vet "luc do ghi xuong PLC nao".
        [HttpGet]
        public ActionResult Audit(int lines = 200, string only = null)
        {
            if (!IsLoopback()) return new HttpStatusCodeResult(403, "Chi tu loopback.");

            string path = PlcAuditLog.Path;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return Json2(200, new
                {
                    path,
                    enabled = PlcAuditLog.Enabled,
                    note = "Chua co file nhat ky (chua co thao tac nao, hoac khong ghi duoc).",
                    lines = new string[0]
                });

            if (lines < 1)    lines = 1;
            if (lines > 5000) lines = 5000;

            string[] all;
            try
            {
                // FileShare.ReadWrite: vong poll dang mo file de ghi, mo doc thuong
                // se bi khoa.
                using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Open,
                           System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                using (var sr = new System.IO.StreamReader(fs, Encoding.UTF8))
                {
                    all = sr.ReadToEnd()
                             .Split(new[] { (char)10 }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(l => l.TrimEnd((char)13))
                             .Where(l => l.Length > 0)
                             .ToArray();
                }
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }

            var q = all.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(only))
                q = q.Where(l => l.IndexOf(only, StringComparison.OrdinalIgnoreCase) >= 0);

            var tail = q.Reverse().Take(lines).Reverse().ToArray();
            return Json2(200, new
            {
                path,
                enabled     = PlcAuditLog.Enabled,
                scan_lines  = PlcAuditLog.ScanLines,
                total_lines = all.Length,
                shown       = tail.Length,
                filter      = only,
                lines       = tail
            });
        }

        private bool IsLoopback()
        {
            var req = Request;
            return req != null && req.IsLocal;
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
