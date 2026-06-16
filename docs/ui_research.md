# Khảo sát & Phân tích Chi tiết Giao diện Hệ thống LUMI TotalParking SCADA (ASP.NET MVC)

Tài liệu này tổng hợp chi tiết chức năng và các thành phần hiển thị trên giao diện người dùng của hệ thống **LUMI Automated Parking SCADA v2.4 (Puzzle Parking System)** dựa trên mã nguồn ASP.NET MVC trong thư mục [Views/Home](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home).

---

## 1. Cấu trúc Giao diện Chung (Global Layout)

Toàn bộ các màn hình chính sử dụng Layout dùng chung là [\_ScadaLayout.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Shared/_ScadaLayout.cshtml) với thiết kế tối (Dark Mode) hiện đại, phối màu chủ đạo dựa trên bảng màu Slate (`#0F172A`, `#1E293B`, `#475569`, `#F8FAFC`) và các biểu tượng động từ thư viện Lucide Icons.

*   **Thanh Tiêu đề (Header Bar):**
    *   **Logo & Phiên bản:** Logo `TP` (TotalParking SCADA v2.4) kèm dòng chữ mô tả hệ thống *"LUMI Automated Parking SCADA – Hệ thống đỗ xe tự động – Puzzle Parking System"*.
    *   **Trạng thái Cảnh báo:** Đèn cảnh báo lỗi hệ thống động và bộ đếm cảnh báo nhấp nháy (*"4 Alarm" / "Lỗi"*).
    *   **Đồng hồ thời gian thực:** Hiển thị giờ phút giây và ngày tháng hiện tại (định dạng `HH:mm:ss dd/MM/yyyy`).
    *   **Thông tin tài khoản đăng nhập:** Hiển thị tên người dùng hiện tại (ví dụ: `supervisor01`).
    *   **Nút chuyển đổi ngôn ngữ:** Hỗ trợ lựa chọn đa ngôn ngữ (mặc định hiển thị `EN`).
    *   **Nút Đăng xuất:** Nút thoát nhanh khỏi phiên làm việc.
*   **Thanh Điều hướng bên trái (Sidebar Navigation):**
    *   Bao gồm 10 liên kết tương ứng với các màn hình chức năng của hệ thống. Mỗi mục hiển thị kèm biểu tượng (Icon) trực quan và trạng thái kích hoạt (Active) màu xanh dương nổi bật:
        1.  **Dashboard Tổng quan** (liên kết đến [Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml))
        2.  **Mặt bằng hệ thống** (liên kết đến [FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml))
        3.  **Zone / Block** (liên kết đến [Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml))
        4.  **Điều hướng xe** (liên kết đến [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml))
        5.  **Alarm & Event** (liên kết đến [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml))
        6.  **Bảo trì** (liên kết đến [Maintenance.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Maintenance.cshtml))
        7.  **Báo cáo** (liên kết đến [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml))
        8.  **Quản lý thẻ** (liên kết đến [Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml))
        9.  **Cài đặt hệ thống** (liên kết đến [Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml))
        10. **Remote Support** (liên kết đến [Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml))

---

## 2. Chi tiết Chức năng & Thành phần Hiển thị từng Màn hình

### 2.1. Dashboard Tổng quan ([Index.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Index.cshtml))
Màn hình trung tâm hiển thị trạng thái vận hành tức thời và các chỉ số hiệu suất chính (KPIs) của bãi xe.

*   **Hộp chỉ số nhanh (KPI Cards):**
    *   *Tổng Block:* `9` block đỗ xe.
    *   *Tổng Pallet (Sức chứa):* `184` vị trí đỗ.
    *   *Số pallet có xe:* `91` xe (đạt tỷ lệ lấp đầy `49%`).
    *   *Số pallet trống:* `69` vị trí.
    *   *Phân loại loại xe đỗ:* `37` xe SUV và `54` xe Sedan.
