# Task R1-01: Tạo mới trang OCC và định tuyến MVC, tích hợp Sidebar Layout (P)

**Requirement:** R1 — Giao diện Trung tâm Điều khiển Vận hành (OCC UI Layout)
**Status:** pending
**Priority:** High
**Estimated Effort:** 3h
**Dependencies:** None
**Spec:** specs/operation-control-center/

## Objective

Tạo mới Route điều hướng và View Razor cho trang OCC, tích hợp đường dẫn vào thanh Sidebar dùng chung, hiển thị bảng lưới tổng quan các block theo Zone được lọc động.

## Constraints

- **MUST**: Thêm liên kết vào Sidebar trong `_ScadaLayout.cshtml` sử dụng đúng cấu trúc `Url.Action("OperationControl", "Home")` và icon Lucide phù hợp.
- **SHOULD**: Sử dụng cấu trúc Grid chia tỉ lệ responsive `grid-cols-1 lg:grid-cols-3` cho giao diện chính.
- **MUST NOT**: Làm xáo trộn CSS và Layout hiện tại của SCADA.

## Implementation Steps

- [ ] 1. Khởi tạo Action và View MVC
  - [ ] 1.1 Thêm Action `OperationControl()` trong `HomeController.cs`
    - Trả về View tương ứng của trang OCC.
    - _Requirements: 1.1_
  - [ ] 1.2 Tạo mới tệp View `Views/Home/OperationControl.cshtml`
    - Khai báo Layout dùng chung `Layout = "~/Views/Shared/_ScadaLayout.cshtml"`.
    - _Requirements: 1.1_

- [ ] 2. Tích hợp Sidebar Navigation và Giao diện Khung
  - [ ] 2.1 Bổ sung nút liên kết OCC vào Sidebar trong `_ScadaLayout.cshtml`
    - Hiển thị nhãn "Điều khiển vận hành" và biểu tượng Lucide `lucide-sliders`.
    - Thêm chỉ báo active khớp với action `operationcontrol`.
    - _Requirements: 1.1_
  - [ ] 2.2 Xây dựng khung giao diện chính trong `OperationControl.cshtml`
    - Khởi tạo phần Header: Tiêu đề trang, phần chọn Zone (Zone 1 - 6) và khối đỗ (Block).
    - Tạo Container chính: Lưới tổng quan Grid hiển thị thẻ block (bên trái) và Bảng điều khiển block chi tiết (bên phải).
    - _Requirements: 1.2, 1.3, 1.4_

- [ ] 3. Kiểm thử giao diện R1
  - [ ] 3.1 Unit / Routing check
    - Kiểm tra truy cập đường dẫn `/Home/OperationControl` trả về view thành công.
    - _Requirements: 1.1_
  - [ ]* 3.2 Integration tests
    - Kiểm tra click liên kết từ Sidebar chuyển trang mượt mà không lỗi.
    - _Requirements: 1.1_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Controllers/HomeController.cs` | Modify | Thêm Action định tuyến cho trang OCC |
| `TotalParking/Views/Shared/_ScadaLayout.cshtml` | Modify | Tích hợp liên kết Sidebar điều hướng |
| `TotalParking/Views/Home/OperationControl.cshtml` | Create | Trang giao diện chính Trung tâm OCC |

## Completion Criteria

- [ ] Route `/Home/OperationControl` hoạt động ổn định và trả về view chuẩn.
- [ ] Sidebar hiển thị đúng nút điều hướng OCC, tự động làm nổi bật (active) khi truy cập.
- [ ] Giao diện OCC hiển thị đúng bộ lọc Zone và chia khung bố cục Grid tỉ lệ 2:1 cho thẻ block và panel điều khiển.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A (Kiểm tra thủ công trên browser)
- [ ] Artifact / runtime verification
  - Inspect: Truy cập `/Home/OperationControl` trên browser.
  - Expect: Hiển thị giao diện điều khiển OCC tối màu SCADA với đầy đủ Sidebar và khung chức năng.
- [ ] Contract / negative-path verification
  - Check: Click vào liên kết khi đang ở trang khác.
  - Expect: Chuyển trang đúng, trạng thái active chuyển sang nút "Điều khiển vận hành".

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Sai lệch Layout Sidebar | Medium | Giữ nguyên cấu trúc HTML `<a>` và class CSS của các liên kết khác trong Sidebar |
