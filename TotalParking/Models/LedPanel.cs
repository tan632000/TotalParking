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

        // HAI CÁCH ĐÁNH SỐ, cố ý giữ cả hai vì ngoài hiện trường dùng song song
        // và chúng không trùng nhau — dải IP nhảy từ .58 sang .65:
        //   Code = octet cuối IP   '65'          (sơ đồ kỹ thuật, led_position.jpg)
        //   Name = tên bảng IP     'Bang led 9'  (bên thi công gọi)
        // Ép về một cái thì mỗi lần trao đổi lại phải quy đổi bằng đầu.
        public string Code      { get; set; }
        public string Name      { get; set; }
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

        // Bãi đã được khai báo sức chứa hay chưa.
        //
        // Phân biệt "không còn chỗ" với "không biết còn chỗ hay không" là bắt
        // buộc, không phải chi tiết. Cả hai đều cho ba số 0, nhưng một cái là
        // sự thật về bãi xe còn cái kia là sự thật về hệ thống — và đẩy `0 0 0`
        // lên bảng khi thực ra chưa có dữ liệu là nói với tài xế rằng bãi đã đầy.
        public bool HasData
        {
            get { return TotalL5m + TotalL48m + TotalStandard > 0; }
        }
    }
}
