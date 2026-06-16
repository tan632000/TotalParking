# Task R2-01: Cập nhật giao diện điều hướng xe và tích hợp thiết bị ngoại vi

**Requirement:** R4, R5, R12, R13, R14, R15, R16, R17, R18 — Điều hướng và truyền thông thiết bị
**Status:** pending
**Priority:** High
**Estimated Effort:** 3h
**Dependencies:** None
**Spec:** .specs/ui-functional-upgrade/

## Objective
Cập nhật màn hình điều phối giao thông [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml) hiển thị chính xác các chỉ số từ hệ thống AI-VDS, cân bằng tải 4 zone, trạng thái chỉ thị phát hành thẻ tự động và truyền thông điệp tới LED Matrix ngoài trời, LED chỉ dẫn trong hầm và hệ thống PGS.

## Constraints
- **MUST**: Hiển thị bảng mô phỏng trạng thái xe quá khổ bị từ chối vào bãi xe.
- **MUST**: Thêm các chấm trạng thái (Active/Inactive) cho máy phát thẻ tự động và bảng LEDMatrix ngoài trời.

## Implementation Steps
- [ ] 1. Sửa đổi [Routing.cshtml](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Views/Home/Routing.cshtml)
  - [ ] 1.1 Cập nhật bảng "Mật độ Zone" hiển thị đủ 4 Zone đỗ xe chính. _Requirements: 4.1_
  - [ ] 1.2 Bổ sung phần tử giám sát trạng thái truyền tín hiệu "Máy phát thẻ tự động" và "LED Matrix Ngoài trời" trong mục Trạng thái gửi LED/PGS. _Requirements: 15.1, 18.1_
  - [ ] 1.3 Thiết kế trực quan hiển thị dòng thông tin xe quá khổ (Oversized) với kích thước thực tế lớn và trạng thái "Từ chối" màu đỏ. _Requirements: 16.1_

## Related Files
| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/Routing.cshtml` | Modify | Nâng cấp giao diện điều phối và truyền thông thiết bị |

## Completion Criteria
- [ ] Màn hình điều hướng hiển thị mật độ 4 zone.
- [ ] Hiển thị đầy đủ thông số tiếp nhận từ camera AI-VDS, có dòng từ chối xe quá khổ và đèn trạng thái kết nối các thiết bị ngoại vi.

## Verification & Evidence
- [ ] Artifact / runtime verification
  - Inspect: Truy cập `/Home/Routing` trên trình duyệt.
  - Expect: Hiển thị đúng dải thông báo chế độ Fallback, danh sách xe (gồm xe bị từ chối) và đèn kết nối máy phát thẻ.
