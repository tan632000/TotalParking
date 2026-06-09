# Task R1-01: (P) Cấu hình Vite base path và viết script build tự động

**Requirement:** R1 — Đóng gói và Host tài nguyên tĩnh
**Status:** completed
**Priority:** P2
**Estimated Effort:** 2h
**Dependencies:** None
**Spec:** specs/scada-web-integration/

## Objective
Thiết lập đường dẫn cơ sở (base path) cho dự án React/Vite tại [TotalParkingLayout](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout) và viết script Node.js tự động chạy build và sao chép kết quả sang thư mục `/SCADALayout` của dự án C#.

## Constraints
- **MUST**: Đặt đường dẫn base của Vite thành `"/SCADALayout/"`.
- **MUST**: Sao chép đúng cấu trúc thư mục của Vite `dist/` sang `TotalParking/SCADALayout/`.
- **MUST NOT**: Sao chép các thư mục thừa hoặc `node_modules`.

## Implementation Steps

- [ ] 1. Cấu hình base path cho Vite và tạo script tự động
  - [ ] 1.1 Cấu hình base path trong file config của Vite
    - Mục đích: Đảm bảo các đường dẫn tài nguyên tĩnh của ứng dụng React sau khi build sẽ sử dụng tiền tố `/SCADALayout/` thay vì `/` để khớp với thư mục host trong IIS/C#.
    - Chi tiết: Sửa file [vite.config.ts](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/vite.config.ts), thêm cấu hình `base: '/SCADALayout/'` vào đối tượng xuất ra của `defineConfig`.
    - _Requirements: 1.1_
  - [ ] 1.2 Tạo tập lệnh tự động hóa quy trình đóng gói và sao chép tài nguyên
    - Mục đích: Tự động hóa quá trình chạy `npm run build` và chuyển các file đầu ra sang thư mục `/SCADALayout` của dự án C# [TotalParking](file:///c:/Users/HOME/source/repos/TotalParking/TotalParking).
    - Chi tiết: Tạo file kịch bản Node.js (ví dụ `scripts/build-and-copy.js` trong thư mục `TotalParkingLayout`), sử dụng module `fs` và `child_process` để:
      1. Chạy lệnh `npm run build`.
      2. Xóa thư mục `TotalParking/SCADALayout` cũ nếu có.
      3. Tạo mới thư mục `TotalParking/SCADALayout` và copy toàn bộ nội dung từ `TotalParkingLayout/dist` sang.
    - _Requirements: 1.1, 1.2_
  - [ ] 1.3 Đăng ký script trong package.json
    - Mục đích: Giúp nhà phát triển dễ dàng chạy toàn bộ quy trình tích hợp qua một lệnh `npm`.
    - Chi tiết: Thêm một script mới `"build:mvc": "node scripts/build-and-copy.js"` vào phần `"scripts"` của file [package.json](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/package.json).
    - _Requirements: 1.1_

- [ ] 2. Test coverage cho R1-01
  - [ ] 2.1 Kiểm thử tự động kịch bản build
    - Kiểm thử: Chạy thử kịch bản build trên terminal và kiểm tra xem thư mục `TotalParking/SCADALayout` có được tạo thành công với cấu trúc chuẩn gồm `index.html` và thư mục con `assets/` không.
    - _Requirements: 1.1_

## Related Files

| Path | Action | Description |
|---|---|---|
| `TotalParkingLayout/vite.config.ts` | Modify | Thêm cấu hình `base: '/SCADALayout/'` |
| `TotalParkingLayout/package.json` | Modify | Đăng ký script build tích hợp mới |
| `TotalParkingLayout/scripts/build-and-copy.js` | Create | File script tự động chạy build và sao chép tài nguyên |

## Completion Criteria

- [ ] File [vite.config.ts](file:///c:/Users/HOME/source/repos/TotalParking/TotalParkingLayout/vite.config.ts) chứa `base: "/SCADALayout/"`.
- [ ] Thư mục `TotalParking/SCADALayout/` được sinh ra tự động chứa đầy đủ `index.html` và thư mục `assets/` sau khi chạy lệnh.
- [ ] Lệnh `npm run build:mvc` thực thi thành công không báo lỗi.

## Verification & Evidence

- [ ] Automated verification
  - Command(s): `npm run build:mvc` chạy từ thư mục `c:\Users\HOME\source\repos\TotalParking\TotalParkingLayout`
  - Expected proof: Terminal xuất kết quả build thành công của Vite và báo sao chép file hoàn tất mà không gặp lỗi thoát.
- [ ] Artifact / runtime verification
  - Inspect: `c:\Users\HOME\source\repos\TotalParking\TotalParking\SCADALayout\index.html`
  - Expect: File tồn tại và các thẻ `<script>` hoặc `<link>` trỏ vào `/SCADALayout/assets/index-...`
- [ ] Contract / negative-path verification
  - Check: Thử chạy script khi thư mục đích `SCADALayout` đang bị khóa hoặc không có quyền ghi.
  - Expect: Script ghi lại log lỗi rõ ràng và dừng tiến trình thay vì báo thành công giả tạo.

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Đường dẫn copy sai do khác biệt OS | Medium | Sử dụng thư viện `path.resolve` và các hàm NodeJS đa nền tảng để tránh lỗi đường dẫn Windows/Linux. |
| Node.js chưa được cài đặt hoặc thiếu quyền ghi | Low | Chạy kiểm tra quyền thư mục trước khi thực hiện xóa hoặc ghi đè file. |
