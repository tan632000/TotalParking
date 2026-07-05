# Task R3-01: Triển khai Reset lỗi, cơ cấu Homming và Checklist hướng dẫn phục hồi động

**Requirement:** R3 — Phục hồi sau lỗi & Checklist hướng dẫn động (Fault Reset & Guided Recovery Checklist)
**Status:** pending
**Priority:** High
**Estimated Effort:** 2.5h
**Dependencies:** tasks/task-R2-01-block-controls.md
**Spec:** specs/operation-control-center/

## Objective

Xây dựng bảng checklist động hướng dẫn phục hồi sự cố cho block bị lỗi, khóa các nút Reset PLC/Home cho đến khi người vận hành hoàn thành tất cả các bước chuẩn bị vật lý, đồng thời lưu trạng thái checklist vào `localStorage`.

## Constraints

- **MUST**: Lưu trạng thái tiến trình checklist phục hồi sự cố vào `localStorage` (`occActiveRecovery`) để không bị mất khi load lại trang.
- **MUST**: Khóa nút "Reset PLC Fault & Home Block" cho đến khi tất cả các checkbox trước đó được tích chọn.
- **SHOULD**: Đồng bộ gỡ bỏ cảnh báo lỗi tương ứng trong danh sách `activeAlarms` của Dashboard sau khi reset thành công.

## Implementation Steps

- [ ] 1. Thiết kế Giao diện Bảng Hướng dẫn Phục hồi sự cố (Guided Recovery Panel)
  - [ ] 1.1 Tạo Container cảnh báo lỗi màu đỏ nổi bật
    - Hiển thị mã lỗi hoạt động (ví dụ: `ERR-MTR-04` cho Block B-04) và mô tả.
    - _Requirements: 3.1_
  - [ ] 1.2 Thiết lập danh sách checklist động các bước kiểm tra
    - Tùy thuộc vào mã lỗi, render các bước checklist kiểm tra (ví dụ lỗi motor, lỗi cảm biến quang).
    - _Requirements: 3.2_

- [ ] 2. Phát triển logic Checklist động và Reset PLC
  - [ ] 2.1 Viết hàm kiểm soát tích chọn checkbox
    - Khi các checkbox được tích chọn, lưu trạng thái vào `occActiveRecovery`.
    - Kiểm tra nếu toàn bộ các bước kiểm tra thực địa được chọn -> kích hoạt (enable) nút "Reset PLC Fault & Home Block".
    - _Requirements: 3.2, 3.3, 8.1_
  - [ ] 2.2 Viết hành động Reset Fault và Homming cơ cấu
    - Khi click "Reset PLC Fault & Home Block", cập nhật trạng thái block trong `occBlockStates` từ "Error" về "Normal".
    - Đồng bộ xóa lỗi này khỏi danh sách `activeAlarms` để cập nhật Dashboard.
    - Xóa trạng thái checklist phục hồi của block khỏi `localStorage`.
    - _Requirements: 3.4_
  - [ ] 2.3 Khôi phục checklist khi tải lại trang (Persistence)
    - Khi trang khởi chạy, kiểm tra nếu block được chọn đang ở trạng thái lỗi và có checklist lưu trữ trong `occActiveRecovery` -> tự động khôi phục giao diện checklist.
    - _Requirements: 8.2_

- [ ] 3. Kiểm thử R3
  - [ ] 3.1 Unit tests
    - Kiểm tra khi chưa chọn đủ checklist thì nút reset ở trạng thái `disabled`.
    - _Requirements: 3.3_
  - [ ]* 3.2 Integration tests
    - Chọn block lỗi -> Tích đủ checklist -> Nhấn Reset -> Xác nhận block chuyển sang Normal và biến mất bảng hướng dẫn phục hồi.
    - _Requirements: 3.4_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/OperationControl.cshtml` | Modify | Thêm giao diện checklist phục hồi lỗi và mã JavaScript tương ứng |

## Completion Criteria

- [ ] Khi chọn một block lỗi, bảng cảnh báo đỏ và checklist phục hồi sự cố hiển thị.
- [ ] Nút Reset PLC/Home chỉ được kích hoạt (mở khóa) khi toàn bộ checklist kiểm tra được tích chọn.
- [ ] Nhấn Reset PLC/Home phục hồi trạng thái block về Normal (màu xanh lá) và đồng thời xóa cảnh báo lỗi đó khỏi Dashboard (dữ liệu `activeAlarms`).
- [ ] Tải lại trang (F5) khi đang làm dở checklist phục hồi sự cố, trạng thái checklist đã tích chọn được giữ nguyên.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A
- [ ] Artifact / runtime verification
  - Inspect: F5 trang OCC sau khi tích chọn 1/3 bước checklist.
  - Expect: Checkbox đã chọn vẫn được tích.
- [ ] Contract / negative-path verification
  - Check: Click nút Reset PLC/Home bằng Javascript Console khi nút đang bị khóa.
  - Expect: Lệnh bị từ chối do trạng thái block và checklist chưa hoàn thành.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Sai lệch dữ liệu alarm đồng bộ | Medium | Sử dụng đúng cấu trúc dữ liệu `activeAlarms` đã có trong `Index.cshtml` để đồng bộ xóa lỗi |
