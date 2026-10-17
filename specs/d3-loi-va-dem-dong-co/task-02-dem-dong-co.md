# Task 02 — Ghi lượt chạy động cơ vào motor_run

Status: done

## Blocker
- 13/10/2026: code đã build (EXIT 0), `bang_bit` PASS, `quy_tac` 8/8 PASS; chưa deploy.
- ĐÃ GỠ 16/10/2026: `motor_run` tồn tại (người dùng chạy `62_motor_run.sql` bằng root); code đã deploy cùng task 03 lúc 14:34.
- ĐÃ GỠ 16/10/2026 16:04: bãi có động cơ chạy — `motor_run` có 15 dòng trên 2 khối, lượt gần nhất kết thúc 16:04:28 (khối 86 RAO_CHAN).

## Outcome
Mỗi lượt chạy động cơ QUAN SÁT ĐƯỢC (độ phân giải bằng nhịp đọc `plc:ioPollMs`) có đúng một dòng `motor_run` ghi khối, động cơ, giờ bắt đầu, giờ kết thúc và thời lượng.

## Scope
- In:
  - `DongCoDetector`: bảng bit theo loại khối, chép từ `docs/NOTE FINAL.xlsx` bản 13/10/2026 09:40 (ô tô màu = động cơ được đếm). `Qwww.bb` = CIO word `www`, bit `bb`. Mọi loại khối: thuận `Q100.02`, nghịch `Q100.03`.

    | Loại | Nguồn | Động cơ (bit → mã) |
    |---|---|---|
    | 3 | `Q101.01` | `Q100.04` 201, `Q100.05` 202, `Q100.06` 102, `Q100.07` RAO_CHAN, `Q101.00` NAM_CHAM |
    | 5 | `Q101.03` | `Q100.04` 201, `Q100.05` 202, `Q100.06` 203, `Q100.07` 102, `Q101.00` 103, `Q101.01` RAO_CHAN, `Q101.02` NAM_CHAM |
    | 6 | `Q101.06` | `Q100.04` 201, `Q100.05` 202, `Q100.06` 102, `Q100.07` RAO_CHAN, `Q101.02` 211, `Q101.03` 212, `Q101.04` 112, `Q101.05` NAM_CHAM |
    | 10 (cột C) | `Q102.02` | `Q100.04` 201, `Q100.05` 202, `Q100.06` 203, `Q100.07` 102, `Q101.00` 103, `Q101.01` RAO_CHAN, `Q101.04` 211, `Q101.05` 212, `Q101.06` 213, `Q101.07` 112, `Q102.00` 113, `Q102.01` NAM_CHAM |

  - Loại khối: nạp `block_no → slot_count` (khối `kind='Mechanical'`) từ DB khi poller khởi động. `slot_count` ngoài {3, 5, 6, 10} → bỏ qua khối, ghi `PlcAuditLog` một lần. Khối 5 ô loại 4800L dùng bảng loại 5.
  - Quy tắc (ghi chú NOTE FINAL): động cơ bắt đầu ĐANG CHẠY khi bit của nó ON và nguồn ON và (thuận ON hoặc nghịch ON); KẾT THÚC khi bit của nó OFF.
  - Chống đếm sai:
    - sau khi poller khởi động, mỗi động cơ phải được thấy OFF ít nhất một lần thì mới được bắt đầu lượt (lượt đang dở lúc restart / recycle không được ghi);
    - nếu CHÍNH khối đó không đọc thành công quá 3 nhịp poller liên tiếp (lỗi đọc hoặc bỏ nhịp) trong khi động cơ đang chạy → bỏ lượt đó (ghi `PlcAuditLog`), động cơ lại phải thấy OFF trước khi bắt đầu lượt mới. Đếm theo nhịp của khối, không theo giây, để nhịp kéo dài vì PLC khác chậm không làm mất lượt.
  - Thời điểm: `started_at`/`ended_at` = `DateTime.Now` lúc lần đọc thành công thấy chuyển trạng thái; `duration_ms` đo bằng `Stopwatch` giữa hai lần đọc đó, kẹp ≥ 0.
  - Dữ liệu CIO lấy từ `PlcConnection.ReadIoAsync` (task 01), poller đưa cho `DongCoDetector` trong cùng nhịp.
  - `62_motor_run.sql` (DDL, tài khoản quản trị MySQL chạy): `motor_run(run_id BIGINT UNSIGNED AUTO_INCREMENT PK, block_no SMALLINT UNSIGNED NOT NULL, motor_code VARCHAR(16) NOT NULL, started_at DATETIME(3) NOT NULL, ended_at DATETIME(3) NOT NULL, duration_ms INT UNSIGNED NOT NULL)`, chỉ mục `(block_no, started_at)`. Không xoá dòng.
  - Ghi qua interface `IMotorRunStore`; bản thật `MotorRunRepository.Ghi(...)`. Thêm `<Compile Include>`.
- Out: hiển thị (task 03); bộ đếm do PLC tự đếm.

## Coverage
- CP-02

## Ownership
- Create: `TotalParking/Services/Plc/DongCoDetector.cs`
- Create: `TotalParking/Services/MotorRunRepository.cs`
- Create: `TotalParking/Database/62_motor_run.sql`
- Create: `tools/kiem_chung_dong_co.ps1`
- Modify: `TotalParking/Services/Plc/PlcIoPoller.cs`, `TotalParking/TotalParking.csproj`

