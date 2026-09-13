using System;

namespace TotalParking.Models
{
    public static class RoutingOutcome
    {
        public const string Routed      = "ROUTED";
        public const string NoCapacity  = "NO_CAPACITY";
        public const string Rejected    = "REJECTED";
        public const string Manual      = "MANUAL";
    }

    // Quyết định điều hướng cho một sự kiện xe từ Camera AI.
    public class VehicleRouting
    {
        public string   EventId   { get; set; }
        public DateTime DecidedAt { get; set; }
        // null với mọi outcome khác ROUTED.
        public int?     ZoneId    { get; set; }
        public string   Outcome   { get; set; }
        public string   Reason    { get; set; }
    }

    // Sức chứa một zone, đọc từ view v_zone_capacity.
    //
    // Ba con số tổng đến từ mức BLOCK (SUM của block.slot_count và
    // block.column_count), không cần bảng ô chi tiết đã seed xong — nên phần
    // điều hướng tới zone chạy được trước khi có dữ liệu ô từ bản vẽ CAD.
    public class ZoneCapacity
    {
        public int    ZoneId   { get; set; }
        public string Code     { get; set; }
        public int    GateRank { get; set; }

        // Tổng ô pallet cơ khí của zone.
        public int TotalMechanical { get; set; }
        // Tổng ô ở tier 0 — giới hạn của xe hạng 2600KG.
        public int TotalTier0 { get; set; }
        // Tổng chỗ đỗ nền — dành cho hạng THUONG.
        public int TotalGround { get; set; }

        // Số phiên đang mở trong zone. Lưu ý: KHÔNG tách theo loại ô, vì phiên
        // chỉ biết zone chứ chưa biết ô cụ thể ở giai đoạn này. Nên số chỗ trống
        // tính ra là con số thô, đủ để cân bằng tải và để hiển thị, chưa đủ để
        // cam kết với khách là còn đúng bao nhiêu ô loại nào.
        public int InUse { get; set; }

        public int FreeMechanical { get { return Math.Max(0, TotalMechanical - InUse); } }
        public int FreeTier0      { get { return Math.Max(0, TotalTier0 - InUse); } }
        public int FreeGround     { get { return Math.Max(0, TotalGround - InUse); } }

        public int Total { get { return TotalMechanical + TotalGround; } }

        // Tỉ lệ đã dùng, để xếp hạng khi nhiều zone cùng khoảng cách.
        public double UsedRatio
        {
            get { return Total == 0 ? 1.0 : (double)InUse / Total; }
        }
    }
}
