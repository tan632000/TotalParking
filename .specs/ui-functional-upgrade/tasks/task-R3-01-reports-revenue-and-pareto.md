# Task R3-01: Bổ sung báo cáo doanh thu từ xa, biểu đồ Pareto lỗi và phân cấp cảnh báo 3 loại

**Requirement:** R7, R10, R19, R20, R22 — Báo cáo và Quản lý Cảnh báo
**Status:** pending
**Priority:** Medium
**Estimated Effort:** 2.5h
**Dependencies:** None
**Spec:** .specs/ui-functional-upgrade/

## Objective
Cập nhật màn hình báo cáo [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml) và quản lý cảnh báo [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml) để bổ sung báo cáo doanh thu từ xa, Pareto lỗi và phân cấp cảnh báo 3 nhóm lỗi.

## Constraints
- **MUST**: Đảm bảo bảng biểu Pareto hiển thị đầy đủ thông số số lần, tỷ lệ và lũy kế phần trăm lỗi.

## Implementation Steps
- [ ] 1. Sửa đổi [Reports.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Reports.cshtml)
  - [ ] 1.1 Thêm mục lựa chọn "Báo cáo doanh thu" vào menu loại báo cáo bên trái. _Requirements: 22.1_
  - [ ] 1.2 Thiết kế mockup giao diện hiển thị biểu đồ Pareto lỗi (loại lỗi, số lần, tỷ lệ %, lũy kế %). _Requirements: 20.1_
- [ ] 2. Sửa đổi [Alarms.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Alarms.cshtml)
  - [ ] 2.1 Cập nhật cấu trúc bảng hiển thị phân loại rõ rệt 3 nhóm lỗi: Lỗi thao tác vận hành, Lỗi hệ thống/thiết bị, Thiết bị đến hạn bảo trì. _Requirements: 10.1_

## Related Files
| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/Reports.cshtml` | Modify | Thêm báo cáo doanh thu và biểu đồ Pareto |
| `TotalParking/Views/Home/Alarms.cshtml` | Modify | Phân cấp cảnh báo 3 loại lỗi |

## Completion Criteria
- [ ] Màn hình Reports xuất hiện tùy chọn báo cáo doanh thu và bảng biểu Pareto.
- [ ] Màn hình Alarms phân nhóm rõ ràng 3 nguồn phát sinh lỗi.

## Verification & Evidence
- [ ] Artifact / runtime verification
  - Inspect: Truy cập `/Home/Reports` và `/Home/Alarms`.
  - Expect: Thấy đầy đủ biểu đồ phân tích lỗi và cách phân loại lỗi vận hành/thiết bị/bảo trì.
