# Task R4-01: Kiểm tra tốc độ tải trang tĩnh SCADA dưới 1.5 giây

**Requirement:** R4 — Hiệu năng Tải trang
**Status:** pending
**Priority:** P3
**Estimated Effort:** 1h
**Dependencies:** task-R1-02-mvc-static-files.md, task-R2-03-mvc-controller-routing.md
**Spec:** specs/scada-web-integration/

## Objective
Xác minh tốc độ phản hồi và thời gian tải trang ban đầu của giao diện SCADA khi được phục vụ qua IIS của C# backend đạt mức tối ưu (< 1.5s).

## Constraints
- **MUST**: Đo lường bằng các công cụ đo lường chuẩn của trình duyệt (Chrome DevTools Performance / Network).
- **MUST**: Đảm bảo điều kiện đo đạc không bị ảnh hưởng bởi tốc độ mạng ngoài (thử nghiệm trên localhost).

## Implementation Steps

- [ ] 1. Đo lường và đánh giá thời gian tải trang
  - [ ] 1.1 Kiểm tra kích thước tài nguyên và nén
    - Mục đích: Đảm bảo dung lượng file CSS/JS đã biên dịch của Vite đủ nhỏ để tải nhanh.
    - Chi tiết: Xem kích thước thư mục [SCADALayout/assets/](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/SCADALayout/assets) sau khi build. Đảm bảo tổng dung lượng file JS chính không vượt quá 1.5MB (chưa nén).
    - _Requirements: 4.1_
  - [ ] 1.2 Đo lường thời gian DOMContentLoaded và Load
    - Mục đích: Đo đạc thực tế thời gian render lần đầu để chứng minh đáp ứng yêu cầu dưới 1.5 giây.
    - Chi tiết: Mở Chrome DevTools Network Tab khi chạy ứng dụng trên localhost. Tải lại trang (Disable Cache = false để mô phỏng lần truy cập thứ hai). Ghi lại thời gian `DOMContentLoaded` và `Load` của trang `/Home/Index`.
    - _Requirements: 4.1_

- [ ] 2. Test coverage cho R4-01
  - [ ] 2.1 Ghi nhận báo cáo hiệu năng
    - Kiểm thử: Đọc thông số thời gian tải trang từ Chrome DevTools Performance panel và lưu vào báo cáo kiểm thử.
    - _Requirements: 4.1_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/SCADALayout/index.html` | Inspect | File HTML tĩnh làm cột mốc đo đạc |

## Completion Criteria

- [ ] Thời gian tải trang hoàn chỉnh (DOMContentLoaded) trên localhost nhỏ hơn 1.5 giây.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A
  - Expected proof: N/A
- [ ] Artifact / runtime verification
  - Inspect: Tab Network trong Chrome Developer Tools.
  - Expect: Thời gian load tài nguyên của `/Home/Index` trên localhost có chỉ số `Load` dưới 1.5s.
- [ ] Contract / negative-path verification
  - Check: Thử tải trang khi không có cache.
  - Expect: Trang vẫn tải xong và hiển thị đầy đủ UI dưới 1.5 giây trên kết nối localhost.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Dung lượng asset quá lớn làm tăng thời gian load | Low | Sử dụng tính năng tối ưu hóa mặc định của Vite (minify JS/CSS bằng esbuild) để giữ file dung lượng cực nhỏ. |
