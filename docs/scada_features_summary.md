# Bản Tổng Hợp Các Chức Năng Theo Từng Tab Sidebar SCADA - TotalParking

Tài liệu này tổng hợp chi tiết toàn bộ các tính năng vận hành, tham số kỹ thuật và các kịch bản mô phỏng đã được triển khai trên hệ thống SCADA TotalParking, được phân chia cụ thể theo từng **Tab điều hướng** trên thanh Sidebar.

---

## 1. Tab: Dashboard Tổng Quan ([Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml))
*Màn hình giám sát trực quan trung tâm, tổng hợp các tham số vận hành chung của bãi Puzzle.*

*   **Widget SCADA Alarms Live (Nhật ký cảnh báo động):**
    *   Bảng nhật ký live feed tự động cập nhật cảnh báo mới nhất với hiệu ứng trượt mượt mà.
    *   *Phân loại 3 nhóm cảnh báo bằng màu sắc và icon (Visual Coding):*
        1.  **Lỗi thao tác vận hành (Operator Error):** Tông màu Vàng/Cam (Amber) – ví dụ: Nút E-stop bị nhấn, mở tủ điện sai quy trình.
        2.  **Lỗi hệ thống / Thiết bị (System Fault):** Tông màu Đỏ nhấp nháy phát sáng – ví dụ: Lỗi quá nhiệt động cơ nâng hạ, lỗi che chắn cảm biến quang.
        3.  **Đến hạn bảo trì (Maintenance Due):** Tông màu Xanh dương/Cyan – ví dụ: Hao mòn pallet, mâm xoay khô dầu.
*   **Đồng bộ Alarms liên trang (Key `activeAlarms` trong `localStorage`):**
    *   Khi operator bấm **Reset PLC** ở trang OCC, cảnh báo lỗi tương ứng lập tức tự động biến mất khỏi Dashboard này.
*   **Tự động kích hoạt cảnh báo bảo trì:**
    *   Hệ thống quét chu kỳ hoạt động của các pallet trong trình duyệt. Bất kỳ pallet nào có số chu kỳ `>= 100` (hao mòn trên 100%) sẽ tự động kích hoạt cảnh báo **Đến hạn bảo trì (loại 3)** trên màn hình.
*   **Banner cảnh báo nguy cấp:**
    *   Dòng chữ đỏ chạy ở đầu trang hiển thị lỗi mới nhất. Nhấn nút **Xác nhận (Ack)** để tắt banner và giảm bộ đếm số lượng lỗi.

---

## 2. Tab: Mặt Bằng Hệ Thống ([FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml))
*Trực quan hóa sơ đồ mặt bằng bãi đỗ xe và định vị vị trí các block đỗ xe Puzzle.*

*   **Sơ đồ mặt bằng 2D tương tác:** Hiển thị vị trí vật lý của các block đỗ xe Puzzle theo từng phân khu (Zone A đến Zone F).
*   **Chỉ báo trạng thái Block nhanh:** Click vào từng block trên mặt bằng hiển thị popup thông số trạng thái hiện tại (Đầy, Rảnh, Lỗi, Bảo trì).
*   **Liên kết chi tiết Zone:** Cho phép chuyển đổi nhanh đến View chi tiết Zone ([ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml)) để giám sát trực tiếp.

---

## 3. Tab: Điều Hướng Xe ([Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml))
*Điều phối xe vào cổng, cân bằng tải trọng và phân loại kích thước bằng AI.*

*   **Hệ thống AI-VDS (Vehicle Detection System):**
    *   Hiển thị thông số quét kích thước và biển số xe ở cổng kiểm soát.
    *   *AI-VDS Fallback Mode:* Báo động và hiển thị cảnh báo nhấp nháy khi mất tín hiệu heartbeat từ cổng AI chính.
*   **Cân bằng tải các Phân khu (Zone Balancer):**
    *   Hiển thị mật độ đỗ xe hiện tại của các Zone A, B, C, D dưới dạng thanh Progress bar phân màu (Xanh: tải nhẹ, Cam: tải trung bình, Đỏ: quá tải).
    *   Hiển thị số khay đỗ trống cho xe SUV và Sedan còn lại ở mỗi Zone.
*   **Hàng đợi phương tiện (Queue Monitor):** Giám sát danh sách xe đang xếp hàng chờ điều phối vào bãi.

---

## 4. Tab: Alarm & Event ([Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml))
*Nhật ký lưu trữ toàn bộ lịch sử các cảnh báo và sự kiện trong hệ thống.*

