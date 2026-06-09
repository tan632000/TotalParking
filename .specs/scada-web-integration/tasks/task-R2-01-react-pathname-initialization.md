# Task R2-01: (P) Sửa đổi React App.tsx để khởi tạo màn hình từ pathname

**Requirement:** R2 — Định tuyến do Backend quản lý
**Status:** completed
**Priority:** P2
**Estimated Effort:** 1.5h
**Dependencies:** None
**Spec:** specs/scada-web-integration/

## Objective
Cập nhật file [App.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/App.tsx) để phân tích đường dẫn URL hiện tại của trình duyệt (`window.location.pathname`) và khởi tạo màn hình SCADA hoạt động tương ứng khi trang web tải lên.

## Constraints
- **MUST**: Khớp đúng các chuỗi con trong URL với định danh màn hình (`Screen`).
- **MUST**: Mặc định hiển thị màn hình `"dashboard"` nếu URL không khớp với bất kỳ từ khóa nào.
- **MUST**: Hỗ trợ chạy bình thường cả trên môi trường local dev server (Vite) và môi trường host C# MVC.

## Implementation Steps

- [ ] 1. Khởi tạo screen state dựa trên pathname
  - [ ] 1.1 Tạo hàm helper getScreenFromPath
    - Mục đích: Trích xuất và khớp pathname của trình duyệt với danh sách định danh màn hình SCADA hợp lệ.
    - Chi tiết: Viết hàm `getScreenFromPath(): Screen` trong [App.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/App.tsx) thực hiện:
      ```typescript
      function getScreenFromPath(): Screen {
        const path = window.location.pathname.toLowerCase();
        if (path.includes("/floorplan")) return "floorplan";
        if (path.includes("/zones")) return "zones";
        if (path.includes("/routing")) return "routing";
        if (path.includes("/alarms")) return "alarms";
        if (path.includes("/maintenance")) return "maintenance";
        if (path.includes("/reports")) return "reports";
        if (path.includes("/cards")) return "cards";
        if (path.includes("/settings")) return "settings";
        if (path.includes("/remote")) return "remote";
        return "dashboard";
      }
      ```
    - _Requirements: 2.2_
  - [ ] 1.2 Cập nhật khởi tạo state trong App component
    - Mục đích: Sử dụng hàm helper để đặt trạng thái ban đầu của màn hình hoạt động khi React render lần đầu.
    - Chi tiết: Sửa đổi dòng khai báo state trong component `App`:
      ```typescript
      const [screen, setScreen] = useState<Screen>(getScreenFromPath());
      ```
    - _Requirements: 2.2_

- [ ] 2. Test coverage cho R2-01
  - [ ] 2.1 Viết unit test cho hàm getScreenFromPath
    - Kiểm thử: Viết test case giả lập `window.location.pathname` với các giá trị khác nhau (ví dụ `/Home/FloorPlan`, `/Home/Alarms`, `/`, `/invalid-path`) và đảm bảo hàm trả về đúng kết quả mong đợi.
    - _Requirements: 2.2_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParkingLayout/src/app/App.tsx` | Modify | Thêm hàm getScreenFromPath và sửa đổi khởi tạo screen state |

## Completion Criteria

- [ ] Hàm `getScreenFromPath` hoạt động chính xác và không gây ra lỗi kiểu dữ liệu (TypeScript).
- [ ] Khi chạy Vite dev server, truy cập đường dẫn chứa `/floorplan` sẽ tự động hiển thị màn hình Mặt bằng.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): `npm run build` chạy từ thư mục `c:\Users\HOME\source\repos\TotalParking\TotalParkingLayout`
  - Expected proof: Trình biên dịch TypeScript xác thực không có lỗi kiểu dữ liệu trong App.tsx.
- [ ] Artifact / runtime verification
  - Inspect: Thay đổi thủ công URL trên trình duyệt thành `http://localhost:5173/Home/FloorPlan` khi chạy dev server.
  - Expect: Giao diện React khởi động hiển thị trực tiếp màn hình Sơ đồ tầng (FloorPlan) thay vì Dashboard.
- [ ] Contract / negative-path verification
  - Check: Thử truy cập URL không hợp lệ (ví dụ: `/Home/invalid_screen`).
  - Expect: React nhận diện không khớp và tự động fallback về màn hình Tổng quan (dashboard).

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Lỗi window is not defined trong môi trường không phải browser | Low | Mã nguồn chỉ chạy trên Client-side (Vite SPA) nên đối tượng `window` luôn khả dụng. |
