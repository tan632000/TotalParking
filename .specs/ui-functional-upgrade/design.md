# Tài liệu Thiết kế Kỹ thuật (Technical Design Document)
## Dự án: Nâng cấp Giao diện Vận hành SCADA TotalParking

Tài liệu này định hình thiết kế giao diện cho quá trình nâng cấp giao diện ASP.NET MVC [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) đáp ứng đầy đủ các yêu cầu chức năng từ tài liệu đặc tả [document_spec.docx](file:///c:/Users/HOME/source/repos/TotalParking/docs/document_spec.docx) mục 4.2.

---

## 1. Tổng quan & Mục tiêu Thiết kế

### Mục tiêu chính:
*   Đồng bộ toàn diện giao diện ASP.NET MVC [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) với các chức năng yêu cầu thương mại (mục 4.2).
*   Đảm bảo khả năng hiển thị tĩnh (mockup) tất cả các kịch bản lỗi, thông số thực tế của trạm phân loại xe AI-VDS, cân bằng tải, quản lý thẻ, và nhật ký hoạt động.
*   Thiết kế giao diện tối ưu hóa cho người vận hành tại phòng điều khiển (Operator).

### Phạm vi ảnh hưởng:
*   Chỉ can thiệp vào tầng View: Các file Razor View (`.cshtml`) trong thư mục [Views/Home/](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home).
*   Không làm thay đổi cấu trúc cơ sở dữ liệu thực tế và các API truyền thông thời gian thực (các thành phần này sẽ được tích hợp ở các giai đoạn sau).

---

## 2. Thiết kế chi tiết cho từng Màn hình

### 2.1. Dashboard Tổng quan ([Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml))
*   **Thiết kế bổ sung:**
    *   Tích hợp dải thông báo khẩn cấp (Alarm Active Alert) nhấp nháy đỏ trên đầu trang.
    *   Bảng trạng thái kết nối phần cứng (PLC, HMI, AI-VDS, LED/PGS, SCADA Server) hiển thị trạng thái kết nối màu Xanh lá/Đỏ.
    *   Bảng log 5 cảnh báo gần nhất bao gồm thông tin: Thời gian, Mức độ (Critical, Major, Minor), Zone, Block, Mô tả sự cố, Trạng thái (Đang lỗi, Đã biết).
*   **Dữ liệu mô phỏng (Mock data):**
    *   Tổng Block: `9`, Tổng Pallet: `184`, Pallet có xe: `91` (`49%`), Pallet trống: `69`.
    *   Phân loại xe đỗ: SUV: `37`, Sedan: `54`.

### 2.2. Mặt bằng Hệ thống ([FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml))
*   **Thiết kế bổ sung:**
    *   Quy hoạch sơ đồ lưới 2D hiển thị đầy đủ 12 block (chia làm 3 tầng hầm: B1, B2, B3).
    *   Thêm chú thích mã màu cho các trạng thái: Bình thường, Đang chạy, Cảnh báo, Lỗi, Bảo trì, Offline.
    *   Bảng theo dõi mật độ và số khay đỗ thực tế của từng phân khu (Zone A: 28/44, Zone B: 20/52, Zone C: 15/60).
    *   *Tương tác:* Click vào mỗi ô block trên lưới sẽ dẫn trực tiếp sang view chi tiết phân khu tương ứng.

### 2.3. Bản đồ Phân khu ([Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml)) & Chi tiết Phân khu ([ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml))
*   **Thiết kế bổ sung cho [Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml):**
    *   Sử dụng hình vẽ phân khu trong file [Images/zones_map.jpeg](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Images/zones_map.jpeg).
    *   Thiết kế 6 vùng đa giác SVG (Zone 1 đến Zone 6) với hiệu ứng đổi màu viền, làm sáng và đổ bóng khi di chuột qua.
*   **Thiết kế bổ sung cho [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml):**
    *   Hiển thị lưới 4 block của phân khu được chọn. Mỗi block hiển thị chi tiết: Sức chứa, số xe đỗ, tỷ lệ SUV/Sedan, Chế độ (Tự động/Bằng tay), thanh tiến trình % đỗ, và đèn báo trạng thái PLC/HMI.
    *   *Modal Chi tiết Block:* Khi click vào block, mở popup hiển thị telemetry của block (Số chu kỳ hoạt động, giờ hoạt động Motor) và lưới khay Pallet (Pallet Grid).
    *   *Thiết kế khay Pallet:*
        *   Khay có xe (màu xanh lá): Hiển thị mã pallet, biển số xe, loại xe, thời gian gửi.
        *   Khay trống (màu trắng): Hiển thị chữ P.
        *   Khay lỗi (màu đỏ): Hiển thị icon cảnh báo.
        *   Khay khóa (màu tím): Hiển thị icon ổ khóa.

### 2.4. Điều hướng Xe – AI-VDS ([Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml))
*   **Thiết kế bổ sung:**
    *   Thanh cảnh báo mất kết nối AI-VDS dẫn tới kích hoạt chế độ Fallback Mode.
    *   Bảng theo dõi mật độ và số khay đỗ SUV còn lại của từng Zone (Zone A, B, C).
    *   Bảng danh sách xe đang vào cổng (biển số, kích cỡ dài/cao, đề xuất block đỗ tối ưu, thẻ xe cấp phát, trạng thái dẫn đường hoặc từ chối do quá khổ).
    *   Trạng thái truyền tin đến LED Display, PGS và máy phát hành thẻ tự động.

### 2.5. Nhật ký Cảnh báo – Alarm & Event ([Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml))
*   **Thiết kế bổ sung:**
    *   Bộ lọc và tìm kiếm sự cố theo mã lỗi hoặc từ khóa mô tả.
    *   Bảng danh sách cảnh báo chi tiết phân loại rõ 3 loại sự cố: Lỗi thao tác, Lỗi thiết bị/hệ thống, Thiết bị đến hạn bảo trì.
    *   Hiển thị nút Xác nhận lỗi (Acknowledge) cho người vận hành.

### 2.6. Bảo trì Hệ thống ([Maintenance.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Maintenance.cshtml))
*   **Thiết kế bổ sung:**
    *   Giám sát tần suất hoạt động theo số chu kỳ (Cycles) và số giờ chạy Motor (Runtime) dưới dạng thanh tiến trình hao mòn thiết bị của tất cả 12 Block.
    *   Bảng danh sách đề xuất Work Order tự động (Độ ưu tiên: Khẩn cấp, Cao, Trung bình, Thấp) đối với các cơ cấu truyền động chính (Motor nâng, Motor trượt, xích, dầu hộp số...).

### 2.7. Báo cáo Hệ thống ([Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml))
*   **Thiết kế bổ sung:**
    *   Menu lựa chọn báo cáo: Báo cáo mật độ/lưu lượng xe, báo cáo lỗi, báo cáo bảo trì, báo cáo thẻ, báo cáo doanh thu từ xa.
    *   Biểu đồ Pareto lỗi thiết bị (Loại lỗi, số lần xuất hiện, tỷ lệ phần trăm tích lũy).
    *   Hỗ trợ xuất báo cáo Excel/PDF (bằng giao diện nút xuất tĩnh).

### 2.8. Quản lý Thẻ xe ([Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml))
*   **Thiết kế bổ sung:**
    *   Form thiết lập thẻ từ mới liên kết trực tiếp với biển số xe và loại xe (SUV/Sedan).
    *   Bảng danh sách thẻ xe đang lưu hành tích hợp chỉ số trạng thái đồng bộ thẻ từ SCADA xuống HMI tại block vật lý.

### 2.9. Cài đặt Hệ thống ([Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml))
*   **Thiết kế bổ sung:**
    *   Các ô cấu hình Modbus TCP, polling interval, thông tin CSDL, email/SMS nhận cảnh báo.
    *   Bộ nút cấu hình giao diện vận hành (Dark Mode, Auto Ack alarm, Âm thanh thông báo).
    *   Bảng chẩn đoán tài nguyên phần cứng của SCADA Server (Uptime, CPU/RAM usage, dung lượng DB).

### 2.10. Remote Support & Phân quyền ([Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml))
*   **Thiết kế bổ sung:**
    *   Sơ đồ trực quan các quyền hạn tương ứng của 5 nhóm người dùng (Admin, Supervisor, Maintenance, Operator, Remote Support).
    *   Bảng ghi nhận Audit Log vận hành các lệnh điều khiển quan trọng (Reset Alarm, Chuyển Maintenance Mode, Forced Mode...) để phục vụ truy vết từ xa.
