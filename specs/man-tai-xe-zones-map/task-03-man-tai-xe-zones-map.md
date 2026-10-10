# Task 03 — Màn tài xế dùng zones_map, publish một lần

Status: done

## Outcome
Trang `/Home/DriverGuide`:
- hiện `zones_map.jpeg` làm nền;
- đường xanh đi từ R1 dọc dải vàng vào tới block;
- vòng tròn khoanh quanh block và có số block đúng vẽ đè (số in trên ảnh bị sai);
- kích thước nét theo khung.

Bản deploy được publish MỘT lần cho cả task 02 + 03 và có file `driver_lanes.json`.

## Scope
- In:
  - `<img>` sang `~/Images/zones_map.jpeg`, chống cache theo `LastWriteTimeUtc` như hiện làm với `plan_map.jpg` (`DriverGuide.cshtml:13-16`).
  - `drawRoute`:
    - độ dày nét, bán kính và độ dày vòng tính theo `payload.view_w` thay cho số cứng 14 / 40 / 12;
    - vòng khoanh tại điểm cuối route (= vị trí block);
    - thêm phần tử `<text>` số `payload.block_no` (chữ lớn, viền tối để đọc được trên nền bản vẽ), xoá cùng `clearRoute`.
  - Cập nhật các chú thích còn nói `plan_map.jpg` và "số in sẵn trong ảnh" trong file.
  - Bổ sung phần live của `tools/kiem_chung_duong_tai_xe.ps1`:
    - `live_file_deploy`: file deploy tồn tại, SHA-256 trùng nguồn;
    - `live_khung_driverroute`: `GET /Monitor/DriverRoute` trả 4800×3584;
    - `live_trang_driverguide`: HTML có `zones_map.jpeg?v=`, không có `plan_map.jpg`;
    - `nguon_drawroute`: `DriverGuide.cshtml` không còn số cứng `'14'`/`'40'`/`'12'` trong `drawRoute` và có vẽ `block_no`.
  - Người dùng publish MỘT lần (VS → FolderProfile) sau khi build task 02 + 03; nếu VS báo CS2001 thì đóng/mở lại VS (memory).
- Out: đổi bố cục khác, màu sắc, thanh chữ trên cùng, nhịp poll; sửa ảnh.

## Coverage
- CP-03

## Ownership
- Modify: `TotalParking/Views/Home/DriverGuide.cshtml`
- Modify: `tools/kiem_chung_duong_tai_xe.ps1` (chỉ thêm 4 ca live/nguồn)

## Acceptance
- AC-07: `live_file_deploy`, `live_khung_driverroute`, `live_trang_driverguide` PASS sau một lần publish.
- AC-08:
  - `nguon_drawroute` PASS;
  - người dùng xác nhận ở C3 trên màn thật: khi có xe được điều hướng (cả block cơ khí lẫn đỗ nền), đường xanh từ R1 dọc dải vàng vào đúng block, vòng khoanh đúng block, số hiện đúng.

## Dependencies
- task-02-tim-duong-driverroute.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1; exit $LASTEXITCODE`
- Named probe: `tools/kiem_chung_duong_tai_xe.ps1` (đầy đủ):
  - các ca DLL của task 02;
  - `live_file_deploy`, `live_khung_driverroute`, `live_trang_driverguide`, `nguon_drawroute`.
- Reachability: bản deploy `C:\Users\Admin\Documents\Web\totalParking\` đã publish build task 02 + 03; app ở `http://localhost:8080`.
- Oracle: mọi dòng PASS, exit 0.
- Counterexample:
  - quên `Content Include` JSON → `live_file_deploy` FAIL;
  - còn `plan_map.jpg` → `live_trang_driverguide` FAIL;
  - giữ nét 14 px → `nguon_drawroute` FAIL;
  - publish build cũ → `live_khung_driverroute` thấy 1594 → FAIL.
- Artifacts: ephemeral.

## Xac nhan
- Người dùng xác nhận trên màn thật (AC-08, C3), nguyên văn: "C3 OK".

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1; exit $LASTEXITCODE
Exit: 0
Base: d6da2c22525212cb13636124576a202a7cf84fd4
Head: 118c8647420e05fd9915268f66ba9deddd2616438daef938e7776b81103e7c33
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1; exit $LASTEXITCODE
PASS  dll_moi_block            118 block (do nen 6), entry=(3480,1860); loi: []
PASS  dll_file_thieu           found=False reason=Thieu file ban do lan duong tai xe (khong_co.json).
PASS  dll_file_hong            found=False reason=File ban do lan duong tai xe hong: Unexpected end of content while loading JObject. Path 'frame.w', line 1, position 24.
PASS  dll_khung_lech           found=False reason=Ban do lan duong tai xe sai khung 1594x1300, can 4800x3584.
PASS  dll_nut_treo             found=False reason=Canh 154-999999 tro toi nut khong ton tai trong ban do lan duong tai xe.
PASS  dll_block_la             found=False reason=Block 777 chua co tren ban do lan duong tai xe.
PASS  dll_khong_cache_loi      lan1 found=False; sau khi sua lan2 found=True so_diem=49
PASS  nguon_drawroute          tim_thay=True; so_cung=[]; view_w=True; ve_block_no=True; view_deploy_trung_nguon=True
PASS  live_file_deploy         nguon=EA02FE8853109784 deploy=EA02FE8853109784
PASS  live_khung_driverroute   state=ROUTE view=4800x3584 block=98 reason=
PASS  live_trang_driverguide   zones_map?v=True; plan_map.jpg=False

TONG KET: 11/11 PASS
```

Counterexamples (cùng lệnh, trước khi publish):
- Bản deploy cũ → `live_file_deploy` (thiếu JSON), `live_khung_driverroute` (1594x1300), `live_trang_driverguide` (còn `plan_map.jpg`) FAIL, exit 1.
- Sửa view nguồn mà chưa publish lại → `nguon_drawroute` FAIL (`view_deploy_trung_nguon=False`), exit 1.

Publish: Claude chạy MSBuild VS2019 `/p:DeployOnBuild=true /p:PublishProfile=FolderProfile /p:Configuration=Release` (người dùng cho phép). Web.config deploy không đổi nội dung (chỉ khác khoảng trắng, transform Release; mật khẩu DB khớp); log `App_Data` không bị đè (chỉ `driver_lanes.json` là Content). Publish lần 2 chỉ để đưa bản sửa M1 lên.

Review: code-auditor PASS_WITH_WARNINGS (M1: vòng đích r = 120 px khoanh cả block bên cạnh ở 56/118 block) → giảm `ringR` = 0,014·`view_w`, `ringW` = 0,004·`view_w` (mép ngoài ~77 px < 95 px khoảng cách block nhỏ nhất); siết `nguon_drawroute` (bỏ chú thích, bắt số không nháy, so SHA view nguồn/deploy) → re-review PASS (còn Low: số block có thể chạm mép trên vòng / đè block hàng trên — để soi ở C3).
Hạn chế: AC-08 phần người dùng xác nhận trên màn thật chưa có. Route live tới block 98 đã vẽ thử lên `zones_map` và kết thúc trong block 98 (kiểm bằng ảnh, không phải trình duyệt).
