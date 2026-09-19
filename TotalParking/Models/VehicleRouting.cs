using System;

namespace TotalParking.Models
{
    public static class RoutingOutcome
    {
        public const string Routed      = "ROUTED";
        public const string NoCapacity  = "NO_CAPACITY";
        // Khác NoCapacity: bãi chưa được khai báo sức chứa. "Hết chỗ" là sự thật
        // về bãi xe; "chưa có dữ liệu" là sự thật về hệ thống. Gộp hai cái làm
        // một chính là cách hệ thống tỏ ra biết trong khi nó không biết.
        public const string NoData      = "NO_DATA";
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

        // Block đích, do BlockAllocator chọn ở server. Cũng null với mọi outcome
        // khác ROUTED, và database có trigger chặn cả hai chiều vi phạm.
        public int?     BlockNo   { get; set; }

        // false nghĩa là PLC chưa báo cáo block này trong 5 phút gần đây, nên số ô
        // trống của nó là suy luận chứ không phải quan sát. Vẫn chọn block đó, nhưng
        // bề mặt hiển thị phải nói ra điều này thay vì im lặng.
        public bool     OccupancyVerified { get; set; }
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
