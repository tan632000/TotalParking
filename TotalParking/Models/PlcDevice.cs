namespace TotalParking.Models
{
    // Cấu hình một PLC Omron, đọc từ bảng plc_device.
    //
    // Địa chỉ thanh ghi để trong DB chứ không hard-code, vì ladder của từng block
    // có thể khác nhau và đổi cấu hình không nên phải build lại ứng dụng.
    // Mặc định là bộ đã chốt với bên lập trình PLC: D100 / W75.0 / D402.
    public class PlcDevice
    {
        public int PlcId    { get; set; }
        public int BlockId  { get; set; }
        // Số in trên bản vẽ — đây là mã mà nhân viên và HMI gọi block.
        public int BlockNo  { get; set; }
        public int ZoneId   { get; set; }

        public string IpAddress { get; set; }
        public int    Port      { get; set; }

        // Giá trị đề nghị. Bắt tay FINS/TCP trả về node được cấp phát và client
        // ghi đè lại, nên hai số này không phải sự thật cuối cùng.
        public byte PlcNode { get; set; }
        public byte PcNode  { get; set; }

        public int TimeoutMs { get; set; }
        public int PollMs    { get; set; }

        // PLC -> SCADA
        public int CardWord { get; set; }
        // 0 = chưa biết bố cục, đang dò.
        public int CardWordLen { get; set; }
        // null = chưa chốt. Xem CardCodeDecoder.
        public string CardLayout { get; set; }
        // null = ladder chưa có bit báo lượt quẹt mới. Khi đó hệ thống rơi về
        // cách so sánh giá trị D100, không phát hiện được hai lần quẹt cùng thẻ.
        public string RequestBit     { get; set; }
        public string RequestBitArea { get; set; }

        // SCADA -> PLC/HMI
        public string PermitBit     { get; set; }
        public string PermitBitArea { get; set; }
        public int    ClassWord     { get; set; }

        public bool IsActive { get; set; }

        public bool HasRequestBit
        {
            get { return !string.IsNullOrWhiteSpace(RequestBit); }
        }

        public string Endpoint
        {
            get { return IpAddress + ":" + Port; }
        }
    }
}
