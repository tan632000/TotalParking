# Research & Design Decisions

---
**Purpose**: Capture discovery findings, architectural investigations, and rationale that inform the technical design.
---

## Summary
- **Feature**: `scada-web-integration`
- **Discovery Scope**: Complex Integration
- **Key Findings**:
  - Giao diện thiết kế React/Vite hiện tại ở [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) sử dụng routing bằng in-memory component state (`screen` state) thay vì URL routing.
  - Dự án C# [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking) là dự án ASP.NET MVC sử dụng Razor view và định tuyến qua RouteConfig.
  - Giải pháp tối ưu để tích hợp và kiểm soát định tuyến từ Backend là: (1) Cấu hình Vite build với `base: "/SCADALayout/"`, (2) Sửa mã nguồn React để đọc `window.location.pathname` làm trạng thái màn hình ban đầu và điều hướng qua `window.location.href`, (3) Copy toàn bộ kết quả build vào thư mục `/SCADALayout/` của dự án C# và phục vụ file `index.html` tĩnh trực tiếp từ Controller (không qua Razor view).

## Research Log

### 1. Phân tích Định tuyến của React và Tích hợp Backend
- **Context**: Làm thế nào để Backend ASP.NET MVC quản lý routing khi chuyển đổi giữa các màn hình UI?
- **Sources Consulted**: [App.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/App.tsx), [Sidebar.tsx](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/src/app/components/scada/Sidebar.tsx).
- **Findings**:
  - Giao diện hiện tại chuyển đổi màn hình bằng hàm `setScreen(Screen)` ở phía Client-side.
  - Nếu giữ nguyên cơ chế client-side routing thuần túy, Backend không thể quản lý và hiển thị đúng màn hình tương ứng khi người dùng nhập trực tiếp URL (ví dụ `/Home/FloorPlan`).
- **Implications**:
  - Cần chỉnh sửa `Sidebar.tsx` để chuyển đổi link menu sang các URL thật trỏ về Controller (ví dụ `/Home/FloorPlan`).
  - Sửa `App.tsx` để khởi tạo `screen` state dựa trên `window.location.pathname` thay vì mặc định `"dashboard"`.

### 2. Phương pháp Phục vụ file HTML tĩnh trong ASP.NET MVC
- **Context**: Tránh lỗi cú pháp Razor do ký tự `@` trong file HTML/JS/CSS đã biên dịch của Vite.
- **Findings**:
  - Nếu copy nội dung file HTML của Vite vào file `.cshtml`, các ký tự `@` trong code CSS hoặc JS (nếu có) sẽ bị trình biên dịch Razor hiểu lầm là code C#, gây ra lỗi biên dịch (Compilation Error).
  - ASP.NET MVC cung cấp phương thức `return File(path, "text/html")` cho phép phục vụ trực tiếp file tĩnh mà không thông qua cơ chế biên dịch Razor.
- **Implications**:
  - Không cần tạo file `.cshtml`. Phục vụ trực tiếp file tĩnh `SCADALayout/index.html` thông qua Controller.
  - Cấu hình `base` trong `vite.config.ts` thành `"/SCADALayout/"` để gom tất cả tài nguyên tĩnh vào một thư mục riêng biệt, tránh làm lộn xộn các thư mục gốc `/Content` hay `/Scripts` của dự án C#.

## Architecture Pattern Evaluation

| Option | Description | Strengths | Risks / Limitations | Notes |
|--------|-------------|-----------|---------------------|-------|
| **Razor View Integration (cshtml)** | Copy nội dung index.html vào Index.cshtml và copy assets ra thư mục gốc | Tích hợp sâu vào MVC views | Dễ gặp lỗi cú pháp Razor (ký tự `@`); làm rác các thư mục tài nguyên gốc | Không khuyến khích |
| **Static File Direct Serving (Khuyến nghị)** | Cấu hình Vite base path, copy toàn bộ thư mục `dist` thành `SCADALayout` trong dự án MVC và dùng `FileResult` | Sạch sẽ, cô lập hoàn toàn mã nguồn FE, không bị lỗi Razor, dễ dàng xóa thư mục React | Đường dẫn tài nguyên tĩnh phụ thuộc vào tên thư mục | Lựa chọn tối ưu |

## Design Decisions

### Decision: Phục vụ trực tiếp file HTML tĩnh từ Controller
- **Context**: Cần host sản phẩm Vite build tĩnh trong C# mà không làm ảnh hưởng tới kiến trúc hiện tại của dự án MVC.
- **Alternatives Considered**:
  1. Dùng Razor view `.cshtml` và copy đè assets vào `/Scripts` / `/Content`.
  2. Phục vụ file `index.html` tĩnh qua phương thức `FileResult` từ `HomeController`.
- **Selected Approach**: Lựa chọn 2.
- **Rationale**: Đảm bảo an toàn 100% trước lỗi biên dịch Razor, giữ cho các file tĩnh SCADA được đóng gói gọn gàng trong thư mục `/SCADALayout/`.
- **Status**: Accepted
- **Trade-offs**: Phải cấu hình `base: "/SCADALayout/"` trong cấu hình Vite để các asset load đúng đường dẫn.

### Decision: Đồng bộ Định tuyến Client-Server bằng Pathname và href
- **Context**: Yêu cầu định tuyến do Backend quản lý (câu hỏi 2 của người dùng).
- **Selected Approach**:
  - Cập nhật Sidebar để chuyển đổi hành vi `onClick` từ gọi state-change sang `window.location.href = URL`.
  - Cập nhật App khởi tạo từ `window.location.pathname`.
- **Rationale**: Thỏa mãn chính xác yêu cầu của người dùng muốn định tuyến qua Backend, dễ dàng cho Backend phân bổ quyền hoặc xử lý link về sau.

## Risks & Mitigations
- **Lỗi 404 khi F5 tải lại trang phụ**: Do Backend chưa map các route `/Home/FloorPlan`, `/Home/Alarms`... về view chính.
  - *Biện pháp:* Khai báo đầy đủ các Action tương ứng trong `HomeController` trả về cùng một file `index.html`.
- **Lệch đường dẫn tài nguyên**: Khi chạy dev server cục bộ của Vite (`localhost:5173`), đường dẫn `/SCADALayout` không tồn tại ở dev server root.
  - *Biện pháp:* Kiểm tra điều kiện dev mode trong React để tự động điều chỉnh linh hoạt (nếu là dev server thì dùng định tuyến client-side và link nội bộ).

## References
- [ASP.NET MVC FileResult](https://docs.microsoft.com/en-us/dotnet/api/system.web.mvc.fileresult) — Tài liệu chính thức về phục vụ file tĩnh.
- [Vite Base Path Configuration](https://vitejs.dev/config/shared-options.html#base) — Cách định cấu hình đường dẫn tài nguyên tĩnh của Vite.
