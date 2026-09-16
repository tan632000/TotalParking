using System;

namespace TotalParking.Models
{
    // Vị trí một chiếc xe trong bãi, trả về cho ô "Tìm vị trí" ở trang Báo cáo.
    //
    // Phiên ĐANG MỞ và phiên ĐÃ ĐÓNG là hai câu trả lời khác nhau về bản chất:
    // "xe đang ở block 12" và "xe từng ở block 12, đã lấy ra lúc 14:05". Gộp làm
    // một thì nhân viên sẽ đi tìm chiếc xe không còn trong bãi. IsActive phân biệt
    // hai trường hợp đó, và phía giao diện phải hiển thị khác nhau.
    public class VehicleLocation
    {
        public long   SessionId { get; set; }
        public string CardCode  { get; set; }
        public string CardNo    { get; set; }
        // Biển số do camera AI đọc. Có thể null: luồng gửi xe chỉ bắt buộc có thẻ
        // RFID, biển số là thông tin kèm theo.
        public string Plate     { get; set; }

        public string Status    { get; set; }
        public bool   IsActive  { get; set; }

        public int?   ZoneId    { get; set; }
        public string ZoneCode  { get; set; }
        public string ZoneName  { get; set; }

        public int?   BlockId   { get; set; }
        public int?   BlockNo   { get; set; }
        public string BlockKind { get; set; }
        public int?   SlotCount { get; set; }

        // Nhãn ô đỗ cụ thể. Hiện LUÔN null vì bảng parking_slot chưa được sinh
        // dữ liệu — hệ thống mới định vị được tới cấp block. Giữ trường này để
        // khi có dữ liệu ô thì không phải đổi hợp đồng API.
        public string SlotLabel { get; set; }

        public DateTime  CreatedAt   { get; set; }
        public DateTime? ParkedAt    { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Khoảng thời gian gửi. Phiên đang mở thì tính tới bây giờ, phiên đã đóng
        // thì tính tới lúc đóng.
        public TimeSpan Duration
        {
            get { return (CompletedAt ?? DateTime.Now) - CreatedAt; }
        }

        public string DurationText
        {
            get
            {
                var d = Duration;
                if (d.TotalMinutes < 1) return "vừa xong";
                if (d.TotalHours  < 1) return string.Format("{0} phút", (int)d.TotalMinutes);
                if (d.TotalDays   < 1) return string.Format("{0} giờ {1} phút", (int)d.TotalHours, d.Minutes);
                return string.Format("{0} ngày {1} giờ", (int)d.TotalDays, d.Hours);
            }
        }
    }
}
