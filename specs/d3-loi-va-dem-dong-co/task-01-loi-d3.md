# Task 01 — Lỗi D3 hiện và tự đóng trên trang Cảnh báo

Status: done

## Outcome
Khi PLC của một khối báo lỗi qua thanh ghi D3 liên tục ít nhất 3 lần đọc, trang Cảnh báo hiện lỗi đó với nhãn tiếng Việt; khi D3 về 0 liên tục 3 lần đọc, cảnh báo được đóng kèm tự xác nhận `CanhBaoRepository.NguoiXacNhanTuDong` (`"System"`) (còn trong lịch sử, không còn ở trạng thái chờ).

## Scope
- In:
  - `PlcConnection.ReadIoAsync`: MỘT hàm mới đọc DM3 (1 word) rồi CIO 100..102 (3 word) trong cùng một lần giữ `_gate`. Lấy gate bằng `WaitAsync(0)` — gate bận thì trả "bỏ nhịp", không chờ (vòng quẹt thẻ `PlcConnection.cs:140` giữ quyền ưu tiên). Timeout riêng `plc:ioTimeoutMs` (mặc định 800). KHÔNG gọi `EnsureConnectedAsync`/kết nối lại: kết nối chưa sẵn sàng → trả "bỏ nhịp" (vòng quẹt thẻ lo việc kết nối lại), để một PLC chết không kéo dài cả nhịp. Lỗi đọc KHÔNG gọi `Fail()`/ngắt kết nối, không đổi `IsOnline`. CIO được đọc ngay từ task này để task 02 không phải sửa lại file này.
  - `PlcIoPoller`: vòng `while` — chạy hết một nhịp (`Task.WhenAll`, `SemaphoreSlim(16)` riêng) rồi mới chờ phần còn lại của `plc:ioPollMs` (mặc định 1000, tối thiểu 500), theo mẫu `PlcConnectionManager.cs:204-214`; không có hai nhịp chồng nhau. Tắt được bằng `plc:ioPoll = false`. Start cạnh `CanhBaoPlcService.Start()` (`PlcHost.cs:94`); `Stop()` gọi trong `PlcHost.Stop` (`PlcHost.cs:131`) TRƯỚC khi manager bị Dispose, chờ nhịp đang chạy kết thúc. Giữ `LastTickMs`, `LastReadOk`, `LastSkipped` để chẩn đoán.
  - `LoiD3Service`: bảng 27 mã chép NGUYÊN VĂN cột "Language 3, State 1" của `docs/error.xls`: 1, 2, 3, 5, 6, 7, 9, 10, 11, 12, 14, 17, 18, 20, 22, 74, 75, 79, 80, 84, 85, 102, 103, 110, 114, 115, 119 (mã = số sau `故障`). Nhãn giữ đúng chữ trong file.
  - Chống rung: mỗi khối giữ "mã ứng viên" + số lần liên tiếp; mã ổn định = mã đã thấy 3 lần đọc thành công liên tiếp (`plc:d3Debounce`, mặc định 3). Lần đọc lỗi/bỏ nhịp không đếm và không reset.
  - Hoà giải theo DB, không theo bộ nhớ: mỗi khi mã ổn định của một khối được xác lập (kể cả lần đầu sau khởi động), đọc khoá đang mở `PLC_D3:<block_no>:` của khối đó rồi làm cho khớp:
    - khoá mở mà không phải mã ổn định → đóng bằng `DongTheoKhoa(khoa, CanhBaoRepository.NguoiXacNhanTuDong)` (như `CanhBaoPlcService.cs:220`);
    - mã ổn định có trong bảng mà chưa có khoá → mở `nguon='hardware'`, `muc_do='high'`, `ma_loi='PLC-D3-<mã>'`, `block_no`, `zone_id`, `mo_ta=<nhãn>`, khoá `PLC_D3:<block_no>:<mã>`.
    - DB lỗi → không đổi trạng thái "đã hoà giải" của khối; nhịp ổn định kế tiếp hoà giải lại.
  - Mã 0 hoặc mã không có trong bảng → không mở gì (C1: bỏ qua mã lạ); mã lạ được ghi vào `PlcAuditLog` (mỗi khối/mã một lần mỗi giờ).
  - Khối không đọc được → không đụng tới cảnh báo của khối đó.
  - Kho lưu trữ qua interface `ICanhBaoD3Store` (đọc khoá mở theo khối / mở / đóng); bản thật bọc `CanhBaoRepository`, để probe thay bằng kho trong bộ nhớ.
  - Thêm `<Compile Include>` cho file mới vào `TotalParking.csproj`.
- Out: nhận lượt chạy động cơ (task 02); sửa giao diện trang Cảnh báo.

## Coverage
- CP-01

## Ownership
- Create: `TotalParking/Services/Plc/PlcIoPoller.cs`
- Create: `TotalParking/Services/Plc/LoiD3Service.cs`
- Create: `tools/kiem_chung_loi_d3.ps1`
- Modify: `TotalParking/Services/Plc/PlcConnection.cs` (chỉ thêm `ReadIoAsync`)
- Modify: `TotalParking/Services/Plc/PlcHost.cs`
- Modify: `TotalParking/TotalParking.csproj`
- Read: `TotalParking/Services/CanhBaoRepository.cs`, `TotalParking/Services/Plc/CanhBaoPlcService.cs`, `TotalParking/Services/Plc/PlcConnectionManager.cs`

