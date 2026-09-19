using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using MySqlConnector;
using Newtonsoft.Json;
using TotalParking.Models;
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

        private static readonly ZoneRouter          _router = new ZoneRouter();
        private static readonly LedPanelRepository  _leds   = new LedPanelRepository();
        private static readonly BlockMapRepository  _map    = new BlockMapRepository();
        private static readonly BlockAllocator      _blocks = new BlockAllocator();
        private static readonly LaneNetwork         _lanes  = new LaneNetwork();

        // GET /Monitor/BlockMap
        //
        // Toạ độ + trạng thái 112 block để vẽ sơ đồ. Chiếu sang khung chuẩn hoá
        // ngay ở đây, phía giao diện chỉ việc vẽ — nếu để giao diện tự chiếu thì
        // mỗi trang dùng sơ đồ sẽ có một bản chiếu riêng và sớm muộn lệch nhau.
        public ActionResult BlockMap()
        {
            try
            {
                var blocks = _map.Load();

                // Trạng thái kết nối lấy từ vòng poll đang chạy, không phải từ cột
                // is_active trong CSDL: cột đó là kết quả của lần quét kích hoạt
                // trước đó và không tự cập nhật theo thời gian thực.
                var online = new HashSet<int>();
                var mgr = Services.Plc.PlcHost.Manager;
                if (mgr != null)
                {
                    foreach (var c in mgr.Connections)
                        if (c.IsOnline) online.Add(c.Device.BlockNo);
                }

                return Json2(200, new
                {
                    now    = DateTime.Now.ToString("HH:mm:ss"),
                    view_w = BlockMapRepository.ViewW,
                    view_h = BlockMapRepository.ViewH,
                    gate   = new { x = BlockMapRepository.GateX, y = BlockMapRepository.GateY },
                    blocks = blocks.Select(b => new
                    {
                        block_no = b.BlockNo,
                        zone_id  = b.ZoneId,
                        x        = b.X,
                        y        = b.Y,
                        slots    = b.SlotCount,
                        occupied = b.Occupied,
                        // fresh = 0 nghĩa là hệ thống ĐANG MÙ ở block này: số ô
                        // trống của nó là suy đoán chứ không phải quan sát.
                        fresh    = b.Fresh,
                        online   = online.Contains(b.BlockNo)
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(500, new { error = ex.Message });
            }
        }

        // GET /Monitor/Simulate?weight=2200KG
        //
        // Chạy thử bộ định tuyến với một hạng tải, trả về zone được chọn và LÝ DO.
        //
        // ===================== VÌ SAO KHÔNG VIẾT LẠI LUẬT Ở ĐÂY =====================
        // Gọi đúng `ZoneRouter` mà luồng thật đang dùng. Nếu chép luật ra một bản
        // riêng cho mô phỏng thì hai bản sẽ lệch nhau ngay lần sửa đầu tiên, và
        // lúc đó màn hình mô phỏng sẽ dạy người vận hành một hành vi không tồn tại.
        //
        // ===================== CHỈ ĐỌC =====================
        // Không ghi `vehicle_event`, không ghi `vehicle_routing`, không chạm PLC.
        // Đây là công cụ xem trước, không phải một lượt xe thật.
        public ActionResult Simulate(string weight)
        {
            string wc = (weight ?? "").Trim().ToUpperInvariant();
            if (wc != WeightClassCode.Max2200 &&
                wc != WeightClassCode.Max2600 &&
                wc != WeightClassCode.Overweight)
            {
                return Json2(400, new
                {
                    error = "weight phai la mot trong: 2200KG, 2600KG, THUONG."
                });
            }

            try
            {
                var zones = _routings.GetZoneCapacity();

                var profile = new VehicleProfile
                {
                    EventId     = "SIM",
                    WeightClass = wc
                };
                var decision = _router.Route(profile, zones);

                // Bảng LED nào sẽ chỉ tài xế tới zone được chọn. Đây là thứ biến
                // quyết định trừu tượng thành việc nhìn thấy được ngoài hiện
                // trường: tài xế không đọc zone_id, họ đọc mũi tên trên bảng.
                object signs = new object[0];
                if (decision.ZoneId.HasValue)
                {
                    int z = decision.ZoneId.Value;
                    signs = _leds.GetPanels()
                        .SelectMany(p => p.Ports
                            .Where(pt => pt.IsActive
                                      && pt.Scope == "ZONES"
                                      && !string.IsNullOrWhiteSpace(pt.ZoneList)
                                      && pt.ZoneList.Split(',').Select(s => s.Trim())
                                           .Contains(z.ToString()))
                            .Select(pt => new
                            {
                                panel = p.Code,
                                port  = pt.PortIndex,
                                arrow = ArrowName(pt.ArrowDirection)
                            }))
                        .ToArray();
                }

                // Xem truoc dung bo chon block that, khong phai mot ban chep. Neu mo
                // phong tu chon lay thi trang nay se day nguoi van hanh mot hanh vi
                // he thong khong co. Van la CHI DOC: Allocate va RouteToBlock khong
                // ghi dong nao.
                int?   simBlock  = null;
                bool   simFresh  = false;
                object simRoute  = new object[0];
                string simReason = decision.Reason;

                if (decision.Outcome == RoutingOutcome.Routed && decision.ZoneId.HasValue)
                {
                    var picked = _blocks.Allocate(decision.ZoneId.Value, "SIM");
                    if (picked.HasCapacity)
                    {
                        simBlock = picked.BlockNo;
                        simFresh = picked.OccupancyVerified;

                        var simPath = _lanes.RouteToBlock(picked.BlockNo.Value);
                        if (simPath.Found && simPath.Points.Count >= 2)
                            simRoute = simPath.Points.Select(pt => new { x = pt.X, y = pt.Y }).ToArray();
                        else
                            simReason = simPath.Reason;
                    }
                }

                return Json2(200, new
                {
                    now          = DateTime.Now.ToString("HH:mm:ss"),
                    view_w       = BlockMapRepository.ViewW,
                    view_h       = BlockMapRepository.ViewH,
                    weight_class = wc,
                    outcome      = decision.Outcome,
                    reason       = simReason,
                    zone_id      = decision.ZoneId,
                    block_no     = simBlock,
                    occupancy_verified = simFresh,
                    route        = simRoute,
                    signs,
                    // Từng zone kèm lý do được chọn hay bị loại — phần quan trọng
                    // nhất với người vận hành: biết vì sao KHÔNG phải zone kia.
                    zones = zones
                        .OrderBy(x => x.GateRank)
                        .Select(x => new
                        {
                            zone_id   = x.ZoneId,
                            code      = x.Code,
                            gate_rank = x.GateRank,
                            free      = FreeFor(x, wc),
                            total     = x.Total,
                            used_pct  = x.Total == 0 ? 0 : (int)Math.Round(x.UsedRatio * 100),
                            eligible  = FreeFor(x, wc) > 0,
                            chosen    = decision.ZoneId.HasValue && decision.ZoneId.Value == x.ZoneId
                        }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(500, new { error = ex.Message });
            }
        }

        // Cùng luật với ZoneRouter.FreeFor. Ở đó là private nên phải lặp lại;
        // nếu đổi luật thì phải đổi CẢ HAI — ghi rõ ra đây để không quên.
        private static int FreeFor(ZoneCapacity z, string weightClass)
        {
            if (weightClass == WeightClassCode.Max2600) return z.FreeTier0;
            if (weightClass == WeightClassCode.Max2200) return z.FreeMechanical;
            return z.FreeGround;
        }

        private static string ArrowName(int dir)
        {
            switch (dir)
            {
                case 0:  return "len";
                case 1:  return "phai";
                case 2:  return "xuong";
                default: return "trai";
            }
        }

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
                        height_mm    = v.HeightMm,
                        weight_kg    = v.WeightKg,
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

        // GET /Monitor/DriverRoute
        //
        // Màn hình cho tài xế đang dừng ở barrier. Trả về ĐÚNG một trong ba trạng
        // thái, và trình duyệt chỉ việc vẽ thứ được gửi tới:
        //
        //   ROUTE   — có block đã lưu VÀ có đường đi ít nhất hai điểm
        //   MESSAGE — có quyết định nhưng không có điểm đến vẽ được; chỉ hiện chữ
        //   WAITING — không có quyết định nào còn hiệu lực; xoá đường đang vẽ
        //
        // Không có trạng thái thứ tư nào kiểu "vẽ tạm". Một nét vẽ trên màn hình này
        // là một mệnh lệnh lái xe; khi hệ thống không biết đường thì nó phải im lặng
        // chứ không được đoán.
        public ActionResult DriverRoute()
        {
            try
            {
                string now = DateTime.Now.ToString("HH:mm:ss");

                var decision = _routings.GetCurrentDecision(BlockAllocator.DisplayWindowSeconds);

                if (decision == null)
                    return Json2(200, Waiting(now));

                if (decision.Outcome != RoutingOutcome.Routed || !decision.BlockNo.HasValue)
                    return Json2(200, Message(now, decision, decision.Reason));

                var route = _lanes.RouteToBlock(decision.BlockNo.Value);

                // Một điểm không phải là đường đi. Khi chỉ có đúng chừng đó, màn hình
                // phải nói bằng chữ chứ không vẽ một chấm rồi để tài xế tự hiểu.
                if (!route.Found || route.Points.Count < 2)
                    return Json2(200, Message(now, decision, route.Reason));

                return Json2(200, new
                {
                    now,
                    view_w             = BlockMapRepository.ViewW,
                    view_h             = BlockMapRepository.ViewH,
                    state              = "ROUTE",
                    event_id           = decision.EventId,
                    outcome            = decision.Outcome,
                    reason             = decision.Reason,
                    zone_id            = decision.ZoneId,
                    block_no           = decision.BlockNo,
                    occupancy_verified = decision.OccupancyVerified,
                    route              = route.Points.Select(p => new { x = p.X, y = p.Y }).ToArray()
                });
            }
            catch (MySqlException ex) when (ex.Number == 1146)
            {
                // Chưa chạy 37/38_*.sql thì bảng làn đường hoặc cột block_no chưa tồn
                // tại. Trả 503 có nội dung, giống RoutingState, thay vì để màn hình
                // nhận trang lỗi HTML của IIS rồi chết ở JSON.parse.
                return Json2(503, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Json2(500, new { error = ex.Message });
            }
        }

        private static object Waiting(string now)
        {
            return new
            {
                now,
                view_w             = BlockMapRepository.ViewW,
                view_h             = BlockMapRepository.ViewH,
                state              = "WAITING",
                event_id           = (string)null,
                outcome            = (string)null,
                reason             = (string)null,
                zone_id            = (int?)null,
                block_no           = (int?)null,
                occupancy_verified = false,
                route              = new object[0]
            };
        }

        // block_no về null theo C1: MESSAGE là trạng thái chỉ có chữ. Số block, nếu
        // có, đã nằm sẵn trong câu lý do.
        private static object Message(string now, VehicleRouting decision, string reason)
        {
            return new
            {
                now,
                view_w             = BlockMapRepository.ViewW,
                view_h             = BlockMapRepository.ViewH,
                state              = "MESSAGE",
                event_id           = decision.EventId,
                outcome            = decision.Outcome,
                reason             = reason,
                zone_id            = decision.ZoneId,
                block_no           = (int?)null,
                occupancy_verified = decision.OccupancyVerified,
                route              = new object[0]
            };
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