*   **Bộ lọc lịch sử Alarm chuyên sâu:** Bộ lọc theo Phân loại lỗi (Thiết bị / Thao tác / Bảo trì) và Mức độ nghiêm trọng (Nghiêm trọng / Cao / Trung bình / Thấp) kèm ô tìm kiếm theo từ khóa.
*   **Bảng Audit Log chi tiết:** Ghi nhận Thời gian, Phân loại, Mức độ, Zone, Block, Thiết bị lỗi, Mã lỗi, Mô tả chi tiết, Trạng thái (Đã xác nhận / Chưa xử lý) và Tên người xác nhận (Ack Person).
*   **Xuất báo cáo sự cố:** Hỗ trợ nút xuất Excel báo cáo sự cố để lưu trữ.

---

## 5. Tab: Bảo Trì ([Maintenance.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Maintenance.cshtml))
*Tab Bảo trì hệ thống và chẩn đoán lỗi phần cứng từ xa dành cho kỹ sư cơ - điện.*

*   **Tab 1: Lịch bảo trì:** Quản lý các phiếu yêu cầu sửa chữa (Work Order) và đồ thị hao mòn.
*   **Tab 2: Chẩn đoán lỗi từ xa (Remote Fault Diagnosis):**
    *   *Sơ đồ CAD cơ khí 2D tương tác:* Thể hiện trực quan vị trí Động cơ nâng hạ, Shuttle, Cảm biến hành trình, Photogate an toàn.
    *   *Giả lập lỗi phần cứng:*
        *   `Block B-04` báo lỗi Lift Motor nhấp nháy đỏ do quá nhiệt ở `115°C` (Mã lỗi: `ERR-MTR-04`).
        *   `Block B-02` báo cảnh báo Limit Switch LS-T3 nhấp nháy cam (Trễ phản hồi).
        *   `Block B-03` báo lỗi Photogate nhấp nháy đỏ (Phát hiện vật cản).
    *   *Bảng Telemetry & I/O PLC:* Click chọn thiết bị trên sơ đồ để đọc điện áp (V), dòng điện (A), nhiệt độ (°C), tốc độ vòng quay (RPM), địa chỉ thanh ghi PLC I/O thực tế (ví dụ: `I:0/2 = ON`) và hướng dẫn troubleshooting.
    *   *Điều khiển chẩn đoán từ xa:* Nút nhấn **Reset PLC/Ack**, nút **Bypass Sensor** (cưỡng bức cảm biến thành OK) và nút **Tạo Work Order** sự cố khẩn cấp đẩy sang Tab 1.

---

## 6. Tab: Báo Cáo ([Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml))
*Tra cứu lịch sử xe vào/ra bãi và thống kê hao mòn khay đỗ.*

*   **Tab 1: Lịch sử & Tìm xe trực quan:**
    *   *Lọc lịch sử gửi xe chéo:* Kết hợp biển số/mã thẻ, thời gian, loại hành động, trạng thái đỗ và zone.
    *   *Sơ đồ vị trí khay đỗ cơ khí (Mini Grid 3x4):* Khi tìm xe, hiển thị sơ đồ block. Khay chứa xe mục tiêu nhấp nháy vàng rực rỡ, các khay trống nét đứt, khay khác xám mờ. Click **Yêu cầu trả xe (Retrieve)** chạy progress % giả lập PLC.
*   **Tab 2: Tần suất hoạt động khay đỗ (Pallet Heatmap):**
    *   *Sơ đồ nhiệt 3x4 hao mòn:* Đổi màu khay dựa trên số chu kỳ chạy (Đỏ `>= 100` cần bảo trì, Cam `50-99` cảnh báo, Xanh `< 50` tốt).
    *   *Bảo trì & Reset chu kỳ:* Chọn khay đỗ bị đỏ (hao mòn trên 100%) -> Click **"Bảo trì & Reset chu kỳ chạy"** để đưa số chu kỳ về 0 (đổi khay đỗ sang màu xanh lá cây thời gian thực).

---

## 7. Tab: Quản Lý Thẻ ([Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml))
*Quản trị danh sách thẻ vé tháng, liên kết biển số xe và đồng bộ thiết bị đầu đọc HMI.*

*   **KPIs Dàn Ngang:** KPIs (Tổng số thẻ, Thẻ hoạt động, Thẻ đang khóa/hết hạn) được dàn ngang hàng trực quan bằng Grid.
*   **Quản trị biển số xe:** Hiển thị rõ ràng biển số xe gắn với từng thẻ.
*   **Lọc Block HMI:** Tự động lọc block HMI tương ứng khi chọn phân khu (Zone).
*   **Simulated HMI Sync Progress:** Khi Đăng ký / Khóa / Xóa thẻ, chạy tiến trình nhấp nháy màu vàng `⟳ HMI SYNC: 0%` -> `100%`. Hoàn thành chuyển sang xanh lá `● ĐÃ ĐỒNG BỘ` hoặc báo khóa `⚠ ĐÃ KHÓA HMI`.

---

## 8. Tab: Cài Đặt Hệ Thống ([Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml))
*Cấu hình các tham số hệ thống và truyền thông cơ sở dữ liệu.*

