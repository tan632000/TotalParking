using System.Collections.Generic;
using System.Linq;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Chọn zone cho một chiếc xe vừa qua camera.
    //
    // Giai đoạn này chỉ điều hướng tới ZONE, không chọn block hay ô: khách tự
    // chọn block trong zone, rồi chọn pallet trên HMI của block đó.
    //
    // Xếp hạng zone bằng `zone.gate_rank` — thứ tự gần → xa tính từ cổng vào.
    // Đúng ra phải là khoảng cách Dijkstra trên đồ thị làn xe, nhưng mạng làn
    // chưa khảo sát; hai cách dùng chung bước lọc và bước chọn, chỉ khác nguồn
    // con số xếp hạng, nên chuyển sang Dijkstra về sau là thay một hàm.
    // Xem docs/parking-session-db-design.md muc 5.5.
    public class ZoneRouter
    {
        // Luật chọn ô rút về đúng một cột `tier`:
        //   2200KG  -> mọi tier      -> cần zone còn ô cơ khí
        //   2600KG  -> chỉ tier 0    -> cần zone còn ô tier 0
        //   THUONG  -> không pallet  -> cần zone còn chỗ đỗ nền
        public VehicleRouting Route(VehicleProfile profile, IList<ZoneCapacity> zones)
        {
            if (profile == null)
            {
                return Decide(null, RoutingOutcome.Manual, "Khong co ho so phan loai.");
            }

            // Hai nhánh này quyết định trước khi xét sức chứa: xe không vào được
            // bãi thì còn chỗ hay không cũng vô nghĩa.
            if (profile.Rejected)
            {
                return Decide(profile.EventId, RoutingOutcome.Rejected, profile.RejectReason);
            }

            if (profile.RequiresManual)
            {
                return Decide(profile.EventId, RoutingOutcome.Manual, profile.ManualReason);
            }

            var eligible = (zones ?? new List<ZoneCapacity>())
                .Where(z => FreeFor(z, profile.WeightClass) > 0)
                // Gần cổng trước. Hoà nhau thì chọn zone rỗng hơn để tải rải đều
                // thay vì dồn hết vào một zone rồi mới sang zone kế tiếp.
                .OrderBy(z => z.GateRank)
                .ThenBy(z => z.UsedRatio)
                .ToList();

            if (eligible.Count == 0)
            {
                return Decide(profile.EventId, RoutingOutcome.NoCapacity,
                    "Khong zone nao con cho cho hang tai " + profile.WeightClass + ".");
            }

            var chosen = eligible[0];
            var decision = Decide(profile.EventId, RoutingOutcome.Routed, null);
            decision.ZoneId = chosen.ZoneId;
            return decision;
        }

        private static int FreeFor(ZoneCapacity zone, string weightClass)
        {
            if (weightClass == WeightClassCode.Max2600) return zone.FreeTier0;
            if (weightClass == WeightClassCode.Max2200) return zone.FreeMechanical;
            // THUONG và mọi giá trị lạ: chỗ đỗ nền. Ngả về phía hạn chế nhất —
            // đưa một chiếc xe quá tải lên pallet cơ khí là làm sập pallet.
            return zone.FreeGround;
        }

        private static VehicleRouting Decide(string eventId, string outcome, string reason)
        {
            return new VehicleRouting
            {
                EventId   = eventId,
                DecidedAt = System.DateTime.Now,
                Outcome   = outcome,
                Reason    = Trim(reason, 255)
            };
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
