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

        // Số Ô CƠ KHÍ đang có xe, đọc từ thanh ghi PLC qua v_slot_taken
        // (migration 47). Trước đó cột này đếm số phiên trong parking_session,
        // mà bảng đó rỗng vì không còn nơi nào tạo phiên — nên nó luôn bằng 0.
        //
        // KHÔNG phải "số xe trong zone": 80 ô đỗ nền không có thanh ghi nào,
        // nên hệ thống không quan sát được chúng.
        public int InUse { get; set; }

        public int FreeMechanical { get { return Math.Max(0, TotalMechanical - InUse); } }

        // Ô đỗ nền KHÔNG trừ InUse. Đây là chỗ dễ sai nhất sau migration 47:
        // InUse đếm ô cơ khí, trừ nó vào sức chứa đỗ nền là trộn hai loại chỗ
        // khác hẳn nhau. Zone 3 chỉ có 2 ô đỗ nền trên 102 ô cơ khí — trừ nhầm
        // thì xe hạng THƯỜNG bị từ chối ngay khi có 2 chiếc lên pallet, dù cả
        // hai ô nền vẫn trống. Hệ thống không quan sát được ô nền, nên câu trả
        // lời trung thực là "còn nguyên sức chứa", không phải một số bịa.
        public int FreeGround { get { return TotalGround; } }

        // CẢNH BÁO: TotalTier0 lấy từ block.column_count, mà cột đó đang NULL ở
        // cả 118 block nên nó luôn bằng 0 — mọi xe hạng 2600KG sẽ bị trả
        // NoCapacity. Chưa lộ ra vì thực tế chỉ có xe 2200KG và THƯỜNG. Nếu sau
        // này khai báo column_count thì phải sửa luôn phép trừ này: InUse là số
        // ô cơ khí Ở MỌI TẦNG, trừ thẳng vào sức chứa riêng tầng 0 là sai.
        public int FreeTier0 { get { return Math.Max(0, TotalTier0 - InUse); } }

        // Tổng sức chứa quan sát được. Ô đỗ nền không nằm trong đây vì không có
        // cảm biến, nên đưa vào sẽ làm mẫu số phồng lên và tỉ lệ luôn thấp giả.
        public int TotalQuanSatDuoc { get { return TotalMechanical; } }

        public int Total { get { return TotalMechanical + TotalGround; } }

        // Tỉ lệ đã dùng, để xếp hạng khi nhiều zone cùng khoảng cách. Chia cho
        // phần quan sát được, nếu không thì zone nhiều ô nền (zone 6 có 19) sẽ
        // luôn trông rỗng hơn zone ít ô nền (zone 3 có 2) ở cùng mức lấp đầy.
        public double UsedRatio
        {
            get { return TotalQuanSatDuoc == 0 ? 1.0 : (double)InUse / TotalQuanSatDuoc; }
        }
    }
}