*   **Thanh Cảnh báo Khẩn cấp:** Dải thông báo nổi bật màu đỏ nhấp nháy hiển thị số lượng cảnh báo đang hoạt động và nội dung cụ thể của cảnh báo nghiêm trọng nhất (Ví dụ: *"3 cảnh báo đang hoạt động: Chùng xích nâng pallet P04 - Block A04"*).
*   **Biểu đồ Mật độ Khai thác:** Biểu đồ vùng (Area Chart) trực quan hóa xu hướng mật độ xe đỗ theo các khung giờ (từ `06:00` đến `12:00`) phân tách theo 3 phân khu: `Zone A` (Xanh lá), `Zone B` (Xanh dương), `Zone C` (Tím).
*   **Thông tin Mật độ và Kết nối Phụ:**
    *   *Mật độ theo Zone:* Hiển thị thanh tiến trình trực quan (%) của từng Zone (Zone A: `78%`, Zone B: `55%`, Zone C: `32%`).
    *   *Trạng thái kết nối truyền thông:* Giám sát trạng thái hoạt động (Kết nối/Mất kết nối) của các thành phần hạ tầng: `PLC Block`, `HMI Block`, `AI-VDS` (Camera thông minh), `LED/PGS` (Bảng hiển thị và Chỉ dẫn đỗ xe), `SCADA Server`.
*   **Trạng thái Hoạt động các Block:** Lưới hiển thị 9 block đỗ xe (A01 đến C02) với các trạng thái được mã hóa màu sắc:
    *   *Bình thường (Xanh lá):* Hoạt động ổn định.
    *   *Đang chạy (Xanh dương):* Đang thực hiện chu trình di chuyển/nâng hạ pallet.
    *   *Cảnh báo (Vàng):* Có lỗi nhẹ hoặc sắp đến hạn bảo trì.
    *   *Lỗi (Đỏ):* Thiết bị dừng hoạt động do sự cố.
    *   *Bảo trì (Tím):* Đang khóa để bảo dưỡng.
    *   *Offline (Xám):* Mất kết nối điều khiển.
    *   *Số lượng xe đỗ:* Tỷ lệ và số lượng vị trí đang sử dụng của từng block (Ví dụ: `16/20`, `18/20`, v.v.).
*   **Danh sách Cảnh báo gần nhất:** Bảng log hiển thị 5 cảnh báo mới nhất với thông tin chi tiết: Thời gian, Mức độ (Critical, Major, Minor), Zone, Block, Nội dung sự cố, Trạng thái xử lý (Đang lỗi, Đã biết).

---

### 2.2. Mặt bằng Hệ thống ([FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml))
Bản đồ trực quan hóa vị trí vật lý của các block đỗ xe trong bãi xe dạng nhiều tầng hầm.

*   **Chỉ dẫn Quy hoạch:** Chỉ hướng lối vào/lối ra chính và các tầng hầm (`B1`, `B2`, `B3`).
*   **Sơ đồ khối 2D dạng lưới:** Trực quan hóa vị trí tương quan của các block (từ `B01` đến `B12`) tương ứng với:
    *   *Zone A (Hầm B1):* Block B01 đến B04.
    *   *Zone B (Hầm B2):* Block B05 đến B08.
    *   *Zone C (Hầm B3):* Block B09 đến B12.
    *   Mỗi ô block sử dụng mã màu hiển thị trạng thái hoạt động thực tế của block đó (Đang chạy, Bình thường, Cảnh báo, Lỗi, Bảo trì, Offline).
*   **Bảng Giám sát Chi tiết theo Từng Zone:**
    *   Hiển thị thông số tổng quát của Zone: Số lượng pallet sử dụng/tổng pallet và mật độ (%) của Zone đó.
    *   Mỗi Block trong Zone được hiển thị dưới dạng card thông tin chứa: Mã block, tên block, thanh tiến trình tỷ lệ lấp đầy, số pallet đang sử dụng trên tổng sức chứa và tỷ lệ phần trăm lấp đầy cụ thể.
    *   *Tương tác:* Cho phép người dùng click vào từng block để chuyển hướng hoặc mở xem chi tiết.

---

### 2.3. Sơ đồ Giám sát Phân khu ([Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml)) & Chi tiết Phân khu ([ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml))
Hỗ trợ quản trị viên đi sâu từ bản đồ quy hoạch tổng thể đến chi tiết từng vị trí pallet đỗ xe.

*   **Bản đồ Phân khu Tương tác ([Zones.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Zones.cshtml)):**
    *   Sử dụng hình ảnh kiến trúc thực tế của bãi xe ([Images/zones_map.jpeg](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Images/zones_map.jpeg)).
    *   Đè lớp phủ SVG tương tác (polygons) lên các vùng phân khu: `Zone 1` (Vàng), `Zone 2` (Hồng/Magenta), `Zone 3` (Cam), `Zone 4` (Tím), `Zone 5` (Xanh cyan), `Zone 6` (Xanh lá).
    *   *Hiệu ứng:* Khi di chuột qua khu vực nào, khu vực đó sẽ nổi bật với viền màu sắc tương ứng và hiệu ứng đổ bóng. Khi click sẽ dẫn vào màn hình giám sát chi tiết của Zone đó.
