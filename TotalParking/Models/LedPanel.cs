using System.Collections.Generic;

namespace TotalParking.Models
{
    public static class LedPanelKind
    {
        // Bảng đầu hầm: luôn nối P1, hiện TỔNG số chỗ trống. Không cần biết mũi
        // tên chỉ đâu nên chạy được mà không cần đồ thị dẫn đường.
        public const string Entrance    = "ENTRANCE";
        public const string Directional = "DIRECTIONAL";
    }

    public static class LedPortScope
    {
        public const string Total = "TOTAL";
        public const string Zones = "ZONES";
    }

    public class LedPanel
    {
        public int    PanelId   { get; set; }
        // Octet cuối của IP, cũng là số bảng trên sơ đồ hiện trường.
        public string Code      { get; set; }
        public string IpAddress { get; set; }
        public int    Port      { get; set; }
        public string HubType   { get; set; }
        public string Kind      { get; set; }
        public int?   PosX      { get; set; }
        public int?   PosY      { get; set; }
        public string Note      { get; set; }
        public bool   IsActive  { get; set; }

        public IList<LedPanelPort> Ports { get; set; }

        public LedPanel() { Ports = new List<LedPanelPort>(); }

        public string Endpoint { get { return IpAddress + ":" + Port; } }
    }

    public class LedPanelPort
    {
        public int    PanelId        { get; set; }
        public int    PortIndex      { get; set; }
        public int    ArrowDirection { get; set; }
        public int    ArrowColor     { get; set; }
        public int    ArrowState     { get; set; }
        public string Scope          { get; set; }
        // Danh sách zone dạng "1,2,6". NULL khi Scope = TOTAL, hoặc khi chưa
        // biết mũi tên này dẫn tới đâu.
        public string ZoneList       { get; set; }
        public bool   IsActive       { get; set; }
    }

    // Số chỗ trống chia theo CHIỀU DÀI khoang — trục khác với ZoneCapacity
    // (chia theo tầng, phục vụ luật pallet 2200/2600).
    //
    // Tên trường theo đúng ba bộ đếm của frame, KHÔNG theo thứ tự enum LedLane:
    // frame là L5M trước, L48M sau. Đặt tên bám frame để chỗ gán giá trị đọc
    // lên là thấy đúng ngay.
    public class LedCapacity
    {
        public int FreeL5m      { get; set; }   // X5.X6  Mechanical L < 5 M
        public int FreeL48m     { get; set; }   // X7.X8  Mechanical L < 4.8 M
        public int FreeStandard { get; set; }   // X9.X10 Standard

        public int TotalL5m      { get; set; }
        public int TotalL48m     { get; set; }
        public int TotalStandard { get; set; }

        // Phiên đang mở nhưng chưa biết block. Không trừ vào bộ đếm nào vì
        // không biết nó sẽ chiếm loại khoang nào — phơi ra để nhìn thấy.
        public int UsedUnassigned { get; set; }

        public int FreeTotal { get { return FreeL5m + FreeL48m + FreeStandard; } }
    }
}
