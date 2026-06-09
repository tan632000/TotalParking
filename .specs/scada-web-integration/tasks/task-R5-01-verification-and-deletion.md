# Task R5-01: Kiểm tra vận hành độc lập và xóa thư mục React

**Requirement:** R5 — Tính Độc lập của Mã nguồn
**Status:** pending
**Priority:** P2
**Estimated Effort:** 1h
**Dependencies:** task-R1-02-mvc-static-files.md, task-R2-03-mvc-controller-routing.md, task-R3-01-localization-header.md
**Spec:** specs/scada-web-integration/

## Objective
Xác minh toàn bộ ứng dụng SCADA chạy ổn định trong ASP.NET MVC độc lập với mã nguồn React ban đầu, sau đó tiến hành xóa bỏ thư mục [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) theo mong muốn của người dùng.

## Constraints
- **MUST**: Đảm bảo tất cả các chức năng giao diện tĩnh (FloorPlan, Alarms, Maintenance...) vẫn hoạt động chính xác trước khi thực hiện xóa.
- **MUST**: Sao lưu tạm thời hoặc xác minh chắc chắn mã nguồn React đã được compile hoàn chỉnh trước khi xóa vĩnh viễn.

## Implementation Steps

- [ ] 1. Kiểm tra nghiệm thu tổng thể và xóa thư mục nguồn React
  - [ ] 1.1 Kiểm thử kiểm duyệt toàn bộ các màn hình trên MVC Server
    - Mục đích: Đảm bảo giao diện và định tuyến trên IIS chạy hoàn hảo, không có file asset nào bị thiếu.
    - Chi tiết: Khởi chạy dự án [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) trên IIS Express. Kiểm tra lần lượt các trang:
      - `/Home/Index` (Tổng quan)
      - `/Home/FloorPlan` (Mặt bằng)
      - `/Home/Alarms` (Cảnh báo)
      - `/Home/Maintenance` (Bảo trì)
      - Nhấp chọn chuyển đổi ngôn ngữ ở Header và đảm bảo trạng thái dịch hoạt động tốt.
    - _Requirements: 5.1_
  - [ ] 1.2 Thực hiện xóa thư mục nguồn React
    - Mục đích: Thực thi yêu cầu dọn dẹp mã nguồn React ban đầu của người dùng và giữ lại phiên bản biên dịch tĩnh.
    - Chi tiết: Tiến hành xóa thư mục [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) ra khỏi cấu trúc dự án.
    - _Requirements: 5.1_
  - [ ] 1.3 Xác minh vận hành độc lập sau khi xóa
    - Mục đích: Đảm bảo ứng dụng web C# vẫn tự chạy tốt không cần đến thư mục React.
    - Chi tiết: Khởi động lại IIS Express của dự án C# [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) sau khi xóa thư mục nguồn React. Truy cập lại các link SCADA để xác nhận hệ thống vẫn hoạt động bình thường.
    - _Requirements: 5.1_

- [ ] 2. Test coverage cho R5-01
  - [ ] 2.1 Kiểm thử liên kết tĩnh sau dọn dẹp
    - Kiểm thử: Xác minh không có lỗi tải file (HTTP 404) trong Console của trình duyệt sau khi xóa thư mục React.
    - _Requirements: 5.1_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParkingLayout/` | Delete | Xóa bỏ toàn bộ thư mục chứa mã nguồn React ban đầu |

## Completion Criteria

- [ ] Thư mục [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) đã bị xóa hoàn toàn khỏi ổ đĩa.
- [ ] Ứng dụng ASP.NET MVC [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) khởi chạy và hiển thị đầy đủ giao diện SCADA bình thường.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): Kiểm tra sự tồn tại của thư mục: `Test-Path c:\Users\HOME\source\repos\TotalParking\TotalParkingLayout`
  - Expected proof: Lệnh trả về `False` (thư mục không còn tồn tại).
- [ ] Artifact / runtime verification
  - Inspect: Truy cập `http://localhost:XXXX/Home/FloorPlan`
  - Expect: Trang tải thành công và hiển thị đầy đủ sơ đồ thiết kế.
- [ ] Contract / negative-path verification
  - Check: Mở F12 Console của trình duyệt khi duyệt các trang SCADA.
  - Expect: Không xuất hiện lỗi tải tài nguyên tĩnh (như 404 cho JS/CSS/ảnh).

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Xóa nhầm file khi chưa copy xong | Medium | Chạy kịch bản build và copy thành công trước, kiểm tra giao diện chạy tốt trên IIS mới thực hiện xóa thư mục nguồn React. |