*   **Giám sát Chi tiết Phân khu ([ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml)):**
    *   *Tiêu đề động:* Thay đổi theo ID phân khu (Zone A đến Zone F), hiển thị vị trí tầng (Hầm B1, Hầm B2, Tầng trệt, Tầng 1, Tầng 2, Tầng hầm B3) và liên kết để quay lại bản đồ.
    *   *Tổng hợp thông số Zone:* Hiển thị số lượng block, số lượng pallet đã sử dụng/tổng pallet, tỷ lệ lấp đầy kèm thanh tiến trình.
    *   *Lưới thông tin 4 Block:* Mỗi block hiển thị chi tiết: Trạng thái (Đang chạy, Bình thường, Cảnh báo, Lỗi), Sức chứa, số xe đỗ, tỷ lệ SUV/Sedan, Chế độ vận hành (Tự động/Bằng tay), thanh tiến trình % đỗ, và đèn báo trạng thái PLC/HMI.
*   **Modal Chi tiết Block (Popup):** Xuất hiện khi click vào một Block bất kỳ:
    *   *Thông số kỹ thuật nâng cao:* Hiển thị Zone, Chế độ, Sức chứa, Số xe đang đỗ, SUV, Sedan, **Tổng số chu kỳ chạy của cơ cấu cơ khí** (Cycles) và **Tổng thời gian hoạt động của Motor** (Runtime).
    *   *Sơ đồ Pallet Chi tiết (Pallets Grid):* Mô phỏng giao diện các khay chứa xe (Pallet P01, P02...) xếp theo dạng Puzzle với 5 trạng thái được chuẩn hóa:
        *   **Có xe (Xanh lá):** Hiển thị icon ô tô, mã pallet, loại xe (SUV/Sedan), biển số xe thực tế (ví dụ: `51G-12345`) và mốc thời gian xe vào bãi.
        *   **Pallet trống (Màu trắng):** Hiển thị ký hiệu chữ `P` màu xám.
        *   **Lỗi (Đỏ):** Hiển thị icon cảnh báo nguy hiểm (Ví dụ: sự cố cảm biến hoặc cơ cấu nâng hạ).
        *   **Khóa/Không khả dụng (Tím):** Hiển thị biểu tượng ổ khóa (Ví dụ: vị trí dự phòng dịch chuyển hoặc pallet bị vô hiệu hóa).
        *   **Trạng thái đặc biệt (Xanh mạ / Xanh cyan):** Dùng cho các phân khu đặc thù như Zone C.
    *   *Các nút chức năng vận hành trực tiếp (Footer Modal):*
        *   **Lịch sử:** Xem nhật ký xe ra vào và vận hành của riêng block này.
        *   **Bảo trì:** Chuyển nhanh block sang chế độ bảo dưỡng.
        *   **Chi tiết Alarm:** Xem danh sách các lỗi đang xảy ra tại block.
        *   **Cưỡng bức (Admin):** Lệnh ghi đè hệ thống dành cho quản trị viên tối cao (Forced Mode).

---

### 2.4. Điều hướng Xe – AI-VDS ([Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml))
Giao diện quản lý luồng xe vào bãi xe kết hợp xử lý dữ liệu từ hệ thống camera nhận diện kích thước và biển số thông minh (AI-VDS).

*   **Cảnh báo Trạng thái Kết nối:**
    *   Đèn báo động khẩn cấp hiển thị trạng thái kết nối của hệ thống AI-VDS.
    *   Bảng thông báo hệ thống chuyển sang *"Chế độ dự phòng (Fallback Mode)"* khi camera mất kết nối (gặp sự cố từ lúc `08:15:44`), yêu cầu người vận hành phân loại xe bằng tay.
*   **Giám sát Sức chứa Phân khu:** Thống kê số chỗ trống và số chỗ dành cho xe SUV còn lại của từng Zone (Zone A, B, C) để hỗ trợ điều hướng luồng xe vào vị trí phù hợp.
*   **Phân loại Xe thời gian thực (Real-time Inbound List):** Danh sách các xe đang đi qua cổng kiểm soát của bãi xe:
    *   Mỗi hàng hiển thị thông tin: Loại xe nhận diện (SUV, Sedan, Quá khổ - Oversized), Biển số xe, Kích thước thực tế đo được (Chiều cao · Chiều dài).
    *   *Đề xuất tự động từ SCADA:* Đề xuất block đỗ tối ưu nhất và thẻ loại xe tương ứng (Ví dụ: Đề xuất đỗ `Block A-02`, cấp thẻ `SUV-Card`).
    *   *Nhật ký thời gian:* Thời điểm xe đi qua cổng.
    *   *Trạng thái điều phối:* Đã phân loại, Đang dẫn đường (nhấp nháy xanh), hoặc Từ chối (đối với xe quá khổ - Oversized, kèm cảnh báo từ chối vào bãi).
