# Task 02 — Tìm đường trên mạng mới và trả qua /Monitor/DriverRoute

Status: done

## Outcome
`/Monitor/DriverRoute` trả đường đi trong khung `zones_map` (4800×3584):
- đi từ R1 dọc dải vàng tới nút đến, rồi chặng cuối vào tới vị trí block (cả block cơ khí lẫn khu đỗ nền 901–906);
- dữ liệu thiếu/hỏng/lệch khung/treo → `MESSAGE` có lý do;
- không 500, không đường đoán.

Task này CHƯA publish: publish một lần ở task 03 (C2-R1).

## Scope
- In:
  - Lớp mới `DriverLaneMap`:
    - hằng `FrameW = 4800`, `FrameH = 3584`;
    - đọc `~/App_Data/driver_lanes.json` (Newtonsoft); đường dẫn nhận qua tham số để script kiểm gọi được ngoài IIS;
    - **kiểm toàn vẹn khi nạp**: `frame` bằng hằng; `entry` tồn tại; mọi đầu cạnh và mọi `blocks[*].node` có trong `nodes`. Sai → kết quả lỗi kèm lý do;
    - chỉ giữ bản nạp **thành công**, dưới dạng snapshot bất biến thay nguyên tử qua một tham chiếu `volatile`; đọc lại khi `LastWriteTimeUtc` đổi; lần nạp lỗi KHÔNG được cache;
    - `RouteToBlock(int blockNo)` trả `LaneNetwork.RouteResult`: điểm của đường đi trên mạng, cộng điểm cuối = `blocks[n].x/y`;
    - `RouteToBlock` KHÔNG BAO GIỜ ném lỗi: mọi lỗi (đọc file, `MapPath`, dữ liệu) → `RouteResult.No(lý do)`. Controller giữ instance tĩnh, constructor không đọc file.
  - Dùng lại Dijkstra: `LaneNetwork.Search` đổi `private` → `internal`, không đổi hành vi.
  - `MonitorController`:
    - `DriverRoute`, `Waiting`, `Message` dùng `DriverLaneMap`;
    - `view_w`/`view_h` = `DriverLaneMap.FrameW/FrameH` (hằng, kể cả khi file lỗi);
    - route không tìm được → `Message(...)`.
  - `TotalParking.csproj`: `Compile Include="Services\DriverLaneMap.cs"`, `Content Include="App_Data\driver_lanes.json"`.
  - Script kiểm `tools/kiem_chung_duong_tai_xe.ps1`:
    - phần DLL (`-ChiDll`);
    - phần live do task 03 bổ sung.
- Out:
  - `RoutingState`, `BlockMap`, `Simulate` và trang Routing giữ `LaneNetwork` + `plan_map` (giới hạn C2-R9);
  - không sửa bảng `lane_*`;
  - **không publish**.

## Coverage
- CP-02

## Ownership
- Create: `TotalParking/Services/DriverLaneMap.cs`
- Modify: `TotalParking/Services/LaneNetwork.cs` (chỉ `Search` → `internal`)
- Modify: `TotalParking/Controllers/MonitorController.cs` (DriverRoute, Waiting, Message)
- Modify: `TotalParking/TotalParking.csproj`
- Create: `tools/kiem_chung_duong_tai_xe.ps1`
- Read: `TotalParking/App_Data/driver_lanes.json`

## Acceptance
- AC-05 (DLL): với mọi `block_no` trong `blocks` của file thật (112 + 901–906), `RouteToBlock`:
  - `Found = true`, ≥ 2 điểm;
  - điểm đầu = toạ độ nút `entry`;
  - điểm cuối = `blocks[n].x/y`;
  - mọi điểm trong 4800×3584.
- AC-06 (DLL), mỗi ca → `Found = false`, `Reason` khác rỗng, không ném lỗi:
  - file không tồn tại;
  - JSON hỏng;
  - `frame` 1594×1300;
  - cạnh trỏ id lạ (`dll_nut_treo`);
  - block không có trong file.
  Gọi qua instance, không chỉ hàm tĩnh. Sau một lần nạp lỗi, sửa file → lần gọi kế tiếp nạp được (lỗi không bị cache).