## Acceptance
- AC-05: `[201 OFF]` → `[201 ON, nguồn ON, thuận ON]` tại t0 → `[201 OFF]` tại t1 → một dòng khối X, `motor_code='201'`, `started_at=t0`, `ended_at=t1`, `duration_ms≈t1−t0`.
- AC-06: `[201 ON, nguồn OFF]` → `[201 OFF]` → không dòng nào; `[201 OFF]` → `[201 ON, nguồn OFF]` → `[201 ON, nguồn ON, nghịch ON]` tại t2 → lượt bắt đầu tại t2.
- Lần đọc đầu sau khởi động đã thấy 201 ON → không ghi lượt đó. Khối đọc thất bại > 3 nhịp liên tiếp giữa lượt → không ghi lượt đó. Nhịp kéo dài (ví dụ 3 giây vì PLC khác chậm) nhưng khối vẫn đọc thành công mỗi nhịp → lượt được giữ.
- Task chỉ được `done` khi có ít nhất một dòng `motor_run` từ một lượt chạy thật trên bản deploy.

## Dependencies
- task-01-loi-d3.md

## Verification Plan
- Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_dong_co.ps1`
- Named probe: `kiem_chung_dong_co.ps1` ca
  - `bang_bit`: 4 loại khối khớp bảng trên (đếm bit và mã);
  - `quy_tac`: các chuỗi AC-05/AC-06, khởi động giữa lượt, khối đọc thất bại > 3 nhịp, nhịp dài 3 giây nhưng khối vẫn đọc được (lượt phải được giữ), trên `DongCoDetector` + kho bộ nhớ;
  - `live_cio`: theo dõi CIO 100..102 thật của mọi khối trong `-GiayTheoDoi` (mặc định 300) giây; FAIL nếu một bit trong 100.02..102.07 bật mà không có trong bảng của loại khối đó, hoặc một bit động cơ KHÁC RAO_CHAN bật trong khi chưa từng thấy nguồn + chiều ở bất kỳ lần đọc nào của khối. RAO_CHAN bật lúc khối đứng yên là trạng thái giữ hợp lệ (người dùng chốt 13/10/2026 sau khi đọc thật thấy RAO_CHAN bật ở 42/110 khối đứng yên, mọi động cơ khác tắt) và không được tính là bằng chứng động cơ hoạt động; thoát ≠ 0 với `UNVERIFIED` nếu cả cửa sổ không thấy bit động cơ nào bật; in số khối đọc được. Đúng mã động cơ (201 hay 202…) chỉ xác nhận được tại hiện trường — đưa vào C3;
  - `live_bang`: `motor_run` có ≥ 1 dòng; không có thì thoát ≠ 0 với `NO_DATA`.
- Reachability: source — DLL `TotalParking/bin`; live — CIO đọc từ máy chủ qua `OmronFinsUdpClient`; `motor_run` tồn tại sau khi quản trị viên chạy `62_motor_run.sql`; lượt chạy thật cần thiết bị vận hành ngoài hiện trường trong cửa sổ theo dõi.
- Oracle: PASS/FAIL từng ca; thoát 0 chỉ khi mọi ca PASS.
- Counterexample: đếm khi thiếu nguồn, ghi lượt bị cắt đầu sau restart, ghi thời lượng phồng qua khoảng mất đọc, ghi hai dòng cho một lượt, hay bảng bit lệch file đều làm script FAIL.
- Artifacts: ephemeral.

## Receipt

Verification: PASS
Command: powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_dong_co.ps1
Exit: 0
Base: 84f7eacca4ce20725a4cfed8f3a73594e74ddd50
Head: 740a1dd2446b8d595fdb2f2fc893cf6081a4e6eee29c2266499b2c7d48e2cb1b
```text
$ powershell -NoProfile -ExecutionPolicy Bypass -File tools\kiem_chung_dong_co.ps1
PASS  bang_bit     loai=[3,5,6,10] dong_co=5/7/8/12
PASS  quy_tac      8/8 chuoi dung
PASS  live_cio     khoi_doc_duoc=112 luot_doc=53088 dong_co_tung_bat=5 khoi_tung_du_dk=1 bit_la=0 dc_bat_khong_dk=0
PASS  live_bang    so_dong=37 moi_nhat: 86 NAM_CHAM 2026-10-16 16:17:36.071 2026-10-16 16:18:00.262 24185 | 86 201 2026-10-16 16:17:36.071 2026-10-16 16:18:00.262 24185 | 86 102 2026-10-16 16:17:15.914 2026-10-16 16:17:33.044 17124

TONG KET: 4/4 PASS
```
- Chạy trên bản deploy (DLL deploy lại 16/10/2026 16:12, bản cũ đuôi `.truoc-dongco-20261016-161245`) trong lúc bãi đang vận hành; cửa sổ theo dõi 300 s.
- Đối chứng âm trước đó: trước khi có bảng `live_bang` báo `ERROR 1146`; lúc bãi đứng yên `live_cio` 60 s trả `UNVERIFIED`.
- Số đo thật khớp thời gian chuẩn người dùng đưa: nâng 201 ≈ 24,2 s, trượt 102 ≈ 17,1–18,2 s.
- Review độc lập: vòng 1 PASS_WITH_WARNINGS (Medium: trạng thái không theo Reload danh sách PLC — đã sửa bằng nạp lại bản đồ + dựng lại bộ nhận khi tập khối đổi; Low: bản đồ rỗng — đã sửa), vòng 2 PASS.
- Giới hạn: mốc giờ dùng chung cho cả nhịp (sai số ≤ một nhịp); INSERT chạy đồng bộ trong vòng poll; Reload danh sách PLC bỏ các lượt đang dở toàn bãi; đổi `slot_count` cần restart; không ghi dòng "hồi phục" sau khi DB trở lại. Ý nghĩa "một lượt" của RAO_CHAN (đang giữ khi đứng yên) và đúng mã động cơ cần xác nhận tại hiện trường (C3).
