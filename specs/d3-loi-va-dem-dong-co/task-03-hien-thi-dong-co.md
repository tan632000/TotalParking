# Task 03 — Trang Bảo trì theo bản đồ khối: lỗi và động cơ của từng khối

Status: done

## Outcome
Trang Bảo trì có bố cục như trang Điều khiển vận hành: chọn Zone, thấy lưới thẻ khối (thẻ đỏ khi khối đang có lỗi). Bấm một khối thì bảng bên phải hiện lỗi đang có, lịch sử lỗi gần nhất và thông tin động cơ (số lượt chạy, lượt chạy gần nhất) của khối đó. Toàn bộ nội dung bịa cũ của trang bị bỏ.

## Scope
- In:
  - `BaoTriController` (chỉ đọc, JSON theo khuôn `Json2` của `TotalParking/Controllers/BaoCaoController.cs`):
    - `GET /BaoTri/LoiTheoKhoi` → `{ items: [{ block_no, dang_mo }] }`: số cảnh báo đang mở (`het_luc IS NULL`) theo khối, mọi nguồn, để tô thẻ khối.
    - `GET /BaoTri/Loi?block=<no>&limit=20` → `{ block_no, dang_mo: [...], lich_su: [...] }`. `dang_mo` = mọi cảnh báo đang mở của khối; `lich_su` = `limit` (1..200) cảnh báo gần nhất của khối kể cả đã đóng, mới nhất trước. Mỗi dòng: `ma_loi, muc_do, mo_ta, xay_ra_luc, het_luc, xac_nhan_boi`.
    - `GET /BaoTri/DongCo?block=<no>&limit=50` → `{ block_no, co_du_lieu, ly_do, tong_hop: [{ motor_code, hom_nay, tong, lan_cuoi }], lich_su: [{ motor_code, bat_dau, ket_thuc, thoi_luong_giay }] }`. Danh sách động cơ lấy từ `DongCoDetector.BangBit` theo `slot_count` của khối (động cơ chưa chạy vẫn có mặt với số 0). Bảng `motor_run` chưa có (MySQL 1146) → `co_du_lieu=false`, `ly_do="Chưa có bảng motor_run"`, vẫn trả 200 và danh sách động cơ với số 0.
    - `block` thiếu/không phải số/không phải khối cơ khí → 400 kèm `error`. Lỗi DB khác → 503 kèm `error`.
  - `Maintenance.cshtml`: viết lại toàn trang. Lưới thẻ khối theo Zone từ `/Monitor/BlockMap` (như `OperationControl.cshtml:246`), thẻ có số lỗi đang mở và viền đỏ khi > 0; bảng bên phải ba khu: Lỗi đang có, Lịch sử lỗi, Động cơ (tổng hợp + 50 lượt gần nhất). Trạng thái đang đọc / trống / lỗi cho từng khu. Làm mới lỗi mỗi 15 s. "Hôm nay" = từ 00:00 giờ máy chủ. Bỏ hết nội dung bịa hiện có (lịch bảo trì giả, chẩn đoán `Math.random()` ở `Maintenance.cshtml:449`).
  - `CanhBaoRepository`: thêm hàm đọc cảnh báo theo khối và đếm cảnh báo mở theo khối. `MotorRunRepository`: thêm hàm tổng hợp và lịch sử theo khối.
  - Thêm `<Compile Include>` cho controller; `Maintenance.cshtml` đã có trong csproj.
  - Sidebar giữ action `Maintenance` (đã có), không cần sửa active state.
- Out: so khớp số lượt chạy với DB khi đã có dữ liệu thật (task 04); ngưỡng bảo trì; ghi bất cứ thứ gì.

## Coverage
- CP-03

## Ownership
- Create: `TotalParking/Controllers/BaoTriController.cs`
- Create: `tools/kiem_chung_bao_tri.ps1`
- Modify: `TotalParking/Views/Home/Maintenance.cshtml`, `TotalParking/Services/CanhBaoRepository.cs`, `TotalParking/Services/MotorRunRepository.cs`, `TotalParking/TotalParking.csproj`
- Read: `TotalParking/Views/Home/OperationControl.cshtml`, `TotalParking/Controllers/MonitorController.cs` (BlockMap), `TotalParking/Services/Plc/DongCoDetector.cs`

