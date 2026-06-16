# Yêu cầu Kỹ thuật và Chức năng (Requirements Document)
## Dự án: Nâng cấp Giao diện Vận hành SCADA TotalParking

Tài liệu này chi tiết hóa các yêu cầu chức năng (đối chiếu với mục 4.2 của file [document_spec.docx](file:///c:/Users/HOME/source/repos/TotalParking/docs/document_spec.docx)) để nâng cấp giao diện vận hành SCADA hiện tại trên dự án ASP.NET MVC [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) từ bản Demo lên bản thương mại hoàn chỉnh.

---

## 1. Bản đồ Phân nhóm Giao diện & Chức năng (Mapping Matrix)

| #  | Chức năng từ Spec (Mục 4.2) | Màn hình/File Giao diện liên quan | Mô tả phạm vi hiển thị trên UI |
|----|---------------------------|----------------------------------|--------------------------------|
| 1  | Bản đồ layout tổng mặt bằng | [FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml) | Bản vẽ lưới 2D phân bổ 12 block đỗ xe theo vị trí vật lý. |
| 2  | Bản đồ chi tiết các Zone | [Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml) & [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) | SVG tương tác, hover nổi bật và click để zoom vào sơ đồ chi tiết. |
| 3  | Trạng thái real-time từng slot | [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) (Modal) | Grid khay Pallet với mã màu trực quan: Trống, Có xe, Lỗi, Khóa. |
| 4  | Mật độ xe theo zone | [Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml) & [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Hiển thị mật độ % và thanh đo; cảnh báo tự động khi quá tải. |
| 5  | Điều hướng giao thông thông minh | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Giao diện điều phối luồng xe, gợi ý vị trí đỗ tối ưu tự động. |
| 6  | Cập nhật trạng thái cơ khí | [Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml) & [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) | Hiển thị trạng thái kết nối PLC/HMI (chấm xanh lá/đỏ). |
| 7  | Tra cứu lịch sử và tìm xe | [Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml) & [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml) | Ô tìm kiếm theo Biển số/Thẻ từ, bảng kết quả định vị xe. |
| 8  | Chẩn đoán lỗi từ xa | [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) (Modal) & [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml) | Hiển thị chi tiết lỗi thiết bị của từng block và mã lỗi phần cứng. |
| 9  | Thống kê tần suất hoạt động | [Maintenance.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Maintenance.cshtml) | Biểu đồ thanh tiến trình đo hao mòn dựa trên Cycles/Runtime. |
| 10 | Cảnh báo 3 loại | [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml) & [Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml) | Phân cấp và lọc 3 loại lỗi: Vận hành, Thiết bị, Hạn bảo trì. |
| 11 | Quản lý thẻ từ (đồng bộ HMI) | [Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml) | Form tạo mới, khóa, xóa thẻ tháng/lượt; đồng bộ hiển thị biển số. |
| 12 | Đèn báo chỗ trống per block | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Trạng thái truyền thông điệp điều khiển đèn chỉ dẫn tại block. |
| 13 | Bảng LED tổng đầu hầm | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) & [Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml) | Chỉ số chỗ trống phân tách Cơ khí SUV / Sedan / Đỗ thường. |
| 14 | Bảng LED chỉ dẫn trong hầm | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Trạng thái PGS (Parking Guidance System) truyền thông tin chỉ hướng. |
| 15 | Màn hình LED ngoài trời | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Giám sát trạng thái truyền gói tin tới 3 ô LED Matrix outdoor. |
| 16 | Tích hợp AI-VDS (Phân loại) | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Tiếp nhận thông số Xe: loại xe, chiều cao, chiều dài từ Camera. |
| 17 | Màn hình trạm phân loại | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Hiển thị loại xe, loại thẻ khuyên dùng (Xanh/Vàng/Cam), bản đồ đi. |
| 18 | Tích hợp máy phát thẻ tự động | [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) | Chỉ thị trạng thái điều khiển máy phát thẻ tự động. |
| 19 | Báo cáo tần suất / lưu lượng | [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml) | Kết xuất biểu đồ cột mật độ hàng tuần và biểu đồ tròn phân loại xe. |
| 20 | Báo cáo lịch sử lỗi | [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml) & [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml) | Biểu đồ Pareto lỗi thiết bị và thời gian xuất hiện, thời gian reset. |
| 21 | Giám sát từ xa (web-based) | [Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml) | Hiển thị danh sách các Operator/Kỹ sư đang trực tuyến hệ thống. |
| 22 | Báo cáo doanh thu từ xa | [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml) | Tùy chọn kết xuất báo cáo tài chính/doanh thu (nếu có tích hợp). |
| 23 | Vận hành từ xa (Remote control)| [Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml) (Audit) & [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) | Lưu vết (Audit Log) các lệnh điều khiển từ xa của kỹ sư nhà thầu. |
| 24 | API tích hợp bên thứ 3 | [Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml) | Cấu hình tích hợp (Webhook, Swagger URL) đến hệ thống khác. |

---

## 2. Các Yêu cầu Chức năng Chi tiết (Functional Requirements - EARS Format)

### Màn hình Dashboard & Bản đồ
*   **REQ-FUN-001 (Bản đồ tổng mặt bằng):** 
    *   *Mô tả:* Hệ thống phải hiển thị bản đồ 2D trực quan của toàn bộ các tầng hầm đỗ xe trên màn hình [FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml), với các block được sắp xếp chính xác theo vị trí lắp đặt thực tế.
    *   *Tiêu chí nghiệm thu (AC):* 
        *   Khi hiển thị màn hình Mặt bằng hệ thống, lưới 2D phải hiển thị đầy đủ 12 block (B01 đến B12) chia theo 3 tầng (B1, B2, B3).
        *   Mỗi ô block trên sơ đồ phải thay đổi màu nền tương ứng với trạng thái thực tế nhận từ PLC (Xanh lá = Bình thường, Xanh dương = Đang chạy, Vàng = Cảnh báo, Đỏ = Lỗi, Tím = Bảo trì, Xám = Offline).

*   **REQ-FUN-002 (Bản đồ phân khu tương tác):**
    *   *Mô tả:* Khi người dùng rê chuột vào các phân khu trên màn hình [Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml), hệ thống phải làm nổi bật phân khu tương ứng bằng đường viền màu và cho phép người dùng click để chuyển hướng đến chi tiết phân khu đó.
    *   *Tiêu chí nghiệm thu (AC):*
        *   Hỗ trợ 6 phân khu tương tác bằng SVG Polygons đè lên ảnh nền.
        *   Khi click vào phân khu, trình duyệt phải điều hướng sang view [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) với đúng ID phân khu tương ứng.

*   **REQ-FUN-003 (Trạng thái và Thông số Pallet chi tiết):**
    *   *Mô tả:* Khi người dùng click vào một block trên màn hình [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml), hệ thống phải hiển thị Modal Popup chứa sơ đồ chi tiết trạng thái từng khay Pallet.
    *   *Tiêu chí nghiệm thu (AC):*
        *   Hiển thị đúng số lượng pallet theo sức chứa của block (10, 12, 14, hoặc 16 khay đỗ).
        *   Hiển thị thông tin xe đỗ trên khay: Loại xe (SUV/Sedan), biển số xe, và thời gian vào bãi.
        *   Cung cấp các nút hành động điều khiển: Lịch sử block, Bảo trì block, Chi tiết cảnh báo, và Cưỡng bức (Admin).

### Điều hướng Giao thông & Tích hợp AI-VDS
*   **REQ-FUN-004 (Tiếp nhận phân loại xe AI-VDS):**
    *   *Mô tả:* Khi hệ thống camera AI-VDS đo quét xe tại trạm phân loại đầu hầm, giao diện [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) phải hiển thị tức thời các chỉ số kích thước xe (Chiều cao, Chiều dài) và phân loại xe (Sedan/SUV/Quá khổ).
    *   *Tiêu chí nghiệm thu (AC):*
        *   Nếu chiều cao xe vượt quá giới hạn thiết kế (ví dụ >1.8m), hệ thống phải đánh dấu đỏ, hiển thị trạng thái "Từ chối" và đưa ra cảnh báo "Xe quá khổ – Từ chối vào bãi".
        *   Hệ thống phải đưa ra đề xuất loại thẻ phát tương ứng (Ví dụ: Thẻ Xanh cho Sedan, Thẻ Vàng cho SUV, Thẻ Cam cho xe đặc biệt).

*   **REQ-FUN-005 (Cân bằng tải và Điều hướng thông minh):**
    *   *Mô tả:* Hệ thống phải tự động đề xuất Block đỗ xe còn trống tối ưu nhất dựa trên logic cân bằng tải giữa các Zone và loại xe.
    *   *Tiêu chí nghiệm thu (AC):*
        *   Hiển thị block đề xuất trên màn hình trạm phân loại.
        *   Hiển thị trạng thái truyền tín hiệu chỉ dẫn tới: Màn hình giám sát 55", LED hiển thị ngoài trời, bảng LED chỉ dẫn trong hầm, và hệ thống PGS.

### Quản lý Vận hành & Cảnh báo
*   **REQ-FUN-006 (Quản lý Cảnh báo 3 loại):**
    *   *Mô tả:* Màn hình [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml) phải hiển thị và phân loại toàn bộ cảnh báo lỗi hệ thống thành 3 nhóm: 1) Lỗi thao tác vận hành, 2) Lỗi hệ thống/thiết bị, 3) Thiết bị đến hạn bảo trì.
    *   *Tiêu chí nghiệm thu (AC):*
        *   Cho phép người dùng tìm kiếm theo mã lỗi hoặc lọc theo mức độ (Nghiêm trọng, Cao, Trung bình, Thấp) và Trạng thái (Đang lỗi, Đã xác nhận, Đã xử lý).
        *   Cung cấp nút "Xác nhận" (Acknowledge) lỗi cho người vận hành.

