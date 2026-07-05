# Nghiên cứu & Quyết định Thiết kế - Operation Control Center (OCC)

## Summary
- **Feature**: operation-control-center
- **Discovery Scope**: New Feature & Extension
- **Key Findings**:
  - Hệ thống sử dụng kiến trúc ASP.NET MVC với Razor Views. Tất cả điều phối và giả lập được định hướng viết bằng Javascript trên client-side để chạy trực quan không cần kết nối PLC thực tế ở giai đoạn UI/UX này.
  - Sidebar layout chứa các liên kết dạng `<a href="@Url.Action("Action", "Controller")">` và cần bổ sung mục điều hướng OCC.
  - Trạng thái lỗi và dữ liệu khay đỗ được lưu trữ hoặc đồng bộ qua `localStorage` (ví dụ `palletCycleData` và cảnh báo activeAlarms từ các task trước).

## Quyết định Thiết kế

### Quyết định 1: Tạo mới Route và View điều khiển OCC
- **Context**: Tách biệt luồng giám sát thụ động (FloorPlan, Zones) với luồng điều khiển chủ động (OCC).
- **Selected Approach**: Thêm Action `OperationControl()` trong `HomeController.cs` trả về `View()`. Tạo mới `OperationControl.cshtml` trong thư mục `Views/Home/`.
- **Rationale**: Đảm bảo cấu trúc sạch sẽ theo chuẩn ASP.NET MVC, không gây loãng các trang giám sát hiện có.

### Quyết định 2: Giả lập PLC Command Pipeline bằng State Machine trong JS
- **Context**: Yêu cầu giám sát chu kỳ trạng thái lệnh PLC: Command Sent -> PLC Accepted -> Running -> Done/Failed.
- **Selected Approach**: Xây dựng một hàng đợi lệnh (Command Queue) và máy trạng thái (State Machine) bằng JavaScript. Khi gửi lệnh, kích hoạt bộ đếm thời gian (timer) để dịch chuyển trạng thái LED từ bước này sang bước khác.
- **Rationale**: Cho phép mô phỏng thời gian thực một cách sinh động trực quan trên giao diện SCADA mà không phụ thuộc hạ tầng PLC thật.

### Quyết định 3: Lưu trữ Checklist phục hồi sự cố trong localStorage
- **Context**: Đảm bảo trạng thái checklist phục hồi sự cố không bị mất khi operator F5 lại trình duyệt (Yêu cầu Reliability).
- **Selected Approach**: Khi checklist thay đổi (checked/unchecked), lưu toàn bộ trạng thái checklist dạng đối tượng JSON vào `localStorage`. Khi load trang, nếu Block đang ở trạng thái lỗi và có checklist lưu trữ, tự động nạp lại tiến trình cũ.
- **Rationale**: Tối ưu hóa trải nghiệm người dùng ca trực vận hành, tránh mất dữ liệu checklist khi mất kết nối mạng tạm thời hoặc vô tình tải lại trang.

## Rủi ro & Giải pháp giảm thiểu
- **Rủi ro xung đột dữ liệu trạng thái block**: Trạng thái block có thể bị mâu thuẫn giữa trang OCC, Maintenance, và Index.
  - *Giải pháp*: Đồng nhất các key lưu trữ trong `localStorage` (như `palletCycleData` và `activeAlarms`). Ghi đè trạng thái lỗi thiết bị về bình thường trong toàn bộ các view khi Reset PLC được gọi thành công.
