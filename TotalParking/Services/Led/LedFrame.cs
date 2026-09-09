using System.Text;

namespace TotalParking.Services.Led
{
    // Mô hình khung tin của bảng LED chỉ hướng.
    //
    // CHUYỂN THỂ TỪ led-control/Led.cs, giữ nguyên logic. Phần sinh khung và
    // checksum ở đó đã được kiểm chứng **9/9 frame mẫu của spec, byte-for-byte**,
    // và board thật ACK đúng (led-control/docs/testing-guide.md mục 6). Viết lại
    // là tự nguyện vứt bỏ bằng chứng kiểm chứng duy nhất đang có.
    //
    // Khác bản gốc đúng hai chỗ:
    //   - namespace
    //   - thêm ClampValue: probe cho thấy bảng hiển thị 4 chữ số nên tối đa 9999,
    //     trong khi giao thức nhận tới 65535. Gửi số lớn hơn thì bảng hiện sai
    //     mà không báo lỗi.
    //
    // Khung tin:  $PORT,X1,X2.X3.X4,X5.X6,X7.X8,X9.X10*CRC#

    public enum LedColor
    {
        Black  = 0,
        Red    = 1,
        Green  = 2,
        Yellow = 3
    }

    public enum LedDirection
    {
        Up    = 0,
        Right = 1,
        Down  = 2,
        Left  = 3
    }

    public enum LedArrowState
    {
        Stand = 0,
        Move  = 1
    }

    public class LedArrow
    {
        public LedColor      Color     { get; set; }
        public LedDirection  Direction { get; set; }
        public LedArrowState State     { get; set; }

        public LedArrow() { Color = LedColor.Green; }
    }

    public class LedNumber
    {
        public int      Value { get; set; }
        public LedColor Color { get; set; }

        public LedNumber() { Color = LedColor.Green; }
    }

    public class LedHub
    {
        // Bảng hiển thị 4 chữ số, đệm số 0 — xác nhận bằng probe: gửi 44 thì
        // bảng hiện 0044. Giao thức nhận tới 65535 nhưng bảng chỉ hiện được
        // 9999, nên phải chặn ở đây chứ không phải trông vào bảng tự xử lý.
        public const int MaxDisplayValue = 9999;

        public int Port { get; set; }

        public LedArrow  Arrow     { get; set; }
        public LedNumber Position1 { get; set; }
        public LedNumber Position2 { get; set; }
        public LedNumber Position3 { get; set; }

        public LedHub()
        {
            Arrow     = new LedArrow();
            Position1 = new LedNumber();
            Position2 = new LedNumber();
            Position3 = new LedNumber();
        }

        public string GetCommand()
        {
            var data =
                "PORT," + Port + "," +
                (int)Arrow.Direction + "." + (int)Arrow.Color + "." + (int)Arrow.State + "," +
                Clamp(Position1.Value) + "." + (int)Position1.Color + "," +
                Clamp(Position2.Value) + "." + (int)Position2.Color + "," +
                Clamp(Position3.Value) + "." + (int)Position3.Color;

            return "$" + data + "*" + CheckSum(data).ToString("X2") + "#";
        }

        private static int Clamp(int value)
        {
            if (value < 0) return 0;
            return value > MaxDisplayValue ? MaxDisplayValue : value;
        }

        // XOR mọi byte giữa '$' và '*', in ra 2 chữ số hex hoa.
        //
        // Bản tham chiếu của nhà cung cấp bắt đầu vòng lặp ở i = 1 để bỏ qua
        // ký tự '$'. Ở đây `data` không chứa '$' nên XOR toàn chuỗi — hai cách
        // tương đương, không phải trùng hợp ngẫu nhiên.
        public static byte CheckSum(string payload)
        {
            byte crc = 0;
            byte[] buffer = Encoding.ASCII.GetBytes(payload);
            for (int i = 0; i < buffer.Length; i++) crc ^= buffer[i];
            return crc;
        }

        // Khung "xoá bảng": mũi tên đen, ba số đen — tất cả biến mất.
        //
        // Cần thiết vì board GIỮ NGUYÊN nội dung cuối cùng vĩnh viễn, kể cả sau
        // khi client ngắt kết nối, và không có watchdog. Publisher chết mà không
        // xoá thì bảng tiếp tục quảng cáo số chỗ trống cũ, dẫn tài xế vào khu đã
        // đầy — không có tín hiệu nào cho biết bảng đang nói dối.
        public static LedHub Blank(int port)
        {
            var hub = new LedHub { Port = port };
            hub.Arrow.Color     = LedColor.Black;
            hub.Position1.Color = LedColor.Black;
            hub.Position2.Color = LedColor.Black;
            hub.Position3.Color = LedColor.Black;
            return hub;
        }
    }
}