*   **REQ-FUN-007 (Quản lý Thẻ xe và Đồng bộ HMI):**
    *   *Mô tả:* Người dùng phải có quyền thiết lập, khóa hoặc xóa thẻ từ (vé tháng/vé lượt) trực tiếp trên giao diện [Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml) và đồng bộ thông tin biển số xe tương ứng.
    *   *Tiêu chí nghiệm thu (AC):*
        *   Form thiết lập thẻ mới yêu cầu các trường: Mã thẻ, Biển số xe, Tên chủ xe, Loại xe đăng ký (SUV/Sedan).
        *   Hiển thị trạng thái đồng bộ thẻ từ hệ thống SCADA sang màn hình HMI tại từng block vật lý.

*   **REQ-FUN-008 (Theo dõi Hao mòn & Lên lịch Bảo trì):**
    *   *Mô tả:* Giao diện [Maintenance.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Maintenance.cshtml) phải hiển thị tích lũy số chu kỳ hoạt động (Cycles) và số giờ chạy (Runtime) của Motor của tất cả 12 Block để phục vụ bảo trì dự báo.
    *   *Tiêu chí nghiệm thu (AC):*
        *   Hiển thị thanh tiến trình hao mòn (%) so với giới hạn an toàn của thiết bị (ví dụ: Motor nâng giới hạn 6,000 chu kỳ).
        *   Hiển thị bảng danh sách các Work Order đề xuất kèm theo độ ưu tiên tự động (Khẩn cấp, Cao, Trung bình, Thấp).

