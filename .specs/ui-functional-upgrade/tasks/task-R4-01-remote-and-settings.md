# Task R4-01: Cập nhật phân quyền người dùng, Audit Log và đồng bộ thẻ từ

**Requirement:** R8, R9, R11, R21, R23, R24 — Phân quyền, Nhật ký Audit và cấu hình cài đặt
**Status:** pending
**Priority:** Medium
**Estimated Effort:** 2h
**Dependencies:** None
**Spec:** .specs/ui-functional-upgrade/

## Objective
Cập nhật màn hình quản trị [Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml), quản lý thẻ [Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml) và cài đặt [Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml) để đáp ứng phân quyền, audit log và thiết lập truyền thông.

## Constraints
- **MUST**: Lưu log vận hành đầy đủ thông tin: Tài khoản, Vai trò, Lệnh, Kết quả, Lý do.

## Implementation Steps
- [ ] 1. Sửa đổi [Remote.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Remote.cshtml)
  - [ ] 1.1 Hoàn thiện bảng "Phân quyền người dùng" hiển thị rõ rệt các vai trò và mức độ truy cập (Admin, Supervisor, Maintenance, Operator, Remote Support). _Requirements: 21.1_
  - [ ] 1.2 Cập nhật bảng Audit Log ghi chép chi tiết các hành động vận hành thử nghiệm từ xa. _Requirements: 23.1_
- [ ] 2. Sửa đổi [Cards.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Cards.cshtml)
  - [ ] 2.1 Bổ sung cột "Đồng bộ HMI" trong danh sách quản lý thẻ xe để hiển thị trạng thái đã đồng bộ/chờ đồng bộ. _Requirements: 11.1_
- [ ] 3. Sửa đổi [Settings.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Settings.cshtml)
  - [ ] 3.1 Bổ sung khu vực cấu hình Webhook và Swagger URL phục vụ tích hợp bên thứ ba. _Requirements: 24.1_

## Related Files
| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/Remote.cshtml` | Modify | Cập nhật ma trận phân quyền và Audit Log |
| `TotalParking/Views/Home/Cards.cshtml` | Modify | Thêm cột trạng thái đồng bộ HMI |
| `TotalParking/Views/Home/Settings.cshtml` | Modify | Thêm thiết lập Webhook / Swagger tích hợp |

## Completion Criteria
- [ ] Giao diện Remote thể hiện ma trận quyền hạn chi tiết và Audit Log.
- [ ] Danh sách thẻ xe hiển thị rõ trạng thái đồng bộ HMI.

## Verification & Evidence
- [ ] Artifact / runtime verification
  - Inspect: Truy cập các trang cài đặt và remote trong bãi.
  - Expect: Các cài đặt cổng API và nhật ký audit log được biểu thị chính xác trên UI.
