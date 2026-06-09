# Thiết kế giao diện WebApp SCADA cho dự án LUMI

Hệ thống đỗ xe tự động dạng Puzzle Parking System.

## Mục tiêu giao diện

- Tạo một hệ thống SCADA trung tâm dạng webapp để giám sát, điều hành, cảnh báo, báo cáo, hỗ trợ bảo trì và điều hướng xe trong bãi đỗ tự động.
- Giao diện dùng cho phòng điều khiển trung tâm, màn hình lớn 55 inch và máy tính vận hành.

## Ngôn ngữ giao diện

- Tiếng Việt là chính
- Có khả năng chuyển đổi Tiếng Việt / English

## Phong cách thiết kế

- Industrial dashboard
- Clean, modern, high-contrast
- Phù hợp cho vận hành 24/7
- Ưu tiên thông tin trạng thái, cảnh báo và hành động nhanh
- Không dùng phong cách quá trang trí
- Giao diện rõ ràng, dễ đọc trên màn hình lớn

## Kích thước frame Figma

- Desktop WebApp: 1920 x 1080
- Responsive cho 1366 x 768
- Có thể hiển thị tốt trên màn hình trung tâm 55 inch

## Cấu trúc layout chính

1. Header cố định phía trên
   - Logo TotalParking
   - Tên dự án: LUMI Automated Parking SCADA
   - Trạng thái hệ thống: Normal / Warning / Fault / Maintenance
   - Thời gian realtime
   - Người dùng đăng nhập
   - Nút chuyển ngôn ngữ
   - Nút đăng xuất

2. Sidebar bên trái
   Menu gồm:
   - Dashboard tổng quan
   - Mặt bằng hệ thống
   - Zone / Block Monitoring
   - Điều hướng xe
   - Alarm & Event
   - Bảo trì
   - Báo cáo
   - Quản lý thẻ
   - Cài đặt hệ thống
   - Remote Support

3. Khu vực nội dung chính
   - Hiển thị layout tổng thể bãi đỗ, các tầng hầm, zone, block, pallet, trạng thái thiết bị, mật độ khai thác và cảnh báo.

4. Bottom / Right panel
   - Hiển thị alarm realtime, event log, command log và trạng thái kết nối PLC / HMI / AI-VDS / LED / PGS.

## Các module màn hình cần thiết

### A. Dashboard tổng quan

- Hiển thị tổng số block
- Tổng số pallet
- Số pallet có xe
- Số pallet trống
- Số chỗ trống theo loại xe: SUV, SEDAN, 4800 mm, 5050 mm
- Mật độ từng zone
- Số alarm active
- Trạng thái kết nối PLC Block, HMI Block, AI-VDS, LED/PGS, SCADA Server
- Biểu đồ mật độ theo thời gian
- Danh sách block lỗi hoặc đầy

### B. Mặt bằng hệ thống

- Hiển thị bản đồ tổng thể bãi đỗ LUMI
- Phân chia theo zone và block
- Mỗi block là một card/marker trên bản đồ
- Màu trạng thái:
  - Green: bình thường / còn chỗ
  - Blue: đang vận hành
  - Yellow: cảnh báo
  - Red: lỗi
  - Gray: offline / bảo trì
- Khi click vào block, mở panel chi tiết block

### C. Chi tiết một block đỗ xe

Hiển thị:

- Tên block
- Zone
- Vị trí trong bãi
- Chế độ vận hành: Thẻ từ / Tự động / Bằng tay / Cưỡng bức
- Sức chứa
- Số pallet có xe
- Số pallet trống
- Số xe SUV / SEDAN
- Trạng thái từng pallet: có xe, trống, khóa, lỗi
- Thời gian xe vào/ra từng pallet
- Tần suất hoạt động
- Số chu kỳ vận hành
- Runtime motor
- Trạng thái lỗi hiện tại
- Nút xem lịch sử / bảo trì / alarm detail

### D. Điều hướng xe

- Kết nối với trạm phân loại xe AI-VDS
- Hiển thị dữ liệu phân loại xe:
  - Loại xe: SUV / SEDAN / quá chiều dài / quá chiều cao / xe đỗ thường
  - Loại thẻ cần cấp
  - Zone/block đề xuất
  - Route dẫn đường trong hầm
- Hiển thị mật độ từng zone để tránh ùn tắc
- Trạng thái gửi dữ liệu đến AI-VDS / LED / PGS
- Có trạng thái fallback khi mất kết nối AI-VDS hoặc SCADA

### E. Alarm & Event

Thiết kế bảng alarm realtime gồm các cột:

- Time
- Severity
- Zone
- Block
- Device
- Alarm Code
- Alarm Message
- Suggested Action
- Status
- Ack User
- Recovery Time

