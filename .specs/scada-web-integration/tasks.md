# Danh sách Nhiệm vụ Triển khai (Implementation Tasks)

Tài liệu này tổng hợp toàn bộ các nhiệm vụ cần thực hiện để tích hợp giao diện SCADA từ dự án React sang ASP.NET MVC C# và dọn dẹp mã nguồn nguồn.

## Danh sách công việc

- [x] **R1: Đóng gói và Host tài nguyên tĩnh**
  - [x] [task-R1-01-vite-config-and-build-script.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R1-01-vite-config-and-build-script.md) - (P) Cấu hình Vite base path và viết script build tự động (2h)
  - [x] [task-R1-02-mvc-static-files.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R1-02-mvc-static-files.md) - (P) Cấu hình MVC HomeController để phục vụ file HTML tĩnh trực tiếp (1.5h)

- [x] **R2: Định tuyến do Backend quản lý**
  - [x] [task-R2-01-react-pathname-initialization.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R2-01-react-pathname-initialization.md) - (P) Sửa đổi React App.tsx để khởi tạo màn hình từ pathname (1.5h)
  - [x] [task-R2-02-react-sidebar-redirection.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R2-02-react-sidebar-redirection.md) - (P) Sửa đổi Sidebar.tsx để chuyển hướng trình duyệt bằng href (1.5h)
  - [x] [task-R2-03-mvc-controller-routing.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R2-03-mvc-controller-routing.md) - (P) Cập nhật HomeController.cs để định tuyến mọi URL SCADA về index.html (1.5h)

- [x] **R3: Đồng bộ trạng thái và Ngôn ngữ**
  - [x] [task-R3-01-localization-header.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R3-01-localization-header.md) - (P) Kiểm tra và điều chỉnh tích hợp đa ngôn ngữ ở Header và lưu trữ trạng thái (1.5h)

- [ ] **R4: Hiệu năng Tải trang**
  - [ ] [task-R4-01-performance-benchmarking.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R4-01-performance-benchmarking.md) - Kiểm tra tốc độ tải trang tĩnh SCADA dưới 1.5 giây (1h)

- [ ] **R5: Tính Độc lập của Mã nguồn**
  - [ ] [task-R5-01-verification-and-deletion.md](file:///c:/Users/HOME/source/repos/TotalParking/.specs/scada-web-integration/tasks/task-R5-01-verification-and-deletion.md) - Kiểm tra vận hành độc lập và xóa thư mục React (1h)

---
*Ghi chú: Ký hiệu `(P)` biểu thị nhiệm vụ có thể triển khai song song.*