*   **Cấu hình Modbus TCP & PLC Connection:** Chỉnh sửa IP SCADA Server, Port Modbus (mặc định 502), chu kỳ Polling quét dữ liệu (ms) và Timeout kết nối.
*   **Cơ sở dữ liệu SCADA:** Chỉnh sửa DB Host Address, DB Name, thời gian lưu lịch sử cảnh báo (ngày) và tần suất tự động Backup.
*   **Phân phối cảnh báo:** Cấu hình Email nhận cảnh báo Critical, Số điện thoại nhận tin nhắn SMS và ngưỡng báo động cảm biến rung lắc.

---

## 9. Tab: Điều Khiển Vận Hành ([OperationControl.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/OperationControl.cshtml))
*Trung tâm điều khiển vận hành OCC chuyên nghiệp tích hợp toàn bộ các nút thao tác.*

*   **Tab 1: Bản đồ vận hành (Operational Map):** Giám sát lưới block và Pallet Grid 3x4 của block được chọn.
*   **Tab 2: Sequence View (Luồng xử lý lệnh từng bước):**
    *   *Tự động switch tab:* Khi Gọi/Trả xe, tự động nhảy sang Tab này.
    *   *Timeline trạng thái động:* Hiển thị 9 bước gửi xe (Store) hoặc 8 bước lấy xe (Retrieve). Trực quan hóa bước: Xanh (Hoàn thành), Vàng nhấp nháy (Đang chạy), Xám (Chờ), Đỏ (Lỗi kẹt).
    *   *Đồng hồ bấm giờ:* Đo thời gian elapsed chạy thực tế và estimated dự kiến.
    *   *Giả lập kẹt cơ cấu:* Check "Giả lập kẹt" khiến tiến trình bị kẹt đỏ ở Bước 6, chuyển block sang trạng thái `Error` và đẩy Alarm lên Dashboard chính.
*   **Cockpit điều khiển chi tiết block:** Auto/Manual/Maint/Emergency và Start/Stop/Pause/Resume, Lock/Unlock, Disable bảo trì.
*   **Guided Fault Recovery Checklist:** Khi block bị Error, hiển thị Panel cảnh báo lỗi đỏ và checklist hướng dẫn an toàn thực địa. Khóa nút Reset PLC cho đến khi tích đủ checkbox. Reset thành công chuyển block sang Normal và xóa Alarm trên Dashboard.
*   **Advanced Overrides (Bypass & Override):**
    *   *Bypass Sensor:* Popup tích chọn bỏ qua cảm biến SafeGuard, Limit Switch LS-T3, overload Lift Motor của từng block.
    *   *Cưỡng bức Admin (Admin Override):* Bật cờ override, hiện banner cảnh báo nhấp nháy màu vàng, cho phép Gọi/Trả xe/Start/Pause ngay cả khi block bị lỗi hoặc bị Disable bảo trì.
*   **Giả lập Lỗi nhanh:** Nút zap cho phép đưa block đang chọn về trạng thái lỗi `Error` để test quy trình checklist.
*   **Gửi lệnh PLC trực tiếp:** Ô nhập raw, Modal bảo mật và chuỗi LED 4 đèn chỉ báo (`Sent -> Accepted -> Running -> Done/Failed`). Hiện mã lỗi từ chối `ERR_PLC_REJECT_0x0A` khi gửi lệnh `CMD_INVALID`.
*   **Gọi xe / Trả xe thủ công:** Hoạt động ở chế độ Manual (hoặc Cưỡng bức Admin), tự động tìm khay trống và lấp đầy xe, tăng chu kỳ hoạt động.

---

## 10. Tab: Remote Support ([Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml))
*Phân quyền ma trận người dùng, giám sát kết nối và xem lịch sử Audit log.*

*   **Ma trận phân quyền hệ thống:**
    *   *Administrator:* Toàn quyền, Forced Mode, cấu hình hệ thống.
    *   *Supervisor:* Reset/Ack alarm, xuất báo cáo, xác nhận điều khiển nguy hiểm.
    *   *Maintenance:* Chế độ Maintenance, kiểm tra cảm biến/VFD, xử lý Work Order.
    *   *Operator:* Dashboard giám sát, đọc cảnh báo, điều hướng, quản lý quẹt thẻ.
    *   *Remote Support:* Chỉ đọc (Read-only), không được điều khiển, xem log chẩn đoán, giới hạn phiên kết nối 1 giờ.
*   **Giám sát tài khoản kết nối:** Hiển thị danh sách các tài khoản đang trực tuyến thời gian thực (ví dụ: `admin_root` đang online, `tech_maintenance` đang kết nối remote support).
*   **Nhật ký hoạt động SCADA (Audit Logs):** Bảng hiển thị lịch sử thao tác của các tài khoản (Thời gian, User, Hành động thực hiện, IP kết nối, Trạng thái thành công/thất bại).
