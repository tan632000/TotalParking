# Task R1-02: (P) Cấu hình MVC HomeController để phục vụ file HTML tĩnh trực tiếp

**Requirement:** R1 — Đóng gói và Host tài nguyên tĩnh
**Status:** completed
**Priority:** P2
**Estimated Effort:** 1.5h
**Dependencies:** task-R1-01-vite-config-and-build-script.md
**Spec:** specs/scada-web-integration/

## Objective
Cấu hình Controller của ASP.NET MVC để phục vụ trực tiếp file `SCADALayout/index.html` tĩnh khi truy cập route chính (Home/Index).

## Constraints
- **MUST**: Trả về đúng kiểu MIME `"text/html"` cho file `index.html`.
- **MUST NOT**: Sử dụng Razor view `.cshtml` cho trang SCADA để tránh lỗi phân tích cú pháp ký tự `@`.
- **MUST**: Xác định đúng đường dẫn vật lý trên server thông qua `Server.MapPath` hoặc `HostingEnvironment`.

## Implementation Steps

- [ ] 1. Sửa đổi HomeController để phục vụ file HTML tĩnh
  - [ ] 1.1 Sửa đổi HomeController Index Action
    - Mục đích: Trả về trực tiếp file `index.html` tĩnh của React thay vì gọi Razor view mặc định của ASP.NET MVC.
    - Chi tiết: Mở file [HomeController.cs](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Controllers/HomeController.cs), sửa phương thức `Index()` thành:
      ```csharp
      public ActionResult Index()
      {
          string filePath = Server.MapPath("~/SCADALayout/index.html");
          if (!System.IO.File.Exists(filePath))
          {
              return HttpNotFound("SCADA Layout index.html file is missing.");
          }
          return File(filePath, "text/html");
      }
      ```
    - _Requirements: 1.3_

- [ ] 2. Test coverage cho R1-02
  - [ ] 2.1 Viết kiểm thử tích hợp cho HomeController
    - Kiểm thử: Tạo một test case hoặc chạy thử cục bộ để gửi request GET tới `/Home/Index` và xác minh rằng response trả về có Header `Content-Type` là `text/html`, và nội dung HTML chứa phần tử `<div id="root">`.
    - _Requirements: 1.3_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParking/Controllers/HomeController.cs` | Modify | Sửa Action Index trả về File tĩnh |

## Completion Criteria

- [ ] Phương thức `Index()` trong [HomeController.cs](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Controllers/HomeController.cs) trả về `File(filePath, "text/html")`.
- [ ] Truy cập `/Home/Index` trả về mã trạng thái HTTP 200 và nội dung trang chứa cấu trúc HTML của React app.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): N/A (Kiểm tra thủ công qua IIS Express / HTTP Client)
  - Expected proof: N/A
- [ ] Artifact / runtime verification
  - Inspect: Request GET tới `http://localhost:XXXX/Home/Index` (sau khi chạy IIS Express)
  - Expect: Nội dung HTML trả về khớp hoàn toàn với file `SCADALayout/index.html` tĩnh, không chứa các thành phần bọc ngoài của `_Layout.cshtml`.
- [ ] Contract / negative-path verification
  - Check: Xóa tạm thời file `SCADALayout/index.html` và truy cập `/Home/Index`.
  - Expect: Server trả về lỗi HTTP 404 với thông báo "SCADA Layout index.html file is missing." thân thiện thay vì ném lỗi crash Exception 500.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| File tĩnh bị thiếu dẫn đến lỗi crash | Low | Đã thêm kiểm tra tồn tại `System.IO.File.Exists(filePath)` và trả về `HttpNotFound` thay vì để ném Exception lỗi đĩa. |
