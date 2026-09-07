using System;

namespace TotalParking.Services.Plc
{
    // Vùng nhớ PLC Omron. Giá trị enum không phải mã FINS — mã FINS tra trong
    // OmronFinsClient, và mã đọc word khác mã đọc bit của cùng một vùng.
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

    // Lỗi khung tin: đọc thiếu byte, sai magic "FINS", độ dài vô lý.
    //
    // Tách riêng vì nó bắt buộc phải dẫn tới ĐÓNG KẾT NỐI. Khi đọc dở một khung
    // thì phần byte còn lại vẫn nằm trong stream, và mọi lần đọc sau đó sẽ lệch
    // đúng bằng số byte thừa ấy — kết nối hỏng vĩnh viễn mà vẫn báo Connected.
    // Đây là lỗi mà PLC-Connect gốc mắc phải: ReadFullAsync ở đó nuốt timeout
    // rồi trả về số byte đọc dở, và luồng gọi cứ thế đọc tiếp.
    public class FinsFramingException : Exception
    {
        public FinsFramingException(string message) : base(message) { }
    }
}
