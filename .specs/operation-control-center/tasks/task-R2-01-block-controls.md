# Task R2-01: Xây dựng bảng điều khiển Block, nút hành động và đổi chế độ hoạt động

**Requirement:** R2 — Bảng Điều khiển Block & Chuyển chế độ Vận hành (Block Controls & Mode Selector)
**Status:** pending
**Priority:** High
**Estimated Effort:** 2.5h
**Dependencies:** tasks/task-R1-01-operation-control-ui.md
**Spec:** specs/operation-control-center/

## Objective

Xây dựng bảng điều khiển block chi tiết gồm các nút hành động (Start, Stop, Pause, Resume, Lock, Unlock, Disable) và bộ chuyển chế độ hoạt động (Auto, Manual, Maintenance, Emergency), cập nhật trạng thái block lên giao diện và localStorage.

## Constraints

- **MUST**: Kiểm tra quyền truy cập (nếu role không phải Operator hoặc Administrator thì ẩn/khóa các nút điều khiển).
- **MUST**: Khi block ở trạng thái "Disabled", khóa toàn bộ các lệnh Start, Pause, Resume và điều phối xe.
- **SHOULD**: Sử dụng CSS glowing màu sắc cho các trạng thái hoạt động (ví dụ: Running xanh lá nhấp nháy, Emergency đỏ nhấp nháy).

## Implementation Steps

- [ ] 1. Thiết kế Giao diện Bảng Điều khiển & Chế độ Vận hành
  - [ ] 1.1 Tạo cụm nút chuyển chế độ (Mode Selector)
    - Hiển thị 4 nút: Auto, Manual, Maintenance, Emergency.
    - _Requirements: 2.3_
  - [ ] 1.2 Tạo cụm nút hành động điều khiển (Action Buttons)
    - Các nút Start, Stop, Pause, Resume, Lock, Unlock, Disable.
    - _Requirements: 2.1, 2.2_

- [ ] 2. Viết Logic Javascript Xử lý Lệnh & Đồng bộ Trạng thái
  - [ ] 2.1 Quản lý trạng thái block bằng state (`occBlockStates`)
    - Đồng bộ và lưu dữ liệu trạng thái block xuống `localStorage`.
    - Viết hàm `sendBlockCommand(blockId, command)` để cập nhật trạng thái block tương ứng (Start -> "Running", Stop -> "Normal", Pause -> "Normal").
    - _Requirements: 2.1_
  - [ ] 2.2 Viết logic khóa lệnh khi Disable
    - Nếu trạng thái block là "Disabled", vô hiệu hóa (disabled) các nút hành động vận hành thường quy.
    - _Requirements: 2.4_
  - [ ] 2.3 Viết cơ chế chuyển chế độ (Auto/Manual/Maintenance/Emergency)
    - Cập nhật chế độ hoạt động trong `occBlockStates` và thay đổi giao diện nút active tương ứng.
    - _Requirements: 2.3_

- [ ] 3. Kiểm thử logic R2
  - [ ] 3.1 Unit tests
    - Kiểm tra hàm `sendBlockCommand` cập nhật trạng thái block đúng trong localStorage.
    - _Requirements: 2.1_
  - [ ]* 3.2 Integration tests
    - Nhấn "Disable block" -> xác nhận các nút Start/Pause bị disable/mờ đi trên giao diện.
    - _Requirements: 2.4_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/OperationControl.cshtml` | Modify | Thêm giao diện cụm điều khiển và viết script JS xử lý |

## Completion Criteria

- [ ] Các nút chuyển chế độ (Auto/Manual/Maintenance/Emergency) cập nhật trạng thái chế độ block tức thì.
- [ ] Gửi lệnh Start/Stop/Pause/Resume làm thay đổi màu sắc và text trạng thái của block card.
- [ ] Khi chuyển block sang "Disabled", các nút Start/Pause/Resume bị khóa cứng và không thể click.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A
- [ ] Artifact / runtime verification
  - Inspect: Chọn Block A-01 -> Bấm "Disable Block" -> Kiểm tra mã HTML của nút "Start".
  - Expect: Nút "Start" có thuộc tính `disabled` hoặc bị khóa tương tác.
- [ ] Contract / negative-path verification
  - Check: Giả lập đăng nhập tài khoản "Guest" (đăng xuất hoặc thay đổi role).
  - Expect: Không hiển thị/cho phép sử dụng bảng điều khiển block.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Xung đột với trạng thái block ở trang khác | Medium | Sử dụng chung key `occBlockStates` trong localStorage để đồng bộ hóa trạng thái block |