## Acceptance
- AC-07: bấm một khối cơ khí trên trang Bảo trì hiện ba khu của đúng khối đó; khối đang có cảnh báo mở thì thẻ viền đỏ và hiện số lỗi.
- AC-08: `/BaoTri/Loi?block=X` trả `dang_mo` khớp `canh_bao` đang mở của khối X và `lich_su` ≤ `limit`, mới nhất trước.
- AC-09: khi chưa có bảng `motor_run`, `/BaoTri/DongCo?block=X` trả 200, `co_du_lieu=false` và đủ động cơ của loại khối X với số 0.

## Dependencies
- task-01-loi-d3.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1`
- Named probe: `kiem_chung_bao_tri.ps1` ca
  - `loi_theo_khoi`: tổng `dang_mo` của `/BaoTri/LoiTheoKhoi` bằng `COUNT(*)` cảnh báo đang mở có `block_no` trong DB (chụp DB ngay sau API; lệch thì gọi lại một lần);
  - `loi_mot_khoi`: chọn khối có cảnh báo đang mở; tập id `dang_mo` khớp DB; `lich_su` ≤ 20, `xay_ra_luc` giảm dần; `limit=5` → ≤ 5 dòng;
  - `dong_co_chua_bang`: khi `motor_run` chưa tồn tại → 200, `co_du_lieu=false`, số động cơ = số động cơ của loại khối trong `BangBit`, mọi số 0; khi đã tồn tại → `co_du_lieu=true` và danh sách động cơ đầy đủ (ca ghi rõ nhánh đã chạy);
  - `dau_vao_sai`: `block=abc` → 400; `block=901` (khối đỗ nền) → 400 ở `/BaoTri/DongCo`;
  - `trang`: `GET /Home/Maintenance` → 200, HTML chứa gọi tới cả ba endpoint `/BaoTri/` và không còn chuỗi `Math.random`.
- Reachability: bản deploy `http://localhost:8080` đã chép DLL + `Views/Home/Maintenance.cshtml` mới; MySQL `127.0.0.1` tài khoản ứng dụng.
- Oracle: PASS/FAIL từng ca; thoát 0 khi mọi ca PASS. Giao diện bấm khối do người dùng xác nhận tại C3.
- Counterexample: đếm lỗi lệch DB, trả lỗi của khối khác, quá `limit`, 500 khi thiếu bảng, 200 cho `block=abc`, hay trang còn dữ liệu bịa đều làm script FAIL.
- Artifacts: ephemeral.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1
Exit: 0
Base: 84f7eacca4ce20725a4cfed8f3a73594e74ddd50
Head: 52e2ddb15c9b271c79f413ce063913b6a276c5a7e667d1fd5d66637273d6b6de
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_bao_tri.ps1
PASS  loi_theo_khoi   http=200 khoi_co_loi=56 tong_dang_mo=56 lech=
PASS  loi_mot_khoi    khoi=1 dang_mo=1 lich_su=9 limit5=5
PASS  dong_co         nhanh=DA_CO_BANG loai3=khoi18/5 dc loai5=khoi1/7 dc loai6=khoi6/8 dc loai10=khoi5/12 dc
PASS  dau_vao_sai     Loi?abc=400 DongCo?abc=400 DongCo?901=400 DongCo?thieu=400
PASS  trang           http=200 thieu=[] con_bia=[]

TONG KET: 5/5 PASS
```
- Build: VS2019 MSBuild với `-p:MvcBuildViews=true` EXIT=0. Deploy 16/10/2026: DLL, `Views/Home/Maintenance.cshtml`, `Views/Shared/_ScadaLayout.cshtml` lên `C:\Users\Admin\Documents\Web\totalParking` (bản cũ đuôi `.truoc-baotri-20261016-143434`).
- Đối chứng âm: chạy cùng lệnh TRƯỚC deploy cho 0/5 PASS (404 ở `/BaoTri/*`, trang còn `Math.random`, `Xuất Work Order`).
- Review độc lập: vòng 1 PASS_WITH_WARNINGS (2 Medium + Low 1/2/3 đã sửa: lỗi DB → 503, xoá số đếm khi đổi khối, kiểm URL đúng, so từng động cơ, nhãn cập nhật), vòng 2 PASS.
- Giới hạn: nhánh AC-09 "chưa có bảng `motor_run`" (MySQL 1146 → `co_du_lieu=false`) chỉ được kiểm bằng đọc code — bảng đã được tạo trước khi chạy probe, ca `dong_co` chạy nhánh DA_CO_BANG. `motor_run` hiện chưa có dòng nên so khớp số lượt chạy là việc của task 04. Giao diện bấm khối chờ người dùng xác nhận tại C3.
