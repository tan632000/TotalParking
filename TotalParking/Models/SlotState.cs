using System;

namespace TotalParking.Models
{
    // Trạng thái một ô đỗ trong block, đọc từ thanh ghi của PLC.
    //
    // Đây là nguồn dữ liệu chiếm chỗ ĐÁNG TIN NHẤT của hệ thống: PLC nói thẳng ô
    // nào đang giữ thẻ nào, không phải suy từ phiên gửi do SCADA tự mở. Cách cũ
    // (đếm parking_session) sai ngay khi có ai đó thao tác tay tại HMI mà SCADA
    // không biết — và trong giai đoạn nghiệm thu thì chuyện đó xảy ra liên tục.
    public class SlotState
    {
        public int BlockId   { get; set; }
        public int BlockNo   { get; set; }
        public int ZoneId    { get; set; }

        // 1..10, theo đúng thứ tự thanh ghi khách xác nhận:
        // ô 1 -> D400, ô 2 -> D202, ô 3 -> D204 ... ô 10 -> D308
        public int SlotIndex { get; set; }
        public int WordAddr  { get; set; }

        // null = ô trống. Khác null = mã thẻ của xe đang đỗ.
        public string CardCode { get; set; }
        // Giá trị thô dạng hex, giữ lại để giải mã lại được nếu bố cục hoá ra khác
        // giả định. Đúng bài học từ D100: không có dữ liệu thô thì mỗi lần đổi giả
        // thuyết là phải ra hiện trường quẹt thẻ lại.
        public string RawWords { get; set; }

        // Mã đọc được có khớp một thẻ THẬT trong parking_card hay không.
        //
        // Phân biệt này sinh ra từ một dương tính giả có thật: thanh ghi D401 trên
        // 5 block khác nhau cùng mang giá trị 0x0301, giải mã ra "03010000" và bị
        // đếm là 5 xe. Một mã thẻ không thể nằm ở 5 block cùng lúc.
        //
        // Thanh ghi khác 0 nhưng không khớp thẻ nào thì là DỮ LIỆU LẠ, không phải
        // xe. Đếm nó vào số ô đã dùng sẽ làm bảng LED báo thiếu chỗ.
        public bool CardKnown { get; set; }

        // Chỉ tính là có xe khi mã khớp thẻ thật.
        public bool IsOccupied { get { return !string.IsNullOrEmpty(CardCode) && CardKnown; } }

        // Thanh ghi khác 0 nhưng chưa nhận ra là thẻ nào — cần người xem.
        public bool IsSuspect { get { return !string.IsNullOrEmpty(CardCode) && !CardKnown; } }

        public DateTime? ReadAt    { get; set; }
        public DateTime? ChangedAt { get; set; }

        public string Register { get { return "D" + WordAddr; } }
    }
}