*   **Trạng thái Truyền tín hiệu Chỉ dẫn (LED/PGS Status):**
    *   Giám sát việc đồng bộ dữ liệu chỉ dẫn từ SCADA tới các hệ thống ngoại vi: Máy chủ AI-VDS, Bảng LED hiển thị thông tin ngoài cổng, và Hệ thống chỉ dẫn đỗ xe trong nhà (PGS). Hiển thị chi tiết thời gian cập nhật gói tin cuối cùng.

---

### 2.5. Quản lý Cảnh báo – Alarm & Event ([Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml))
Màn hình kiểm soát, truy vết và xác nhận các cảnh báo lỗi thiết bị trong toàn bộ hệ thống đỗ xe.

*   **Chỉ số Tổng hợp Alarm:**
    *   *Alarm Active:* Số lượng cảnh báo chưa được khắc phục (`4` lỗi).
    *   *Nghiêm trọng:* Số lỗi ở mức nguy hiểm cần xử lý ngay (`2` lỗi).
    *   *Đã xác nhận:* Số lỗi đã được kỹ sư vận hành ghi nhận nhưng chưa sửa xong (`2` lỗi).
*   **Bộ lọc và Tìm kiếm:**
    *   Ô tìm kiếm nhanh mã lỗi hoặc mô tả lỗi.
    *   Bộ lọc phân loại theo Trạng thái (Tất cả, Đang hoạt động, Đã xác nhận, Đã giải quyết).
    *   Bộ lọc phân loại theo Mức độ nghiêm trọng (Tất cả, Nghiêm trọng, Cao, Trung bình, Thấp).
    *   Nút xuất dữ liệu nhanh ra file Excel (*"Xuất Excel"*).
*   **Bảng Nhật ký Cảnh báo chi tiết:**
    *   *Thông tin hiển thị:* Thời gian xuất hiện, Mức độ (Nghiêm trọng, Cao, Trung bình, Thấp), Phân khu, Block xảy ra sự cố, Thiết bị gặp lỗi (Motor nâng, Cảm biến, Safety Relay...), Mã lỗi nội bộ (E-xxxx/W-xxxx), Mô tả lỗi chi tiết, Trạng thái hiện tại, Người dùng đã xác nhận (ACK).
    *   *Ví dụ mã lỗi điển hình:*
        *   `E-0401` (Motor-Lift-01): Lỗi motor nâng hạ – quá tải dòng điện.
        *   `E-0408` (Safety-Relay): E-Stop đang được nhấn – Block A-04 dừng khẩn cấp.
        *   `W-0302` (HMI-Panel): Cửa tủ điều khiển mở – Block A-03.
        *   `E-0405` (Sensor-LS-03): Công tắc hành trình dưới không về vị trí – Pallet P05.
        *   `W-0701` (AI-VDS): AI-VDS mất kết nối.
    *   *Hành động:* Nút *"Xem"* hỗ trợ mở popup chuẩn đoán vị trí lỗi chi tiết trên mặt bằng.

---

### 2.6. Bảo trì Hệ thống ([Maintenance.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Maintenance.cshtml))
Công cụ quản lý vòng đời thiết bị, theo dõi độ hao mòn cơ khí và lập kế hoạch bảo dưỡng định kỳ.

*   **Thống kê Kế hoạch Bảo trì:**
    *   Hiển thị số lượng thiết bị cần bảo dưỡng phân cấp theo mức độ khẩn cấp (1 Khẩn cấp, 2 Ưu tiên cao).
    *   Theo dõi tổng số lệnh bảo trì (Work Orders) được tạo trong 30 ngày qua và số lượng block đang tạm dừng để bảo dưỡng.
    *   Nút *"Xuất Work Order"* để in phiếu công tác cho kỹ thuật viên.
