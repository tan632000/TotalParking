namespace TotalParking.Models
{
    // Một thẻ RFID đã đăng ký, đọc từ view v_parking_card.
    // Nguồn dữ liệu gốc: docs/dsthe.xls, nhập bằng Database/02_seed_parking_cards.sql.
    public class ParkingCard
    {
        public int CardId { get; set; }

        // UID RFID 4 byte, 8 ký tự hex. Collation của MySQL không phân biệt hoa thường
        // nên đầu đọc trả "A0D22940" vẫn khớp "a0d22940".
        public string CardCode { get; set; }

        // Số thẻ in trên mặt thẻ, ví dụ "CP.30001".
        public string CardNo { get; set; }

        // VANG (vãng lai) | XT (xe tháng) | GHI
        public string CustomerType { get; set; }
        public string CustomerTypeName { get; set; }

        // THUONG | 2200KG | 2600KG
        public string WeightClass { get; set; }

        // Tải trọng pallet tối đa. null = quá tải, không dùng được pallet cơ khí,
        // phải đỗ nền. Đây là con số tầng PLC cần.
        public int? MaxWeightKg { get; set; }

        public bool IsActive { get; set; }

        // Tiện cho phía gọi: quá tải nghĩa là không pallet nào chịu được.
        public bool IsOverweight
        {
            get { return !MaxWeightKg.HasValue; }
        }
    }
}
