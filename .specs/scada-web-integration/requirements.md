# Requirements Document

## Introduction
Tài liệu này xác định các yêu cầu chức năng và phi chức năng để tích hợp thiết kế web từ dự án React [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) vào backend C# ASP.NET MVC [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking). Dự án sử dụng phương pháp đóng gói tĩnh (HTML/JS/CSS) và quản lý định tuyến hoàn toàn ở backend để chạy ứng dụng độc lập, cho phép xóa mã nguồn React sau khi hoàn tất tích hợp.

## Requirements

### Requirement 1: Đóng gói và Host tài nguyên tĩnh
**Objective:** As a developer, I want to đóng gói mã nguồn React thành các file tĩnh và host chúng trong ASP.NET MVC, so that trang web có thể chạy mà không cần môi trường phát triển React nguyên bản.

#### Acceptance Criteria
1. When chạy lệnh build tĩnh ở React, the build system shall tạo ra thư mục chứa `index.html` và các file assets (.js, .css, ảnh).
2. The ASP.NET MVC project shall phục vụ trực tiếp các file script và style tĩnh từ thư mục `/assets` nằm ở thư mục gốc của dự án.
3. The Index.cshtml view shall chứa toàn bộ nội dung của file `index.html` được biên dịch và không sử dụng layout mặc định của ASP.NET MVC (Layout = null).

### Requirement 2: Định tuyến do Backend quản lý
**Objective:** As a user, I want các thao tác chuyển trang đi qua route của C# backend, so that các URL được quản lý và phục vụ bởi ASP.NET MVC controller.

#### Acceptance Criteria
1. When người dùng gửi yêu cầu tới các URL giao diện SCADA (bao gồm `/Home/Index`, `/Home/FloorPlan`, `/Home/Zones`, `/Home/Routing`, `/Home/Alarms`, `/Home/Maintenance`, `/Home/Reports`, `/Home/Cards`, `/Home/Settings`, `/Home/Remote`), the HomeController shall trả về view Index.
2. While trang web được tải lần đầu, the client-side JavaScript shall đọc pathname từ `window.location.pathname` để xác định màn hình SCADA hoạt động ban đầu.
3. When người dùng nhấp chuột vào một mục menu trên Sidebar, the browser shall chuyển hướng trực tiếp sang đường dẫn URL tương ứng của backend bằng cách thay đổi `window.location.href`.

### Requirement 3: Đồng bộ trạng thái và Ngôn ngữ
**Objective:** As a user, I want giao diện SCADA hiển thị đúng ngôn ngữ lựa chọn, so that tôi có thể dễ dàng theo dõi hệ thống bằng tiếng Việt hoặc tiếng Anh.

#### Acceptance Criteria
1. When người dùng nhấp vào nút chuyển đổi ngôn ngữ ở Header, the client-side UI shall chuyển đổi toàn bộ văn bản hiển thị giữa Tiếng Việt và Tiếng Anh.

## Non-Functional Requirements

### Requirement 4: Hiệu năng Tải trang
**Objective:** As a system owner, I want trang web tải nhanh chóng trên trình duyệt, so that người vận hành SCADA có trải nghiệm mượt mà.

#### Acceptance Criteria
4.1 The system shall tải và kết xuất hoàn chỉnh màn hình Dashboard đầu tiên trong thời gian dưới 1.5 giây trong điều kiện mạng nội bộ.

### Requirement 5: Tính Độc lập của Mã nguồn
**Objective:** As a system owner, I want dự án chạy độc lập với mã nguồn React ban đầu, so that mã nguồn React có thể được xóa bỏ hoàn toàn mà không ảnh hưởng tới vận hành.

#### Acceptance Criteria
5.1 The system shall thực thi và phục vụ tất cả các trang giao diện SCADA bình thường sau khi thư mục [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) bị xóa khỏi máy chủ.
