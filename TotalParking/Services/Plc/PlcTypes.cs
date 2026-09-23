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
        // Mã lỗi của lớp FINS/TCP khi PLC từ chối bắt tay. 0 = lỗi khung tin
        // thông thường, không phải PLC từ chối.
        //
        // Để ở đây thay vì bắt phía trên đọc chuỗi thông báo: phân loại lỗi bằng
        // cách so chuỗi là thứ vỡ im lặng ngay lần đầu ai đó sửa câu thông báo.
        public uint HandshakeError { get; private set; }

        // PLC hết khe kết nối. Đo thực tế trên CP-series tại bãi này: mỗi PLC
        // cấp đúng BA khe, node 251/252/253. Khe thứ tư bị từ chối tức thì.
        //
        // Đây là lỗi TÀI NGUYÊN, không phải lỗi mạng — thử lại dày không giúp gì
        // mà còn đốt cổng tạm: mỗi lần thử để lại một socket TIME_WAIT sống vài
        // phút. Đo được 278 socket như vậy khi 60 block cùng hỏng.
        public const uint AllConnectionsInUse = 0x00000020;

        public bool HetKheKetNoi { get { return HandshakeError == AllConnectionsInUse; } }

        public FinsFramingException(string message) : base(message) { }

        public FinsFramingException(string message, uint handshakeError) : base(message)
        {
            HandshakeError = handshakeError;
        }
    }
}
