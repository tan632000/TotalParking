# Task R3-01: (P) Kiểm tra và điều chỉnh tích hợp đa ngôn ngữ ở Header và lưu trữ trạng thái

**Requirement:** R3 — Đồng bộ trạng thái và Ngôn ngữ
**Status:** completed
**Priority:** P2
**Estimated Effort:** 1.5h
**Dependencies:** task-R2-01-react-pathname-initialization.md, task-R2-02-react-sidebar-redirection.md
**Spec:** specs/scada-web-integration/

## Objective
Đảm bảo lựa chọn ngôn ngữ (Tiếng Anh/Tiếng Việt) của người dùng được lưu trữ và đồng nhất khi chuyển trang qua định tuyến backend (vì việc dùng `window.location.href` sẽ làm tải lại trang và làm mất state React thông thường).

## Constraints
- **MUST**: Sử dụng `localStorage` của trình duyệt để duy trì trạng thái ngôn ngữ qua các lần tải lại trang.
- **MUST**: Mặc định là tiếng Việt (`"vi"`) nếu chưa có trạng thái được lưu.

## Implementation Steps

- [ ] 1. Duy trì trạng thái ngôn ngữ qua localStorage trong React
  - [ ] 1.1 Khởi tạo trạng thái lang từ localStorage
    - Mục đích: Đọc ngôn ngữ đã lưu từ lần truy cập trước để hiển thị đúng giao diện tương ứng khi tải lại trang.
    - Chi tiết: Cập nhật hàm khởi tạo state `lang` trong [App.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/App.tsx):
      ```typescript
      const [lang, setLang] = useState<"vi" | "en">(() => {
        const saved = localStorage.getItem("scada_lang");
        return (saved === "vi" || saved === "en") ? saved : "vi";
      });
      ```
    - _Requirements: 3.1_
  - [ ] 1.2 Cập nhật hàm toggle ngôn ngữ để lưu vào localStorage
    - Mục đích: Ghi nhận lựa chọn mới của người dùng vào bộ nhớ trình duyệt mỗi khi họ nhấn nút chuyển ngôn ngữ.
    - Chi tiết: Sửa đổi sự kiện chuyển đổi ngôn ngữ truyền cho component `Header` trong [App.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/App.tsx):
      ```typescript
      onLangToggle={() => {
        setLang(current => {
          const next = current === "vi" ? "en" : "vi";
          localStorage.setItem("scada_lang", next);
          return next;
        });
      }}
      ```
    - _Requirements: 3.1_

- [ ] 2. Test coverage cho R3-01
  - [ ] 2.1 Kiểm thử lưu trữ ngôn ngữ
    - Kiểm thử: Chuyển ngôn ngữ sang tiếng Anh -> Tải lại trang (F5) -> Xác nhận giao diện vẫn hiển thị bằng tiếng Anh.
    - _Requirements: 3.1_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParkingLayout/src/app/App.tsx` | Modify | Đọc/Ghi trạng thái `lang` qua `localStorage` |

## Completion Criteria

- [ ] Lựa chọn ngôn ngữ được lưu vào `localStorage.getItem("scada_lang")`.
- [ ] Tải lại trang không làm mất trạng thái ngôn ngữ đã chọn.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): `npm run build` trong `TotalParkingLayout`
  - Expected proof: Trình biên dịch TypeScript chạy thành công không có lỗi.
- [ ] Artifact / runtime verification
  - Inspect: Console trình duyệt sau khi click đổi ngôn ngữ. Chạy lệnh: `localStorage.getItem("scada_lang")`
  - Expect: Trả về `"en"` (nếu đã đổi sang Tiếng Anh) hoặc `"vi"` (nếu chọn Tiếng Việt).
- [ ] Contract / negative-path verification
  - Check: Xóa cache/localStorage của trình duyệt (`localStorage.clear()`) và tải lại trang.
  - Expect: Giao diện tự động phục hồi về ngôn ngữ mặc định là Tiếng Việt (`"vi"`).

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| LocalStorage bị chặn bởi chế độ ẩn danh | Low | Thêm khối `try/catch` bọc quanh thao tác truy cập `localStorage` để tránh crash ứng dụng khi chạy ở các trình duyệt chặn cookie/storage bên thứ ba. |