### Báo cáo & Giám sát từ xa
*   **REQ-FUN-009 (Kết xuất Báo cáo Vận hành):**
    *   *Mô tả:* Người dùng phải xuất được báo cáo thống kê mật độ, lưu lượng và lỗi thiết bị dưới định dạng tệp Excel hoặc PDF trên màn hình [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml).
    *   *Tiêu chí nghiệm thu (AC):*
        *   Biểu đồ cột (Bar Chart) phải thể hiện mật độ lấp đầy theo từng ngày trong tuần.
        *   Biểu đồ tròn (Pie Chart) phải thể hiện chính xác cơ cấu loại xe đỗ.
        *   Bảng phân tích Pareto lỗi phải hiển thị tỷ lệ phần trăm lỗi tích lũy.

*   **REQ-FUN-010 (Ghi log vận hành - Audit Log):**
    *   *Mô tả:* Hệ thống phải ghi nhận mọi lệnh điều khiển quan trọng thực hiện từ giao diện vào bảng nhật ký Audit Log trên màn hình [Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml).
    *   *Tiêu chí nghiệm thu (AC):*
        *   Mỗi dòng ghi nhận Audit Log phải lưu trữ đầy đủ: Thời gian, Tài khoản thực hiện, Vai trò (Admin/Operator/Maintenance), Block chịu tác động, Nội dung lệnh, Kết quả (Thành công/Thất bại), và Lý do thao tác.

---

## 3. Các Yêu cầu Kỹ thuật Phi chức năng (Non-Functional Requirements)

*   **REQ-NFR-001 (Lưu trữ Database):** Cơ sở dữ liệu của hệ thống phải được thiết lập lưu trữ tối thiểu **2 năm dữ liệu lịch sử** đối với các sự kiện, log cảnh báo lỗi, và thông tin quẹt thẻ.
*   **REQ-NFR-002 (Cấu hình Truyền thông LED/ZCU):** 
    *   Tần suất quét trạng thái đèn báo chỗ trống tại các block phải đạt chu kỳ **2.5 giây** thông qua giao thức TCP/IP.
    *   Hệ thống phải tự động phát hiện mất kết nối với bảng LED hoặc ZCU và hiển thị cảnh báo tức thì cho người vận hành.
*   **REQ-NFR-003 (Logic Điều phối & Cân bằng tải):** 
    *   Khi mật độ đỗ giữa các Zone lệch nhau vượt quá ngưỡng cấu hình (ví dụ lệch >20%), hệ thống phải tự động kích hoạt logic điều phối xe sang Zone ít sử dụng hơn.
    *   Tín hiệu điều phối phải đồng thời xuất ra 3 đầu ra: Giao diện trạm phân loại (màn hình 55"), API hệ thống kiểm soát ra vào, và bảng LED chỉ dẫn.
