namespace TotalParking.Models
{
    // Lý do từ chối. W75.0 chỉ có một bit nên HMI không phân biệt được các lý do
    // này — chúng được giữ lại ở phía SCADA cho việc truy vết và cho tab Báo cáo.
    public static class RejectReason
    {
        public const string WrongBlock   = "WRONG_BLOCK";     // xe đang gửi ở block khác
        public const string InvalidCard  = "INVALID_CARD";    // không có trong parking_card
        public const string InactiveCard = "INACTIVE_CARD";   // thẻ bị khoá
        public const string DbUnavailable = "DB_UNAVAILABLE"; // không đọc được DB
        public const string UnknownCode  = "UNKNOWN_CODE";    // giải mã D100 không ra mã nào
        // Ràng buộc uq_session_active_card chặn — nghĩa là có một lượt quẹt khác
        // cùng thẻ vừa chen vào. Đáng lẽ khoá hàng thẻ đã ngăn được, nên gặp mã
        // này là dấu hiệu có đường ghi nào đó không đi qua CardScanService.
        public const string ConcurrentScan = "CONCURRENT_SCAN";
    }

    // Kết quả SCADA trả về cho một lượt quẹt thẻ.
    //
    // Permit ánh xạ thẳng sang W75.0: true = đúng block đã gửi xe (hoặc thẻ chưa
    // gửi ở đâu cả, tức block nào cũng hợp lệ), false = sai block hoặc lỗi.
    //
    // WeightClassValue ánh xạ sang D402: 2200 hoặc 2600.
    public class CardScanDecision
    {
        public bool   Permit           { get; set; }
        public int    WeightClassValue { get; set; }
        public string RejectReason     { get; set; }
        public long?  SessionId        { get; set; }

        // Phân biệt gửi xe với lấy xe. Cả hai đều cho Permit = true nên W75.0 một
        // mình không nói được đây là nghiệp vụ nào — HMI phải tự suy ra. Giữ ở
        // đây để ghi nhật ký và để dùng khi có thêm thanh ghi phân biệt.
        public bool IsRetrieval { get; set; }

        // Giá trị an toàn khi không xác định được gì: từ chối, và hạng tải hạn chế
        // nhất. Đưa xe 2600 lên pallet tầng trên có thể làm sập pallet, nên mọi
        // trường hợp không chắc chắn phải ngả về phía hạn chế chứ không phải cho phép.
        public static CardScanDecision Deny(string reason)
        {
            return new CardScanDecision
            {
                Permit           = false,
                WeightClassValue = WeightClassWord.Restricted,
                RejectReason     = reason
            };
        }
    }

    // Giá trị ghi xuống D402. Là số thật, không phải mã quy ước.
    public static class WeightClassWord
    {
        public const int Max2200 = 2200;
        public const int Max2600 = 2600;

        // Hạng hạn chế nhất trong hai hạng pallet: chỉ dùng được tầng dưới.
        public const int Restricted = Max2600;
    }
}
