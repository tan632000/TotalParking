using System;
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

            // Hai bộ đếm cơ khí lấy số từ thanh ghi PLC, nên chúng — và chỉ chúng
            // — chịu ảnh hưởng của việc PLC mất kết nối.
            bool trusted = IsCoverageOk(capacity);

            // X5.X6 — Mechanical L < 5 M
            hub.Position1.Value = capacity.FreeL5m;
            hub.Position1.Color = ColorFor(capacity.FreeL5m, capacity.TotalL5m, trusted);

            // X7.X8 — Mechanical L < 4.8 M
            hub.Position2.Value = capacity.FreeL48m;
            hub.Position2.Color = ColorFor(capacity.FreeL48m, capacity.TotalL48m, trusted);

            // X9.X10 — Standard
            //
            // KHÔNG áp độ phủ cho dòng này. Độ phủ đo bằng số ô cơ khí vừa được
            // PLC đọc lại, mà chỗ đỗ thường không đi qua PLC — nó đến từ cảm
            // biến PGS qua CCU (xem StandardFreeSource). Áp một thước đo của
            // tầng khác vào đây thì dòng này bật vàng vĩnh viễn và cảnh báo mất
            // hết ý nghĩa.
            //
            // Con số này chỉ thật ở cổng TOTAL. Cổng ZONES vẫn lấy từ CSDL vì
            // chưa biết cảm biến nào thuộc zone nào.
            hub.Position3.Value = capacity.FreeStandard;
            hub.Position3.Color = ColorFor(capacity.FreeStandard, capacity.TotalStandard, true);

            return hub;
        }

        // Đỏ khi hết chỗ, vàng khi sắp hết, xanh khi còn thoải mái.
        // Bảng là module P10-RG hai màu và hiện được cả ba màu này — đã xác nhận
        // bằng probe trên phần cứng thật.
        //
        // ===================== KHÔNG BAO GIỜ TẮT SỐ =====================
        // Trước đây chỗ này trả về ĐEN khi `total == 0` để số biến mất khỏi bảng.
        // Khách bác bỏ (17/09) và họ đúng: nhãn "ĐỖ CƠ KHÍ L < 4.8 M" được IN CỐ
        // ĐỊNH trên mặt bảng, nên nhãn in sẵn kèm ô trống trông như bảng hỏng và
        // người vận hành sẽ đi tìm lỗi thiết bị. `0000` ít nhất cho biết hệ thống
        // đang sống và đang trả lời — kiểm chứng được, còn ô trống thì không.
        //
        // ===================== HAI TÌNH HUỐNG CÙNG RA `0000` =====================
        // Giống hệt nhau trên mặt bảng nhưng ý nghĩa ngược nhau, nên màu khác nhau:
        //
        //   total <= 0  bãi KHÔNG CÓ loại khoang này -> `0000` XANH
        //               Không phải tin xấu, chỉ là dòng không áp dụng cho bãi này.
        //               Khách chốt ngày 17/09: hiện xanh chứ không đỏ.
        //
        //   free <= 0   loại khoang này CÓ, nhưng ĐÃ HẾT CHỖ -> `0000` ĐỎ
        //               Đây là tin xấu thật, và là thứ duy nhất khiến tài xế quay
        //               đầu. Để xanh là nói với họ còn chỗ trong khi bãi đã đầy.
        //               CHƯA ĐƯỢC KHÁCH XÁC NHẬN đổi, nên giữ đỏ.
        //
        // LƯU Ý: cả hai khác với cơ chế xoá bảng ở LedPublisher. Ở đó, khi CHƯA CÓ
        // dữ liệu sức chứa thì xoá sạch cả bảng — vì đẩy `0 0 0` lúc chưa biết gì
        // là bịa ra "bãi đã đầy". Ở đây `0` là một sự thật đã biết, không phải sự
        // thiếu hiểu biết.
        private static LedColor ColorFor(int free, int total, bool trusted)
        {
            // MỘT MÀU DUY NHẤT — khách chốt ngày 17/09.
            //
            // Yêu cầu nguyên văn: "chỉ dùng màu xanh, không dùng các màu khác",
            // "update toàn bộ sử dụng màu xanh trong mọi trường hợp".
            //
            // Tôi đã nêu hệ quả trước khi làm (mất cảnh báo hết chỗ và mất cảnh
            // báo độ phủ thấp) và khách vẫn giữ quyết định. Ghi lại ở đây để lần
            // sau ai đọc mã không tưởng là lỗi rồi "sửa" ngược lại.
            //
            // KHÔNG xoá luật cũ mà đưa ra sau công tắc led:numberColor, để bật
            // lại chỉ cần sửa Web.config, không phải viết lại và build.
            if (!SemanticColors) return LedColor.Green;

            if (total <= 0)           return LedColor.Green;   // không có loại khoang này
            if (free <= 0)            return LedColor.Red;     // có nhưng hết chỗ
            if (free <= LowThreshold) return LedColor.Yellow;
            return trusted ? LedColor.Green : LedColor.Yellow;
        }

        // led:numberColor = "auto"  -> bật lại bộ luật màu theo ngữ nghĩa
        //                   bất kỳ giá trị nào khác (hoặc thiếu) -> luôn XANH
        //
        // Mặc định là LUÔN XANH: đó là trạng thái khách yêu cầu, và mặc định phải
        // trùng với thứ đang chạy ngoài hiện trường. Nếu mặc định là "auto" thì
        // chỉ cần một lần deploy thiếu dòng cấu hình là bảng đổi màu mà không ai
        // biết vì sao.
        private static bool SemanticColors
        {
            get
            {
                string v = System.Configuration.ConfigurationManager
                               .AppSettings["led:numberColor"];
                return v != null && v.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);
            }
        }

        // Ngưỡng độ phủ để số được hiện màu xanh. Dưới ngưỡng thì số vẫn hiện
        // nhưng chuyển sang VÀNG.
        //
        // ===================== VÌ SAO VÀNG, KHÔNG PHẢI MÀU RIÊNG =====================
        // Bảng là module P10-RG, chỉ có đỏ và xanh (vàng = bật cả hai). Không tồn
        // tại màu thứ tư, nên "sắp hết chỗ" và "số chưa chắc chắn" buộc phải dùng
        // chung màu vàng. Với tài xế thì cả hai đều mang một nghĩa: **đừng tin
        // tuyệt đối vào con số này**. Đó là lý do việc dùng chung chấp nhận được.
        //
        // ===================== VÌ SAO KHÔNG TẮT SỐ ĐI =====================
        // Xoá trắng thì tài xế không có thông tin gì. Con số phủ 30% vẫn hữu ích
        // hơn không có gì — nó chỉ không được phép trông chắc chắn như số phủ 95%.
        //
        // Mặc định 70%: dưới mức đó thì gần một phần ba số ô là suy đoán.
        private const int DefaultMinCoveragePct = 70;

        private static bool IsCoverageOk(LedCapacity capacity)
        {
            int pct = capacity.CoveragePct;

            // -1 = phạm vi này không có ô cơ khí nào. Không phải "độ phủ kém",
            // mà là "không có gì để phủ" — để nguyên xanh.
            if (pct < 0) return true;

            int min;
            string v = System.Configuration.ConfigurationManager
                           .AppSettings["led:minCoveragePct"];
            if (!int.TryParse(v, out min)) min = DefaultMinCoveragePct;

            // 0 = tắt hẳn cơ chế này.
            if (min <= 0) return true;

            return pct >= min;
        }
    }
}
