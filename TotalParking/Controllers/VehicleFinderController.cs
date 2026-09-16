using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // Tra vị trí xe cho ô "Tìm vị trí" ở trang Báo cáo (tab Lịch sử thẻ xe).
    //
    // Chỉ đọc. Không ghi PLC, không đổi trạng thái phiên — tra cứu vị trí không
    // được phép gây tác dụng phụ, kể cả khi nhân viên bấm nhầm nhiều lần.
    public class VehicleFinderController : Controller
    {
        private static readonly VehicleFinderRepository _finder =
            new VehicleFinderRepository();

        // GET /VehicleFinder/Find?q=a0d22940
        //
        // Ba câu trả lời khác nhau, KHÔNG gộp làm một:
        //   found        - có phiên, kèm vị trí
        //   card_unknown - thẻ không có trong danh sách đăng ký (gõ sai / chưa cấp)
        //   no_session   - thẻ hợp lệ nhưng chưa từng gửi xe
        // Gộp cả ba thành "không tìm thấy" là bắt nhân viên tự đoán nguyên nhân.
        [HttpGet]
        public ActionResult Find(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Json2(400, new
                {
                    outcome = "empty_query",
                    message = "Nhap ma the, so the hoac bien so."
                });
            }

            q = q.Trim();

            try
            {
                var hits = _finder.Find(q);

                if (hits.Count == 0)
                {
                    bool known = _finder.CardExists(q);
                    return Json2(200, new
                    {
                        query   = q,
                        outcome = known ? "no_session" : "card_unknown",
                        message = known
                            ? "The hop le nhung chua co phien gui xe nao."
                            : "Khong tim thay the hoac bien so nay trong he thong.",
                        results = new object[0]
                    });
                }

                return Json2(200, new
                {
                    query   = q,
                    outcome = "found",
                    // Có phiên đang mở hay không quyết định giao diện hiển thị
                    // "đang trong bãi" hay "đã lấy ra".
                    has_active = hits.Any(h => h.IsActive),
                    results = hits.Select(h => new
                    {
                        session_id = h.SessionId,
                        status     = h.Status,
                        is_active  = h.IsActive,
                        plate      = h.Plate,
                        card_code  = h.CardCode,
                        card_no    = h.CardNo,
                        zone_id    = h.ZoneId,
                        zone_code  = h.ZoneCode,
                        zone_name  = h.ZoneName,
                        block_no   = h.BlockNo,
                        block_kind = h.BlockKind,
                        slot_count = h.SlotCount,
                        // null khi bang parking_slot chua co du lieu — giao dien
                        // phai hien "chua dinh vi toi o" chu khong duoc bia mot o.
                        slot_label = h.SlotLabel,
                        created_at = Fmt(h.CreatedAt),
                        parked_at  = Fmt(h.ParkedAt),
                        completed_at = Fmt(h.CompletedAt),
                        duration   = h.DurationText
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                // Mất DB thì nói thẳng là mất DB. Trả "khong tim thay" khi thực ra
                // không tra được là nói dối người dùng.
                return Json2(503, new
                {
                    outcome = "db_error",
                    message = "Khong truy van duoc co so du lieu: " + ex.Message
                });
            }
        }

        private static string Fmt(DateTime? t)
        {
            return t.HasValue ? t.Value.ToString("dd-MM-yyyy HH:mm:ss") : null;
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
