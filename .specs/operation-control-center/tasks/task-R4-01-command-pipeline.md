# Task R4-01: Triển khai bộ gửi lệnh PLC trực tiếp, popup xác nhận, và đèn chỉ báo chu kỳ lệnh

**Requirement:** R4 — Bộ gửi lệnh PLC trực tiếp & Pipeline giám sát (Direct PLC Command Generator & Pipeline)
**Status:** pending
**Priority:** High
**Estimated Effort:** 2h
**Dependencies:** tasks/task-R1-01-operation-control-ui.md
**Spec:** specs/operation-control-center/

## Objective

Xây dựng module gửi lệnh PLC trực tiếp (raw commands) kèm theo hộp thoại xác nhận bảo mật trước khi gửi, và hiển thị chu kỳ phản hồi lệnh bằng chuỗi đèn chỉ báo LED và thanh tiến trình động.

## Constraints

- **MUST**: Hiển thị hộp thoại xác nhận bảo mật trước khi gửi lệnh PLC để tránh vận hành sai.
- **MUST**: Khóa input nhập lệnh khi tiến trình đang chạy (`Running`) để tránh trùng lệnh.
- **MUST**: Trả mã lỗi cụ thể (ví dụ: `ERR_PLC_REJECT_0x0A` khi lệnh bị từ chối).

## Implementation Steps

- [ ] 1. Thiết kế Giao diện Module Lệnh PLC
  - [ ] 1.1 Tạo Input nhập lệnh và nút gửi (Direct Command input)
    - Input text nhập mã lệnh cơ học, nút Send.
    - _Requirements: 4.1_
  - [ ] 1.2 Thiết kế chuỗi đèn LED chỉ báo trạng thái và thanh tiến trình
    - Dựng 4 đèn LED tương ứng: `Command Sent`, `PLC Accepted`, `Running`, `Done / Failed`.
    - Dựng thanh tiến trình (progress bar) thể hiện mức hoàn thành.
    - _Requirements: 4.2_

- [ ] 2. Phát triển logic Hàng đợi và Pipeline lệnh PLC
  - [ ] 2.1 Tạo Modal xác nhận gửi lệnh
    - Khi bấm gửi, hiện Modal: "Bạn có chắc chắn muốn gửi lệnh X xuống block Y?".
    - _Requirements: 4.1_
  - [ ] 2.2 Xây dựng máy trạng thái lệnh PLC thời gian thực
    - Khi xác nhận gửi: kích hoạt chu kỳ đổi màu đèn LED theo thời gian:
      - 0ms: `Command Sent` sáng xanh.
      - 300ms: `PLC Accepted` sáng xanh.
      - 600ms: `Running` sáng xanh và chạy tiến trình % tăng dần.
      - 100% (sau 2.5 giây): cập nhật trạng thái block đích và sáng đèn `Done` xanh lá.
    - _Requirements: 4.2_
  - [ ] 2.3 Xử lý kịch bản lỗi / từ chối của PLC
    - Nếu lệnh nhập vào là "CMD_INVALID" hoặc có lỗi cơ cấu, giả lập PLC từ chối lệnh:
      - Đèn LED chuyển sang `Failed` đỏ rực.
      - Hiển thị mã lỗi cụ thể bên cạnh đèn chỉ báo: `ERR_PLC_REJECT_0x0A`.
    - _Requirements: 4.4_
  - [ ] 2.4 Khóa nhập liệu khi đang chạy
    - Vô hiệu hóa input text và nút Send trong suốt quá trình pipeline lệnh đang chạy ở trạng thái `Running`.
    - _Requirements: 4.3_

- [ ] 3. Kiểm thử R4
  - [ ] 3.1 Unit tests
    - Kiểm tra khi pipeline đang chạy thì input nhập liệu bị `disabled`.
    - _Requirements: 4.3_
  - [ ]* 3.2 Integration tests
    - Nhập lệnh hợp lệ -> bấm gửi -> xác nhận đổi màu đèn LED theo đúng chu kỳ -> block chuyển trạng thái tương ứng.
    - Nhập lệnh không hợp lệ -> kiểm tra hiển thị đèn Failed đỏ và mã lỗi tương ứng.
    - _Requirements: 4.2, 4.4_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Views/Home/OperationControl.cshtml` | Modify | Thiết lập giao diện gửi lệnh PLC, đèn LED chỉ báo và viết Javascript xử lý luồng pipeline |

## Completion Criteria

- [ ] Hiển thị hộp thoại Modal xác nhận trước khi thực hiện gửi lệnh PLC.
- [ ] Trạng thái đèn LED chuyển màu tuần tự theo chu kỳ phản hồi lệnh của PLC.
- [ ] Khóa input nhập lệnh trong suốt quá trình lệnh đang chạy.
- [ ] Hiển thị lỗi đỏ rực và mã lỗi cụ thể khi gửi lệnh không hợp lệ.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A
- [ ] Artifact / runtime verification
  - Inspect: Nhập lệnh và click gửi lệnh trên view OCC.
  - Expect: Đèn LED sáng xanh tuần tự từ trái qua phải, progress bar chạy từ 0-100%.
- [ ] Contract / negative-path verification
  - Check: Nhập lệnh `CMD_INVALID` và xác nhận.
  - Expect: Đèn LED cuối cùng chuyển màu đỏ (Failed) và xuất hiện nhãn lỗi `ERR_PLC_REJECT_0x0A`.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Đơ giao diện do vòng lặp | Low | Sử dụng `setTimeout` hoặc `setInterval` không đồng bộ để tránh nghẽn thread chính của trình duyệt |
