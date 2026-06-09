# Task R2-02: (P) Sửa đổi Sidebar.tsx để chuyển hướng trình duyệt bằng href

**Requirement:** R2 — Định tuyến do Backend quản lý
**Status:** completed
**Priority:** P2
**Estimated Effort:** 1.5h
**Dependencies:** None
**Spec:** specs/scada-web-integration/

## Objective
Cập nhật hành vi nhấp chuột trên Sidebar tại [Sidebar.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/components/scada/Sidebar.tsx) để thực hiện chuyển hướng URL thật về backend C# (sử dụng `window.location.href`), thay vì chỉ cập nhật local state.

## Constraints
- **MUST**: Chuyển hướng đúng đường dẫn backend theo cấu trúc `/Home/[Action]` của MVC.
- **MUST**: Giữ nguyên cơ chế chuyển đổi local state (`onChange`) khi chạy trên local dev server của Vite (`localhost:5173`) để không làm hỏng quy trình xem thử của lập trình viên FE.
- **MUST NOT**: Làm mất các tham số trạng thái như ngôn ngữ hiện tại (`lang`) nếu cần thiết (tuy nhiên hiện tại chỉ cần điều hướng đơn thuần).

## Implementation Steps

- [ ] 1. Sửa đổi hành vi nhấp chuột trên menu Sidebar
  - [ ] 1.1 Thêm bản đồ ánh xạ URL backend
    - Mục đích: Định nghĩa rõ ràng các URL backend tương ứng với từng màn hình SCADA.
    - Chi tiết: Khai báo hằng số `SCREEN_URLS` trong [Sidebar.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/components/scada/Sidebar.tsx):
      ```typescript
      const SCREEN_URLS: Record<Screen, string> = {
        dashboard: "/Home/Index",
        floorplan: "/Home/FloorPlan",
        zones: "/Home/Zones",
        routing: "/Home/Routing",
        alarms: "/Home/Alarms",
        maintenance: "/Home/Maintenance",
        reports: "/Home/Reports",
        cards: "/Home/Cards",
        settings: "/Home/Settings",
        remote: "/Home/Remote",
      };
      ```
    - _Requirements: 2.3_
  - [ ] 1.2 Cập nhật hàm xử lý sự kiện onClick của nút menu
    - Mục đích: Chuyển quyền điều hướng sang Backend khi chạy trên IIS, và giữ nguyên hành vi SPA khi chạy trên Vite Dev Server.
    - Chi tiết: Sửa thuộc tính `onClick` của thẻ `<button>` trong hàm map menu items:
      ```typescript
      onClick={() => {
        if (window.location.origin.includes("localhost:5173")) {
          // Môi trường dev của Vite: dùng SPA local transition
          onChange(item.id);
        } else {
          // Môi trường ASP.NET MVC: chuyển hướng thật
          window.location.href = SCREEN_URLS[item.id];
        }
      }}
      ```
    - _Requirements: 2.3_

- [ ] 2. Test coverage cho R2-02
  - [ ] 2.1 Kiểm thử click và chuyển hướng
    - Kiểm thử: Xác minh rằng khi click vào một menu item, nếu không phải cổng 5173, trình duyệt sẽ gán giá trị mới cho `window.location.href`.
    - _Requirements: 2.3_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParkingLayout/src/app/components/scada/Sidebar.tsx` | Modify | Khai báo SCREEN_URLS và sửa sự kiện onClick để chuyển hướng bằng href |

## Completion Criteria

- [ ] File [Sidebar.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/components/scada/Sidebar.tsx) biên dịch thành công không có lỗi TypeScript.
- [ ] Sự kiện `onClick` của menu item gọi `window.location.href = SCREEN_URLS[item.id]` khi chạy trên IIS.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): `npm run build` trong `TotalParkingLayout`
  - Expected proof: File JS được biên dịch chứa chuỗi URL tương ứng với `SCREEN_URLS` và kiểm tra logic chuyển hướng.
- [ ] Artifact / runtime verification
  - Inspect: Chạy ứng dụng trên IIS Express (`http://localhost:XXXX/Home/Index`) và nhấp chuột vào mục "Mặt bằng" (Floor Plan) trên Sidebar.
  - Expect: Trình duyệt tải lại và URL thay đổi thành `http://localhost:XXXX/Home/FloorPlan`.
- [ ] Contract / negative-path verification
  - Check: Nhấp chuột vào menu đang kích hoạt (active item).
  - Expect: Trình duyệt vẫn chuyển hướng bình thường hoặc không gây crash trang.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Load lại trang gây trễ UI nhẹ | Low | Vì toàn bộ tài nguyên tĩnh đã được IIS cache sau lần tải đầu, việc chuyển hướng và tải lại file HTML tĩnh sẽ diễn ra cực nhanh (< 200ms). |
