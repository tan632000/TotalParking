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

        // ---- HOP DONG CU, DA BO ----
        // D402 (hang tai 2200/2600) va W75.0 (dung/sai block). Giu lai de khong
        // pha du lieu cu, nhung KHONG con duoc ghi nua. Thay bang FindAnswerWord:
        // W75.0 chi tra duoc dung/sai (1 bit), con D1000 tra ve SO BLOCK — tra loi
        // duoc "xe dang o dau" chu khong chi "co phai o day khong".
        public string PermitBit     { get; set; }
        public string PermitBitArea { get; set; }
        public int    ClassWord     { get; set; }

        // ---- HOP DONG MOI: tim xe ----
        // Khach quet RFID o mot block BAT KY -> PLC ghi ma the vao day.
        public int FindCardWord { get; set; }
        // So word cua ma the o FindCardWord. Ma the 32 bit = 2 word.
        public int FindCardLen  { get; set; }
        // SCADA ghi SO BLOCK noi xe dang dau vao day. 0 = khong tim thay.
        public int FindAnswerWord { get; set; }

        // ---- HOP DONG MOI: bang tai trong ----
        // Khach quet RFID -> PLC ghi ma the vao day (D106). Khac FindCardWord:
        // day la MOI luot quet, con FindCardWord rieng cho yeu cau tim xe.
        public int ScanCardWord { get; set; }
        // So word cua ma the o ScanCardWord. Ma the 32 bit = 2 word.
        public int ScanCardLen  { get; set; }
        // SCADA ghi bang tai trong vao day (D1004):
        //   1 duoi 2200 kg, 2 tu 2200 den 2600, 3 tren 2600, 0 khong biet.
        public int WeightBandWord { get; set; }

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