## Dependencies
- task-01-so-hoa-duong-vang.md

## Verification Plan
- Command: `& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.csproj /p:Configuration=Debug /v:minimal /nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1 -ChiDll; exit $LASTEXITCODE`
- Named probe: `tools/kiem_chung_duong_tai_xe.ps1 -ChiDll`:
  - nạp `bin\TotalParking.dll` bằng `LoadFrom`;
  - ca `dll_moi_block`, `dll_file_thieu`, `dll_file_hong`, `dll_khung_lech`, `dll_nut_treo`, `dll_block_la`, `dll_khong_cache_loi`;
  - file lỗi tạo trong thư mục tạm, xoá sau.
  Thoát 0 khi mọi ca PASS.
- Reachability: DLL build từ source; không cần IIS.
- Oracle: mọi dòng PASS, exit 0.
- Counterexample:
  - điểm cuối là nút trên làn thay vì block → `dll_moi_block` FAIL;
  - thiếu kiểm toàn vẹn → `dll_nut_treo` ném KeyNotFoundException → FAIL;
  - cache lần nạp lỗi → `dll_khong_cache_loi` FAIL;
  - quên khu đỗ nền → `dll_moi_block` thiếu 901–906 → FAIL.
- Artifacts: ephemeral.

## Receipt

Verification: PASS
Command: & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.csproj /p:Configuration=Debug /v:minimal /nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1 -ChiDll; exit $LASTEXITCODE
Exit: 0
Base: d6da2c22525212cb13636124576a202a7cf84fd4
Head: 9ef7b415f4063218a06cfddd5b22c95ec9df3385fc900c523a10c37a7227a58f
```text
$ & "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.csproj /p:Configuration=Debug /v:minimal /nologo; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }; powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_duong_tai_xe.ps1 -ChiDll; exit $LASTEXITCODE
  TotalParking -> C:\Users\Admin\source\repos\TotalParking\TotalParking\bin\TotalParking.dll
PASS  dll_moi_block            118 block (do nen 6), entry=(3480,1860); loi: []
PASS  dll_file_thieu           found=False reason=Thieu file ban do lan duong tai xe (khong_co.json).
PASS  dll_file_hong            found=False reason=File ban do lan duong tai xe hong: Unexpected end of content while loading JObject. Path 'frame.w', line 1, position 24.
PASS  dll_khung_lech           found=False reason=Ban do lan duong tai xe sai khung 1594x1300, can 4800x3584.
PASS  dll_nut_treo             found=False reason=Canh 154-999999 tro toi nut khong ton tai trong ban do lan duong tai xe.
PASS  dll_block_la             found=False reason=Block 777 chua co tren ban do lan duong tai xe.
PASS  dll_khong_cache_loi      lan1 found=False; sau khi sua lan2 found=True so_diem=49

TONG KET: 7/7 PASS
```

Negative controls (sửa tạm `DriverLaneMap.cs`, khôi phục nguyên byte và build lại sau mỗi lượt):
- Bỏ chặng cuối → `dll_moi_block` FAIL (`cuoi_khong_phai_block` cả 118 block), exit 1.
- Bỏ `catch` ngoài + tra block bằng indexer → `dll_block_la` FAIL (ném KeyNotFoundException), exit 1.
- Đường thẳng `[entry] + block` → `dll_moi_block` FAIL (`ap_chot_khong_phai_nut_den`), exit 1.
- Nhảy cóc `[entry, nút đến] + block` → `dll_moi_block` FAIL (`doan_khong_phai_canh`), exit 1.

Review: code-auditor PASS_WITH_WARNINGS (M1: oracle không phân biệt đường bám làn với đường đoán) → bổ sung kiểm điểm áp chót = nút đến và mọi đoạn trên làn là cạnh của JSON; `dll_khong_cache_loi` trả mtime về đúng mốc lần ghi hỏng → re-review PASS.
Hạn chế: chưa publish (theo C2-R1); nếu chạy F5 ở trạng thái này, `DriverGuide` còn nền `plan_map.jpg` nên đường 4800×3584 vẽ lệch cho tới task 03. Toạ độ nút/block không được kiểm nằm trong khung lúc nạp (AC-05 kiểm trên file thật).
