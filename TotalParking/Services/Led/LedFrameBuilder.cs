using TotalParking.Models;

namespace TotalParking.Services.Led
{
    // Dựng khung tin cho một cổng hiển thị từ số chỗ trống.
    //
    // ĐIỂM DỄ SAI NHẤT CỦA CẢ TẦNG LED nằm ở đây: thứ tự ba bộ đếm trong khung
    // là L<5M → L<4.8M → Standard, trong khi thứ tự enum LedLane là
    // L48M → L5M → Normal. **Hai phần tử đầu đảo nhau.**
    //
    // Gán nhầm thì không có gì crash, không có test nào đỏ — bảng chỉ hiện sai
    // số và không ai phát hiện cho tới khi có người đối chiếu bằng tay. Vì vậy
    // ba dòng gán ở dưới ghi kèm mã trường của khung tin.
    public static class LedFrameBuilder
    {
        // Ngưỡng đổi màu số. Con số tuyệt đối chứ không phải phần trăm: tài xế
        // nhìn bảng cần biết "còn mấy chỗ", và 3 chỗ trống là ít dù bãi to hay nhỏ.
        private const int LowThreshold = 3;

        public static LedHub Build(LedPanelPort port, LedCapacity capacity)
        {
            var hub = new LedHub { Port = port.PortIndex };

            hub.Arrow.Direction = (LedDirection)port.ArrowDirection;
            hub.Arrow.Color     = (LedColor)port.ArrowColor;
            hub.Arrow.State     = (LedArrowState)port.ArrowState;

            // X5.X6 — Mechanical L < 5 M
            hub.Position1.Value = capacity.FreeL5m;
            hub.Position1.Color = ColorFor(capacity.FreeL5m);

            // X7.X8 — Mechanical L < 4.8 M
            hub.Position2.Value = capacity.FreeL48m;
            hub.Position2.Color = ColorFor(capacity.FreeL48m);

            // X9.X10 — Standard
            hub.Position3.Value = capacity.FreeStandard;
            hub.Position3.Color = ColorFor(capacity.FreeStandard);

            return hub;
        }

        // Đỏ khi hết chỗ, vàng khi sắp hết, xanh khi còn thoải mái.
        // Bảng là module P10-RG hai màu và hiện được cả ba màu này — đã xác nhận
        // bằng probe trên phần cứng thật.
        private static LedColor ColorFor(int free)
        {
            if (free <= 0)            return LedColor.Red;
            if (free <= LowThreshold) return LedColor.Yellow;
            return LedColor.Green;
        }
    }
}
