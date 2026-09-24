using System.Linq;
using TotalParking.Services.Pgs;

namespace TotalParking.Services.Led
{
    // Số chỗ đỗ thường còn trống, lấy từ cảm biến PGS thay vì từ CSDL.
    //
    // ===================== VÌ SAO CẦN LỚP NÀY =====================
    // `v_led_capacity.free_standard` tính bằng `tổng ô - số phiên gửi xe`. Khu đỗ
    // thường không phát thẻ nên không có phiên nào, và con số đó đứng yên ở 80
    // trong khi cảm biến đếm được 31 xe đang đỗ. Bảng đầu hầm vì thế nói với tài
    // xế rằng còn trống toàn bộ bãi.
    //
    // ===================== CHỈ ÁP CHO CỔNG TOTAL =====================
    // Cổng khai báo ZONES cần số của riêng zone mà mũi tên dẫn tới, mà hiện chưa
    // biết cảm biến nào thuộc zone nào: có 5 ZCU cho 6 zone, và số cảm biến mỗi
    // ZCU (21/17/18/16/7) không khớp số ô mỗi zone (13/18/2/17/11/19).
    //
    // Đoán ánh xạ rồi đẩy lên mũi tên chỉ hướng là chỉ sai đường — tệ hơn hẳn
    // việc chưa chỉ. Nên lớp này chỉ được gọi ở đường số toàn bãi.
    public static class StandardFreeSource
    {
        // Số ô trống theo cảm biến, hoặc null khi chưa từng đọc được gói nào.
        //
        // ===================== ĐẾM GÌ LÀ TRỐNG =====================
        // Chỉ những cảm biến báo đúng trạng thái "trống" (ký tự '0'). Ô lỗi và ô
        // không có cảm biến KHÔNG được tính là trống — người dùng chốt ngày
        // 24/09. Số báo thấp hơn sức chứa thật một chút, đổi lại tài xế đi theo
        // bảng thì chắc chắn có chỗ.
        //
        // ===================== TÍNH CẢ ZCU ĐANG ĐÓNG BĂNG =====================
        // Khác endpoint chẩn đoán, vốn tách hai nhóm để người vận hành thấy rõ
        // cái nào cũ. Bảng LED chỉ hiện được một con số, và người dùng chốt giữ
        // số cuối cùng đọc được: bãi đỗ thường biến động chậm nên số cũ vài phút
        // vẫn gần đúng, còn quay về con số 80 của CSDL là quay lại nói dối.
        public static int? SoOTrong()
        {
            var ccu = PgsHost.Ccu;
            if (ccu == null) return null;

            var zcus = ccu.Snapshot();
            if (zcus.Count == 0) return null;

            return zcus.Sum(z => z.Trong);
        }
    }
}