*   **Giám sát Tần suất Hoạt động & Hao mòn theo Block:**
    *   Danh sách 12 block hiển thị số chu kỳ đã chạy (Cycles) và tổng số giờ chạy thực tế (Runtime).
    *   *Thanh đo hao mòn (%) trực quan:* Cho biết tỷ lệ phần trăm tuổi thọ đã sử dụng của block đó (Ví dụ: Block B07 đã chạy 8,900 chu kỳ / 3200h đạt mức hao mòn `89%` - hiển thị cảnh báo màu cam).
*   **Danh sách Phiếu Bảo trì Đề xuất (Proposed Work Orders):**
    *   Hệ thống tự động tính toán và đưa ra danh sách các bộ phận cơ khí/điện cần bảo dưỡng dựa trên giới hạn an toàn thiết kế.
    *   *Các hạng mục đề xuất điển hình:*
        *   Motor Nâng Hạ (Block A-04): Đã chạy 5,640/6,000 chu kỳ (đạt `94%` giới hạn) -> Mức độ: *Khẩn cấp*.
        *   Motor Trượt Ngang (Block B-01): Đã chạy 2,100/2,500 chu kỳ -> Mức độ: *Cao*.
        *   Xích nâng (Block A-03): Đạt `82%` tuổi thọ -> Mức độ: *Cao*.
        *   Dầu hộp số (Block B-03): Đạt `89%` thời gian vận hành -> Mức độ: *Trung bình*.
        *   Cảm biến kích thước (Block A-02): Đạt `58%` tuổi thọ -> Mức độ: *Thấp*.

---

### 2.7. Báo cáo Hệ thống ([Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml))
Công cụ kết xuất dữ liệu thống kê phục vụ công tác quản lý vận hành bãi xe.

*   **Menu Phân loại Báo cáo:** Lựa chọn nhanh giữa các định dạng báo cáo:
    *   Báo cáo mật độ / lưu lượng xe.
    *   Báo cáo lỗi và sự cố thiết bị.
    *   Báo cáo lịch sử bảo trì.
    *   Báo cáo quản lý thẻ và loại xe.
    *   Lịch sử chi tiết các lượt gửi/lấy xe.
*   **Bộ lọc Báo cáo:** Chọn khoảng thời gian (Từ ngày - Đến ngày), chọn phân khu cụ thể (Tất cả, Zone A, B, C) và nút *"Tạo báo cáo"*. Hỗ trợ nút xuất dữ liệu ra file **Excel** hoặc **PDF**.
*   **Trực quan hóa Dữ liệu (Charts Grid):**
    *   *Biểu đồ cột (Bar Chart):* Thống kê tỷ lệ mật độ đỗ xe trung bình (%) theo các ngày trong tuần (Thứ 2 đến Chủ Nhật) phân tách chi tiết cho từng phân khu (Zone A, B, C).
    *   *Biểu đồ tròn (Pie Chart):* Thể hiện cơ cấu các loại xe đỗ trong bãi (SUV: `35%`, Sedan: `42%`, Standard: `17%`, Oversized: `6%`).
*   **Bảng Phân tích Pareto Lỗi (Error Pareto Table):** Thống kê các nhóm lỗi phổ biến nhất để tìm nguyên nhân gốc rễ:
    *   *Motor quá tải:* 12 lần (`30%` tổng lỗi).
    *   *Cảm biến hành trình:* 9 lần (`23%`).
    *   *Mất kết nối truyền thông:* 7 lần (`18%`).
    *   *Dừng khẩn cấp (E-Stop):* 5 lần (`13%`).
    *   *Nguồn điện:* 4 lần (`10%`).
    *   *Các lỗi khác:* 3 lần (`8%`).

---

### 2.8. Quản lý Thẻ xe ([Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml))
Hệ thống quản lý thông tin khách hàng đỗ xe và định danh thẻ từ.

*   **KPIs Thẻ xe:** Tổng số thẻ đang lưu hành (`5` thẻ), số thẻ đang hoạt động (`4` thẻ), số thẻ bị khóa/hết hạn (`1` thẻ). Nút *"Cài đặt thẻ mới"*.
*   **Thanh tìm kiếm:** Tìm nhanh theo Mã thẻ, Biển số hoặc tên Chủ xe.
*   **Bảng Danh sách Thẻ:**
    *   *Thông tin hiển thị:* Mã thẻ (Ví dụ: `TH-00123`), Biển số xe đăng ký, Tên chủ xe, Loại xe đăng ký (SUV/Sedan), Zone và Block hiện tại đang đỗ, Trạng thái (Hoạt động/Khóa), Thời gian sử dụng cuối cùng.
    *   *Hành động:* Nút xem Lịch sử di chuyển của thẻ và Nút xóa/khóa thẻ khỏi hệ thống.

