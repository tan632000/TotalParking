namespace TotalParking.Models
{
    // Ba lớp đếm của bảng LED, theo tài liệu giao thức.
    public enum LedLane
    {
        MechanicalL48M,  // pallet cơ khí, dài < 4.8 m
        MechanicalL5M,   // pallet cơ khí, dài >= 5 m
        Normal           // đỗ nền, không dùng pallet cơ khí
    }

    // Mã hạng tải, cùng bộ giá trị với cột weight_class của bảng thẻ, để đối
    // chiếu trực tiếp được giữa "xe này cần gì" và "thẻ này đăng ký gì".
    public static class WeightClassCode
    {
        public const string Overweight = "THUONG";
        public const string Max2200    = "2200KG";
        public const string Max2600    = "2600KG";
    }

    // Kết quả phân loại một sự kiện xe. Tính một lần lúc tiếp nhận, sau đó không đổi.
    public class VehicleProfile
    {
        public string EventId { get; set; }

        // Xe vượt giới hạn vật lý của bãi — không phải "chờ có chỗ" mà là không
        // bao giờ vào được. Phải có nhánh này, nếu không mỗi xe tải đi ngang
        // camera sẽ nằm lại trong hàng đợi nhân viên vĩnh viễn.
        public bool   Rejected     { get; set; }
        public string RejectReason { get; set; }

        // Thiếu dữ liệu thì không được đoán khoang. Đoán ở đây nghĩa là điều một
        // chiếc van 5,4 m vào pallet 4,8 m.
        public bool   RequiresManual { get; set; }
        public string ManualReason   { get; set; }

        public LedLane Lane { get; set; }

        // THUONG | 2200KG | 2600KG
        public string WeightClass { get; set; }

        // Tải trọng pallet tối thiểu cần có. null khi phải đỗ nền.
        public int? RequiredPalletKg { get; set; }

        // Khối lượng ước tính khi có tải, không phải khối lượng bản thân camera gửi.
        public int? EstimatedLoadedWeightKg { get; set; }

        // Chiều rộng đã cộng biên gương — gương mới là thứ va vào ray trong khoang.
        public int? EffectiveWidthMm { get; set; }

        public bool CanUseMechanicalPallet
        {
            get { return !Rejected && Lane != LedLane.Normal; }
        }
    }
}
