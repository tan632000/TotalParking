using System;

namespace TotalParking.Models
{
    // Một lần Camera AI phát hiện xe, đã chuẩn hoá từ payload POST /vehicle.
    // Các trường đo lường là nullable: camera gửi 0 hoặc "Unknown" khi không
    // xác định được, và cả hai đều được chuyển thành null lúc tiếp nhận.
    public class VehicleEvent
    {
        public string   EventId    { get; set; }
        public DateTime ReceivedAt { get; set; }

        // Giờ camera gửi. Cùng máy nên cùng đồng hồ, so sánh trực tiếp được.
        public DateTime? CameraTimestamp { get; set; }

        public string Make      { get; set; }
        public string Model     { get; set; }
        public string YearRange { get; set; }

        public int? LengthMm { get; set; }
        public int? WidthMm  { get; set; }
        public int? HeightMm { get; set; }
        public int? WeightKg { get; set; }

        // Nhãn tổng hợp của camera. Chỉ để hiển thị và đối chiếu, không dùng
        // để phân bổ block — xem docs/camera-led-routing-design.md §4.4.
        public string Category { get; set; }

        public string ImagePath { get; set; }

        // Body thô nguyên văn, giữ lại để phân xử khi hai repo bất đồng.
        public string RawBody { get; set; }
    }
}