---

### 2.9. Cài đặt Hệ thống ([Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml))
Cấu hình các tham số lõi của hệ thống phần cứng, phần mềm và giao diện.

*   **Kết nối PLC/HMI:** Địa chỉ IP của máy chủ SCADA, Cổng Modbus TCP (mặc định `502`), tần suất quét dữ liệu (Polling interval: `500 ms`) và thời gian chờ kết nối (Timeout: `3000 ms`).
*   **Cơ sở Dữ liệu (Database):** IP máy chủ DB Host, tên cơ sở dữ liệu (`lumi_scada`), thời gian lưu trữ nhật ký alarm (mặc định `90 ngày`), và chu kỳ tự động backup dữ liệu (mặc định mỗi `6 giờ`).
*   **Cảnh báo & Thông báo:** Thiết lập email và số điện thoại SMS để nhận thông báo tự động khi phát hiện lỗi mức độ *Critical*.
*   **Tùy chọn giao diện (UI Options):** Các nút gạt (Toggle Switches) để bật/tắt: Chế độ tối (Dark Mode), tự động xác nhận lỗi mức độ thấp (Auto Ack low alarm), bật âm thanh cảnh báo lỗi khẩn cấp, và hiển thị nhãn thời gian dữ liệu quét.
*   **Thông tin Hệ thống (System Info):** Phiếu chẩn đoán tài nguyên phần cứng máy chủ SCADA: Phiên bản phần mềm (`v2.4.1`), ngày lắp đặt hệ thống, thời gian hoạt động liên tục (Uptime), tỷ lệ sử dụng CPU, RAM và Dung lượng DB hiện tại (`12.4 GB`).

---

### 2.10. Remote Support & Phân quyền ([Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml))
Quản lý bảo mật, phân cấp vai trò người dùng và ghi nhật ký thao tác vận hành (Audit Log).

*   **Ma trận Phân quyền Người dùng:** Mô tả chi tiết quyền hạn của 5 nhóm tài khoản truy cập hệ thống:
    *   `Admin`: Có toàn quyền hệ thống, được quyền kích hoạt chế độ cưỡng bức (Forced Mode), quản lý người dùng và thay đổi cấu hình hệ thống.
    *   `Supervisor`: Được quyền Reset Alarm thiết bị, xem toàn bộ các module hiển thị, xuất báo cáo và phê duyệt các lệnh vận hành nguy hiểm.
    *   `Maintenance`: Quyền truy cập chế độ bảo trì, xem chi tiết tình trạng cơ khí của block và cập nhật phiếu công tác bảo dưỡng (Work Orders).
    *   `Operator`: Xem Dashboard tổng quan, xem danh sách alarm (chỉ đọc), thực hiện điều hướng xe và đăng ký thẻ xe (hạn chế).
    *   `Remote Support`: Chỉ xem trạng thái hệ thống từ xa qua Internet, xem Audit Log, không được phép điều khiển thiết bị, giới hạn thời gian phiên làm việc.
*   **Danh sách tài khoản trực tuyến (Online Users):** Giám sát thời gian thực các tài khoản đang kết nối vào hệ thống SCADA bao gồm: Tên tài khoản (Ví dụ: `supervisor01`), Vai trò, Thời điểm đăng nhập, Địa chỉ IP kết nối (Ví dụ: `192.168.1.10` hoặc IP WAN hỗ trợ từ xa `203.162.x.x`).
*   **Audit Log – Nhật ký Vận hành Hệ thống:** Bảng ghi lại mọi tác động điều khiển thiết bị quan trọng của người vận hành:
    *   *Nội dung ghi nhận:* Thời gian ra lệnh, Tên tài khoản, Vai trò, Vị trí thiết bị chịu tác động (Block), Lệnh thực hiện, Kết quả (OK/Lỗi), và Lý do vận hành.
    *   *Ví dụ lịch sử vận hành:*
        *   `supervisor01` thực hiện *"Reset Alarm E-0401"* tại `Block A-04` thành công. Lý do: *"Đã xử lý lỗi motor, reset VFD"*.
        *   `maintenance01` chuyển `Block B-03` sang *"Chế độ bảo trì"* thành công. Lý do: *"Bảo trì định kỳ motor trượt"*.
        *   `admin01` cố gắng thực hiện *"Forced Mode"* tại `Block A-04` thất bại do *"Mật khẩu không đúng"*.
