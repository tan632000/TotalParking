using System;

namespace TotalParking.Services.Plc
{
    // Vùng nhớ PLC Omron. Giá trị enum không phải mã FINS — mã FINS tra trong
    // OmronFinsUdpClient, và mã đọc word khác mã đọc bit của cùng một vùng.
    public enum PlcMemoryArea
    {
        DM,   // D — Data Memory, nơi có D100 và D402
        CIO,
        WR,   // W — Work Area, nơi có W75.0
        HR,
        AR
    }

    public enum BitState
    {
        Off = 0,
        On  = 1
    }

    // Lỗi do chính PLC trả về trong End Code của khung FINS, khác hẳn lỗi mạng.
    // Phân biệt hai loại này quan trọng: lỗi FINS nghĩa là kết nối vẫn tốt và
    // lệnh vẫn tới nơi, chỉ là lệnh sai — reconnect không giải quyết được gì.
    public class FinsException : Exception
    {
        public byte MainCode { get; private set; }
        public byte SubCode  { get; private set; }

        public FinsException(byte mainCode, byte subCode, string message)
            : base(message)
        {
            MainCode = mainCode;
            SubCode  = subCode;
        }
    }

    // Lỗi đường truyền: hết giờ không có trả lời, phản hồi thiếu byte, lỗi socket.
    //
    // Tách riêng vì nó bắt buộc phải dẫn tới ĐÓNG rồi dựng lại socket: không còn
    // tin được đường này nữa. Khác FinsException, nơi PLC đã trả lời đàng hoàng
    // và chỉ có lệnh là sai.
    public class FinsFramingException : Exception
    {
        public FinsFramingException(string message) : base(message) { }
    }
}
