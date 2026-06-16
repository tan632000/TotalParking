# Task R1-01: Cập nhật FloorPlan hiển thị 4 Zone đỗ xe chính

**Requirement:** R1 — Bản đồ layout tổng mặt bằng hiển thị 4 Phân khu (Zone A, B, C, D)
**Status:** pending
**Priority:** High
**Estimated Effort:** 2h
**Dependencies:** None
**Spec:** .specs/ui-functional-upgrade/

## Objective
Nâng cấp giao diện [FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml) để hiển thị đầy đủ 4 Phân khu đỗ xe (Zone A, B, C, D) thay vì 3 phân khu như trước đây, tích hợp các block tương ứng (B01 đến B12).

## Constraints
- **MUST**: Đảm bảo các ô Block (B01-B12) phân bổ đều theo 4 Zone trên layout 2D.
- **MUST**: Định dạng màu sắc trạng thái block tự động cập nhật qua các lớp CSS đã cấu hình.

## Implementation Steps
- [ ] 1. Sửa đổi [FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml)
  - [ ] 1.1 Thêm phân vùng Zone D (Tầng 1) vào sơ đồ lưới 2D đầu trang và khu vực thống kê chi tiết bên dưới. _Requirements: 1.1_
  - [ ] 1.2 Phân bổ lại các mã block: Zone A (B01, B02, B03), Zone B (B04, B05, B06), Zone C (B07, B08, B09), Zone D (B10, B11, B12). _Requirements: 1.2_
  - [ ] 1.3 Cập nhật mốc liên kết của các nút bấm block để dẫn sang view [ZoneDetail.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/ZoneDetail.cshtml) với đúng tham số ID (1, 2, 3, 4). _Requirements: 1.3_

## Related Files
| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/FloorPlan.cshtml` | Modify | Nâng cấp sơ đồ và cấu trúc hiển thị 4 Zone |

## Completion Criteria
- [ ] Màn hình [FloorPlan.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/FloorPlan.cshtml) hiển thị trực quan 4 Phân khu đỗ xe (Zone A, B, C, D).
- [ ] Click vào bất kỳ block nào trong 4 zone đều điều hướng chính xác về trang chi tiết tương ứng.

## Verification & Evidence
- [ ] Artifact / runtime verification
  - Inspect: Truy cập đường dẫn `/Home/FloorPlan` trên trình duyệt.
  - Expect: Hiển thị 4 phân khu đỗ xe tương ứng với sơ đồ quy hoạch mới.
