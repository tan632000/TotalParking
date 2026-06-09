# Task R2-03: (P) Cập nhật HomeController.cs để định tuyến mọi URL SCADA về index.html

**Requirement:** R2 — Định tuyến do Backend quản lý
**Status:** completed
**Priority:** P2
**Estimated Effort:** 1.5h
**Dependencies:** task-R1-02-mvc-static-files.md
**Spec:** specs/scada-web-integration/

## Objective
Khai báo và triển khai tất cả các Action Method tương ứng với phân hệ SCADA trong C# [HomeController.cs](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Controllers/HomeController.cs) để đảm bảo mọi request gửi từ Client đều trả về đúng file `index.html` tĩnh của React.

## Constraints
- **MUST**: Đảm bảo tất cả các action phục vụ đúng file `index.html` tĩnh nằm trong thư mục `/SCADALayout`.
- **MUST**: Trả về `FileResult` tĩnh để tránh Razor parsing.
- **MUST NOT**: Ảnh hưởng tới các action method có sẵn khác nếu chúng không liên quan tới SCADA (như `About`, `Contact`).

## Implementation Steps

- [ ] 1. Khai báo các Action Method SCADA trong HomeController
  - [ ] 1.1 Thêm các Action Method mới vào HomeController
    - Mục đích: Tiếp nhận các request chuyển trang của người dùng từ trình duyệt và trả về view chính chứa ứng dụng React.
    - Chi tiết: Mở file [HomeController.cs](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Controllers/HomeController.cs) và bổ sung các phương thức:
      ```csharp
      public ActionResult FloorPlan()
      {
          return GetScadaView();
      }

      public ActionResult Zones()
      {
          return GetScadaView();
      }

      public ActionResult Routing()
      {
          return GetScadaView();
      }

      public ActionResult Alarms()
      {
          return GetScadaView();
      }

      public ActionResult Maintenance()
      {
          return GetScadaView();
      }

      public ActionResult Reports()
      {
          return GetScadaView();
      }

      public ActionResult Cards()
      {
          return GetScadaView();
      }

      public ActionResult Settings()
      {
          return GetScadaView();
      }

      public ActionResult Remote()
      {
          return GetScadaView();
      }
      ```
    - _Requirements: 2.1_
  - [ ] 1.2 Viết hàm helper GetScadaView để tái sử dụng code
    - Mục đích: Tránh lặp lại mã nguồn đọc file tĩnh trong từng Action.
    - Chi tiết: Thêm phương thức private helper `GetScadaView` vào `HomeController`:
      ```csharp
      private ActionResult GetScadaView()
      {
          string filePath = Server.MapPath("~/SCADALayout/index.html");
          if (!System.IO.File.Exists(filePath))
          {
              return HttpNotFound("SCADA Layout index.html file is missing.");
          }
          return File(filePath, "text/html");
      }
      ```
      Và sửa action `Index()` để gọi helper này:
      ```csharp
      public ActionResult Index()
      {
          return GetScadaView();
      }
      ```
    - _Requirements: 2.1_

- [ ] 2. Test coverage cho R2-03
  - [ ] 2.1 Kiểm tra phản hồi HTTP cho từng Action
    - Kiểm thử: Viết kịch bản test hoặc chạy thử bằng trình duyệt để gửi request lần lượt đến `/Home/FloorPlan`, `/Home/Alarms`, `/Home/Maintenance` và kiểm tra mã phản hồi trả về luôn là 200 OK.
    - _Requirements: 2.1_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Controllers/HomeController.cs` | Modify | Thêm các action method cho SCADA và viết hàm helper GetScadaView |

## Completion Criteria

- [ ] Tất cả các route `/Home/FloorPlan`, `/Home/Alarms`... biên dịch thành công và được đăng ký trong HomeController.
- [ ] Truy cập bất kỳ URL nào trong danh sách trên đều trả về đúng nội dung file `index.html`.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): Build dự án TotalParking C# bằng MSBuild hoặc thông qua IDE Visual Studio.
  - Expected proof: Dự án build thành công không lỗi cú pháp C#.
- [ ] Artifact / runtime verification
  - Inspect: Request HTTP GET tới `http://localhost:XXXX/Home/FloorPlan` và `http://localhost:XXXX/Home/Alarms`.
  - Expect: Mã trạng thái 200 OK, kiểu Content-Type là `text/html`, và nội dung HTML là của React.
- [ ] Contract / negative-path verification
  - Check: Thử truy cập một action MVC khác không được đăng ký trong HomeController (ví dụ `/Home/UnknownAction`).
  - Expect: Trả về lỗi HTTP 404 tiêu chuẩn của IIS/ASP.NET.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Xung đột với các Action sẵn có | Low | Chỉ bổ sung các Action mới phục vụ cho SCADA, không làm thay đổi các Action như `About` hay `Contact` đang có. |
