# Task R5-01: Xây dựng module Gọi xe vào / Trả xe ra dành cho Operator

**Requirement:** R5 — Điều phối xe - Gọi xe và Trả xe (Operator Vehicle Dispatch)
**Status:** pending
**Priority:** Medium
**Estimated Effort:** 2h
**Dependencies:** tasks/task-R1-01-operation-control-ui.md
**Spec:** specs/operation-control-center/

## Objective

Xây dựng module điều phối xe thủ công dành cho operator ở chế độ Manual, cho phép nhập biển số để gửi xe vào khay trống bất kỳ, hoặc chọn một khay đang có xe để lấy ra thời gian thực.

## Constraints

- **MUST**: Chỉ mở khóa cụm tính năng điều phối này khi block được chuyển sang chế độ hoạt động "Manual".
- **MUST**: Cập nhật trực quan sơ đồ khay đỗ (pallet grid) của block tương ứng ngay sau khi xe được gọi vào hoặc trả ra thành công.
- **SHOULD**: Sử dụng dữ liệu chu kỳ chạy và xe đỗ đồng bộ với dữ liệu chung trong localStorage.

## Implementation Steps

- [ ] 1. Thiết kế Giao diện Điều phối Xe (Manual Dispatch Panels)
  - [ ] 1.1 Tạo Form "Gọi xe vào" (Store Vehicle)
    - Input nhập biển số xe, chọn loại xe (Sedan/SUV), nút Gọi xe vào.
    - _Requirements: 5.1_
  - [ ] 1.2 Tạo Form "Trả xe ra" (Retrieve Vehicle)
    - Hiển thị khay đỗ đang có xe, nút Trả xe ra.
    - _Requirements: 5.1_

- [ ] 2. Phát triển Logic Lưu trữ và Trả xe trong JS
  - [ ] 2.1 Viết logic kiểm soát trạng thái chế độ hoạt động
    - Kiểm tra nếu block đang ở chế độ `Manual` -> mở khóa (enable) form gửi/trả xe, ngược lại thì khóa (disabled) hiển thị thông báo yêu cầu chuyển chế độ.
    - _Requirements: 5.1_
  - [ ] 2.2 Viết hành động Gọi xe vào
    - Khi submit biển số: tìm khay đỗ trống đầu tiên phù hợp của block, cập nhật khay đó thành "Có xe" kèm biển số xe.
    - Chạy pipeline mô phỏng quá trình cất xe (timer chạy mâm xoay).
    - Cập nhật số chu kỳ chạy của khay đỗ tăng thêm 1 đơn vị.
    - _Requirements: 5.2_
  - [ ] 2.3 Viết hành động Trả xe ra
    - Khi chọn một khay đỗ đang có xe và click Trả xe ra: cập nhật khay đó thành "Trống".
    - Chạy pipeline mô phỏng quá trình hạ mâm lấy xe.
    - _Requirements: 5.3_
  - [ ] 2.4 Cập nhật trực tiếp sơ đồ khay đỗ (Pallet Grid Update)
    - Cập nhật màu sắc khay đỗ trực quan (Xanh: trống, Xám: có xe, Vàng: đang hoạt động điều phối).
    - _Requirements: 5.4_

- [ ] 3. Kiểm thử R5
  - [ ] 3.1 Unit tests
    - Kiểm tra khi block ở chế độ Auto, form điều phối xe thủ công bị `disabled`.
    - _Requirements: 5.1_
  - [ ]* 3.2 Integration tests
    - Nhập biển số -> click Gọi xe vào -> xác nhận một khay đỗ trống chuyển sang màu xám đỗ xe và lưu trữ đúng biển số trong localStorage.
    - _Requirements: 5.2, 5.4_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/OperationControl.cshtml` | Modify | Thêm giao diện Gọi/Trả xe thủ công và viết JS xử lý logic điều phối xe |

## Completion Criteria

- [ ] Form điều phối Gọi/Trả xe chỉ được mở khóa sử dụng khi block ở chế độ Manual.
- [ ] Lệnh Gọi xe vào tự động tìm kiếm khay đỗ trống phù hợp, tăng chu kỳ hoạt động khay đỗ lên 1 đơn vị và cập nhật sơ đồ khay đỗ.
- [ ] Lệnh Trả xe ra giải phóng khay đỗ thành Trống và cập nhật trạng thái sơ đồ khay đỗ tức thì.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A
- [ ] Artifact / runtime verification
  - Inspect: Chuyển block sang chế độ Manual -> Nhập biển số xe và bấm "Gọi xe vào".
  - Expect: Sơ đồ khay đỗ của block cập nhật một khay đỗ thành Có xe.
- [ ] Contract / negative-path verification
  - Check: Bấm Gọi xe khi block ở chế độ Auto.
  - Expect: Form bị vô hiệu hóa, không thể click nút.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Hết khay đỗ trống khi gọi xe | Low | Kiểm tra và hiển thị thông báo lỗi "Block đã đầy" nếu không tìm thấy khay đỗ trống phù hợp |
