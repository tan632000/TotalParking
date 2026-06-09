# Kế hoạch Triển khai: Chuyển đổi SCADA sang HTML/CSS & Razor Views Thuần

Tài liệu này mô tả kế hoạch xuất giao diện SCADA từ dự án React (`TotalParkingLayout`) thành các file HTML/CSS tĩnh và tích hợp trực tiếp vào dự án C# ASP.NET MVC (`TotalParking`) dưới dạng các Razor Views (`.cshtml`) độc lập.

---

## 🎯 Mục tiêu Kiến trúc mới

1. **Không chạy React ở Client-side:** Trình duyệt chỉ tải HTML tĩnh và file CSS duy nhất, giúp tối ưu hóa hiệu năng và đơn giản hóa việc chỉnh sửa code sau này.
2. **Cấu trúc Razor View chuẩn mực:**
   - Tạo một layout chung `Views/Shared/_ScadaLayout.cshtml` chứa khung HTML, Header và Sidebar.
   - Các trang màn hình (`Index`, `FloorPlan`, `Zones`, `Routing`,...) sẽ là các file `.cshtml` độc lập chỉ chứa phần nội dung chính (Main Content) của màn hình đó và kế thừa từ `_ScadaLayout.cshtml`.
3. **Quản lý CSS tập trung:** Gom toàn bộ CSS của Tailwind đã compile vào file `TotalParking/Content/scada.css`.

---

## 🗺️ Sơ đồ Cấu trúc File tích hợp

```
TotalParking/
├── Content/
│   └── scada.css                  <-- Chứa toàn bộ CSS Tailwind được xuất ra
├── Views/
│   ├── Shared/
│   │   └── _ScadaLayout.cshtml    <-- Chứa khung HTML chính, Header và Sidebar
│   └── Home/
│       ├── Index.cshtml           <-- Giao diện Dashboard (Tổng quan)
│       ├── FloorPlan.cshtml       <-- Giao diện Mặt bằng
│       ├── Zones.cshtml           <-- Giao diện Zone/Block
│       ├── Routing.cshtml         <-- Giao diện Điều hướng xe
│       ├── Alarms.cshtml          <-- Giao diện Alarm & Event
│       ├── Maintenance.cshtml     <-- Giao diện Bảo trì
│       ├── Reports.cshtml         <-- Giao diện Báo cáo
│       ├── Cards.cshtml           <-- Giao diện Quản lý thẻ
│       ├── Settings.cshtml        <-- Giao diện Cài đặt
│       └── Remote.cshtml          <-- Giao diện Remote Support
```

---

## ⚙️ Quy trình Chuyển đổi & Đóng gói tự động

Do giao diện React có cấu trúc phức tạp và chứa nhiều CSS Tailwind động, chúng ta sẽ tự động hóa việc xuất HTML bằng một Node.js script sử dụng **Puppeteer** (hoặc Playwright) để chụp lại DOM thực tế của từng màn hình khi chạy trên trình duyệt ảo:

1. **Khởi chạy Server tạm thời:** Script sẽ start Vite dev server để chạy ứng dụng React.
2. **Quét DOM từng trang:** Sử dụng headless browser truy cập lần lượt 10 URL màn hình, đợi React render xong và trích xuất:
   - Phần HTML của Header & Sidebar -> lưu vào `_ScadaLayout.cshtml`.
   - Phần HTML của nội dung chính (Main Content area) -> lưu vào từng file `.cshtml` tương ứng.
3. **Sao chép CSS:** Biên dịch và lưu file CSS của Vite vào `Content/scada.css`.
4. **Liên kết dự án:** Đăng ký các file `.cshtml` mới vào file dự án `TotalParking.csproj`.

---

## 📝 Các bước triển khai chi tiết

### Bước 1: Chuẩn bị Script Xuất HTML tĩnh
- Cập nhật thư mục `TotalParkingLayout/scripts/` với script `export-html.cjs`.
- Script này sẽ cài đặt tạm thời `puppeteer` để cào DOM sạch từ Vite dev server.

### Bước 2: Tạo Layout chung và các View con
- Script sẽ tạo file `_ScadaLayout.cshtml` với định dạng Razor:
  ```html
  <!DOCTYPE html>
  <html>
  <head>
      <meta charset="UTF-8" />
      <title>TotalParking SCADA</title>
      <link rel="stylesheet" href="~/Content/scada.css" />
  </head>
  <body style="background: #0F172A; color: #F8FAFC; font-family: 'Inter', sans-serif;">
      <div class="flex flex-col h-screen overflow-hidden">
          <!-- Header HTML -->
          @RenderSection("Header", required: false)
          
          <div class="flex flex-1 overflow-hidden">
              <!-- Sidebar HTML -->
              @RenderSection("Sidebar", required: false)
              
              <!-- Main Content -->
              @RenderBody()
          </div>
      </div>
  </body>
  </html>
  ```
- Các file view con (ví dụ `FloorPlan.cshtml`) sẽ được tạo kế thừa từ layout trên:
  ```html
  @{
      Layout = "~/Views/Shared/_ScadaLayout.cshtml";
  }
  @section Header {
      <!-- Header HTML riêng của trang -->
  }
  @section Sidebar {
      <!-- Sidebar HTML riêng của trang -->
  }
  <!-- Main Content HTML của FloorPlan -->
  <div class="flex-1 overflow-auto p-6">
      ...
  </div>
  ```

### Bước 3: Cấu hình Controller trong C#
- Sửa đổi [HomeController.cs](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/Controllers/HomeController.cs) để trả về các View mặc định:
  ```csharp
  public ActionResult Index() { return View(); }
  public ActionResult FloorPlan() { return View(); }
  // ... tương tự cho các trang khác
  ```

### Bước 4: Đăng ký tệp vào .csproj
- Cập nhật [TotalParking.csproj](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking/TotalParking.csproj) để Visual Studio nhận dạng các file `.cshtml` và file CSS mới tạo.