## Acceptance
- AC-01: D3 = 102 ba lần liên tiếp ở khối X → đúng một dòng `canh_bao` đang mở, `ma_loi = PLC-D3-102`, `mo_ta = "Cảnh báo quá tải động cơ"`, `block_no = X`.
- AC-02: D3 102 → 0 ba lần → dòng đó có `het_luc` và `xac_nhan_boi` tự động; D3 102 → 3 ba lần → 102 đóng, `PLC-D3-3` mở. 102/0/102/0 nhấp nháy → không mở/đóng gì.
- AC-03: D3 = 899 ba lần → không có dòng `PLC-D3-899`.
- AC-04: đọc lỗi xen giữa → không đóng/mở; khởi động lại với khoá `PLC_D3:X:102` đang mở và lần đọc đầu của X lỗi → khoá vẫn mở cho tới lần mã ổn định đầu tiên của X.

## Dependencies
- none

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_loi_d3.ps1` (sau khi build bằng MSBuild VS2019)
- Named probe: `kiem_chung_loi_d3.ps1` ca
  - `bang_ma`: đủ 27 mã, 102 → nhãn đúng, 899 → không có;
  - `chuyen_trang_thai`: chuỗi trên `LoiD3Service` + kho bộ nhớ: 0,102,102,102 (mở 1) → lỗi đọc → 102 (không trùng) → 3,3,3 (đóng 102 kèm xác nhận, mở 3) → 899,899,899 (đóng 3, không mở) → 102,0,102,0 (không gì);
  - `khoi_dong`: kho có sẵn khoá mở + lần đọc đầu lỗi → không đóng;
  - `db_loi`: kho ném lỗi khi mở → nhịp ổn định kế tiếp mở lại đúng một dòng;
  - `live`: đọc D3 thật 112 PLC 3 lần, lấy tập (khối, mã có trong bảng) ổn định, so với khoá `PLC_D3:` đang mở trên bản deploy; nếu tập rỗng thì thoát ≠ 0 với `UNVERIFIED: khong quan sat duoc ma nao trong bang`.
- Reachability: source — DLL `TotalParking/bin`; live — bản deploy `C:\Users\Admin\Documents\Web\totalParking` đã chép DLL mới và khởi động lại, MySQL `127.0.0.1` tài khoản ứng dụng.
- Oracle: PASS/FAIL từng ca; thoát 0 chỉ khi mọi ca PASS.
- Counterexample: mở trùng, đóng khi đọc lỗi, mở cho 899, mở khi nhấp nháy, đóng không kèm xác nhận, hoặc treo khoá sau lỗi DB đều làm script FAIL.
- Artifacts: ephemeral — script không ghi file; ca live chỉ đọc.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_loi_d3.ps1
Exit: 0
Base: 84f7eacca4ce20725a4cfed8f3a73594e74ddd50
Head: 1a923a10e669dbb54864bb689a74a1948ec746d3c0330571b31c4a25232d2c0b
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_loi_d3.ps1
PASS  bang_ma              so_ma=27 thieu=[] thua=[] 102='Cảnh báo quá tải động cơ' co_899=False
PASS  chuyen_trang_thai    7/7 buoc dung; nhat_ky: MO PLC_D3:7:102 Cảnh báo quá tải động cơ | DONG PLC_D3:7:102 | MO PLC_D3:7:3 Cảnh báo quá hành trình | DONG PLC_D3:7:3 | MO PLC_D3:2:1 Cảnh báo nút dừng khẩn cấp
PASS  khoi_dong            giu_khi_doc_loi=True khong_mo_trung=True dong_khi_het_loi=True
PASS  db_loi               sau_loi=0 sau_hoi_phuc=1 so_lan_mo=1
PASS  dong_kem_xac_nhan    khoa=PLC_D3:7:102 nguoi_xac_nhan=System
PASS  live                 plc=112 on_dinh=110 co_ma_trong_bang=60 khoa_dang_mo=60 lech=0 da_dong=0 da_dong_chua_xac_nhan=0

TONG KET: 6/6 PASS
```
- Build: VS2019 MSBuild EXIT=0. Deploy: DLL chép lên `C:\Users\Admin\Documents\Web\totalParking\bin` (bản cũ: `TotalParking.dll.truoc-d3-20261013-105055`); sau khi app khởi động lại vẫn 60 khoá mở, không trùng.
- Ca live chạy TRƯỚC deploy cho FAIL (`khoa_dang_mo=0 lech=60`) — probe phân biệt được chưa triển khai.
- Review độc lập: vòng 1 PASS_WITH_WARNINGS (2 Medium, 1 Low đã sửa), vòng 2 PASS.
- Giới hạn: chưa có cảnh báo D3 nào tự đóng trên hệ thống thật (`da_dong=0`); vế đóng-kèm-xác-nhận được chứng minh ở mức source (`dong_kem_xac_nhan`, `chuyen_trang_thai`). Lambda mặc định của `CanhBaoD3Store` không được probe chạy qua (Low).