#### Nhóm alarm

- Nguồn/điện: mất phase, cúp điện, lỗi nguồn 3 pha 380VAC
- HMI/control panel: cửa tủ điều khiển mở, E-stop đang nhấn
- Cảm biến kích thước: quá chiều dài, quá chiều cao
- An toàn cơ khí: móc chống rơi, công tắc hành trình, chùng xích/cáp
- Xâm nhập: người hoặc vật thể trong vùng nguy hiểm
- Bảo trì: thiết bị vượt ngưỡng số lần hoạt động hoặc runtime

#### Alarm Detail

- Mô tả lỗi
- Nguyên nhân khả dĩ
- Trạng thái cảm biến liên quan
- Hướng xử lý
- Điều kiện reset
- Lịch sử lỗi
- Nút Acknowledge
- Nút Export report

### F. Bảo trì

Thiết kế màn hình maintenance dashboard:

- Tần suất hoạt động của mỗi block
- Tần suất hoạt động từng pallet
- Tần suất hoạt động từng motor
- Runtime motor nâng/hạ
- Runtime motor trượt ngang
- Số chu kỳ pallet
- Thiết bị đến hạn bảo trì
- Cảnh báo bảo trì theo usage-based maintenance
- Danh sách work order đề xuất
- Bộ lọc theo zone, block, thiết bị, mức độ ưu tiên

### G. Báo cáo

Các loại báo cáo:

- Báo cáo mật độ/lưu lượng
- Báo cáo lỗi
- Báo cáo bảo trì
- Báo cáo thẻ/xe
- Báo cáo lịch sử gửi/lấy xe

Chức năng:

- Bộ lọc thời gian
- Bộ lọc zone/block
- Export Excel
- Export PDF
- Biểu đồ Pareto lỗi
- Biểu đồ mật độ theo ngày/tuần/tháng

### H. Quản lý thẻ

Thiết kế màn hình quản lý thẻ:

- Cài đặt thẻ mới cho từng loại xe
- Xóa thẻ theo ID
- Xóa thẻ bằng cách quẹt thẻ
- Xóa thẻ theo danh sách
- Gán thẻ theo xe
- Gán pallet cố định
- Gán zone/block được phép
- Lịch sử sử dụng thẻ

### I. Remote Support và phân quyền

- Vai trò người dùng:
  - Operator
  - Maintenance
  - Supervisor
  - Admin
  - Remote Support
- Mọi lệnh quan trọng phải có audit log:
  - User
  - Thời gian
  - Block
  - Lệnh
  - Kết quả
  - Lý do thao tác
- Các thao tác nguy hiểm như Reset Alarm, Manual Mode, Forced Mode cần confirm modal 2 bước

## Component cần tạo trong Figma

- Status badge
- Zone card
- Block card
- Pallet cell
- Alarm row
- Alarm detail modal
- Device connection indicator
- KPI card
- Maintenance card
- Report table
- Filter bar
- Route guidance panel
- Confirm action modal
- User role badge
- Language switcher

## Design tokens

### Colors

- Running / Normal: `#16A34A`
- Idle / Standby: `#2563EB`
- Warning: `#F59E0B`
- Fault / Critical: `#DC2626`
- Maintenance: `#7C3AED`
- Offline / Disabled: `#6B7280`
- Background dark: `#0F172A`
- Surface: `#1E293B`
- Text primary: `#F8FAFC`
- Text secondary: `#CBD5E1`

### Typography

- Font: Inter, Roboto hoặc IBM Plex Sans
- Header: 24–32 px
- KPI number: 32–48 px
- Body: 14–16 px
- Table: 13–14 px
- Alarm text: tối thiểu 15 px

## UX rules

- Trạng thái quan trọng phải có cả màu, icon và text
- Alarm critical phải nổi bật trong dưới 2 giây
- Không cho phép SCADA bypass liên động an toàn PLC
- Forced mode phải có mật khẩu, phân quyền và ghi log
- Khi mất kết nối SCADA, PLC Block vẫn đảm bảo an toàn cục bộ
- Khi mất kết nối AI-VDS, giao diện phải hiển thị fallback mode và timestamp dữ liệu cuối cùng

## Prototype flow cần tạo

1. Dashboard → click Zone → xem danh sách Block
2. Click Block → mở Block Detail
3. Click Alarm → mở Alarm Detail
4. Click Acknowledge → confirm modal → cập nhật trạng thái
5. Click Maintenance → xem thiết bị đến hạn bảo trì
6. Click Route Guidance → xem tuyến đề xuất đến block còn trống
7. Click Manage Card → thêm/xóa/gán thẻ
8. Click Forced Mode → yêu cầu quyền admin + confirm 2 bước
