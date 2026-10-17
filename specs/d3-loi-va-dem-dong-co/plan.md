# Lỗi D3 và đếm lượt chạy động cơ
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-10-13)
- Existing:
  - `canh_bao` mở/đóng sự cố bằng `xay_ra_luc`/`het_luc` + `khoa_chong_trung` — `TotalParking/Database/45_canh_bao.sql:1`, ghi qua `CanhBaoRepository.Ghi` — `TotalParking/Services/CanhBaoRepository.cs:95`.
  - Mẫu dịch vụ tự mở/đóng cảnh báo theo PLC — `TotalParking/Services/Plc/CanhBaoPlcService.cs:34`, đóng theo tiền tố khoá `:192`; khởi động trong `TotalParking/Services/Plc/PlcHost.cs:94`.
  - Trang Cảnh báo đọc `canh_bao` qua `/Alarm/List` — `TotalParking/Views/Home/Alarms.cshtml:136`, `TotalParking/Controllers/AlarmController.cs:28`.
  - FINS/UDP đọc DM và CIO — `TotalParking/Services/Plc/OmronFinsUdpClient.cs:93`; đã đọc thật D3 trên 111/112 PLC và CIO 0.09 khối 1 (12/10/2026).
  - Danh sách kết nối PLC + giới hạn 16 PLC song song — `TotalParking/Services/Plc/PlcConnectionManager.cs:28`.
  - Loại khối 3/5/6/10 ô = `block.slot_count` (DB 13/10: 9 + 41 + 22 + 40 = 112 khối cơ khí).
  - Trang Bảo trì đang hiện số liệu động cơ bịa bằng `Math.random()` — `TotalParking/Views/Home/Maintenance.cshtml:449`.
- Minimum change: đọc D3 + CIO 100..102 của 112 PLC theo nhịp; map D3 → nhãn từ `docs/error.xls` và mở/đóng cảnh báo trong `canh_bao`; nhận lượt chạy động cơ theo quy tắc của `docs/NOTE FINAL.xlsx` và lưu vào bảng mới `motor_run`; hiển thị số lượt chạy và lịch sử trên trang Bảo trì.
- Expansion signals: khoảng 10 file; 3 lớp mới (poller, dịch vụ lỗi D3, bộ nhận lượt chạy) + 1 repository + 1 controller; 1 bảng mới cần tài khoản quản trị MySQL chạy DDL (user ứng dụng không có quyền CREATE).
- User decision: KEEP — cả ba phần trong một packet.
- Quyết định dữ liệu tại C1:
  - BLOCK 10 trong NOTE FINAL: lấy CỘT C làm chuẩn (cột B lệch từ dòng 9).
  - Nam châm điện được đếm như một động cơ.
  - Mã D3 không có trong `error.xls` (đã thấy 108, 899): BỎ QUA, không sinh cảnh báo.
  - BLOCK 6: khách cập nhật NOTE FINAL ngày 13/10/2026 09:40 — nguồn `Q101.06`, DCO 212 giữ `Q101.03`, bỏ DCO 213. Ô tô màu trong file = động cơ được đếm.
  - Nghĩa D3 (C2/F3): làm theo `error.xls` — D3 là một mã nhị phân đơn, PLC tự về 0 khi hết lỗi. Người dùng xác nhận 13/10/2026: D3 = 1 đúng là đang dừng khẩn cấp (58/112 khối ngày 12/10, nên deploy xong mở khoảng 58 cảnh báo mã 1 là đúng thực tế); mã ngoài danh sách (108, 899, …) không cần quan tâm.
  - Chống rung D3 (C2/F5): 3 lần đọc liên tiếp.

## Scope decision (C1 bổ sung — 2026-10-16, task 03)
- Người dùng yêu cầu trang Bảo trì có bố cục như trang Điều khiển vận hành (`TotalParking/Views/Home/OperationControl.cshtml:89-230`): bấm khối nào thì hiện thông tin bảo trì và lỗi của khối đó.
- User decision: làm lại TOÀN trang Bảo trì, bỏ hết nội dung bịa; bảng bên phải gồm lỗi đang có, lịch sử lỗi, động cơ. Phần so khớp số lượt chạy với DB tách thành task 04 (cần dữ liệu thật của task 02), để task 03 không phải chờ task 02.
- Ngoài packet (làm trực tiếp cùng ngày): xoá trang CCTV và Giám sát điện năng (view, action, csproj, sidebar).

## Out of scope
- Ngưỡng bảo trì theo số lượt chạy, cảnh báo vượt ngưỡng.
- Ghi bất cứ thứ gì xuống PLC. Mọi đọc là chỉ đọc.
- Xoá/gom lịch sử theo thời gian (giữ vĩnh viễn).
- Đọc bộ đếm do PLC tự đếm (không tồn tại).

## Coverage profile
| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Lỗi D3 của từng khối hiện đúng nhãn trên trang Cảnh báo khi đang có, tự đóng (kèm tự xác nhận) khi PLC về 0, và còn trong lịch sử | add, integrate, modify | PLC đọc DM3 qua `PlcConnection` (chung gate với vòng quẹt thẻ); `canh_bao`; `/Alarm/List`; vòng poll mới | none — mã lạ bỏ qua (C1); nghĩa D3 theo file (C2/F3) | elevated — tích hợp PLC + ghi DB vận hành + chạm đường quẹt thẻ | source (kịch bản chuyển trạng thái trên DLL) + live (D3 thật ↔ khoá đang mở) |
| CP-02 | Mỗi lượt chạy động cơ quan sát được ghi một dòng có giờ bắt đầu, kết thúc, thời lượng | add, integrate | PLC đọc CIO 100..102; bảng mới `motor_run` (DDL quản trị); vòng poll mới | none — BLOCK 6 đã chốt bằng file 13/10 | elevated — tích hợp PLC + bảng mới | source (kịch bản bit trên DLL) + live (theo dõi CIO thật + ≥ 1 dòng `motor_run` thật) |
| CP-03 | Trang Bảo trì theo bản đồ khối: bấm khối hiện lỗi đang có, lịch sử lỗi, số lượt chạy và lượt chạy gần nhất của khối | add, modify, remove | API JSON mới `/BaoTri/*`; `Maintenance.cshtml` viết lại; đọc `canh_bao`, `motor_run` | none — C1 bổ sung 16/10 | routine — chỉ đọc | source (HTTP API ↔ DB) + live (user bấm khối trên trang) |

## Acceptance criteria
| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When D3 của một khối đọc được bằng cùng một mã có trong `error.xls` 3 lần liên tiếp, the system shall có đúng một cảnh báo đang mở `ma_loi = PLC-D3-<mã>` cho khối đó với `mo_ta` = nhãn tiếng Việt của mã. | task 01 |
| AC-02 | When D3 của khối đó ổn định 3 lần ở 0 hoặc ở mã khác, the system shall đặt `het_luc` kèm tự xác nhận cho cảnh báo cũ (và mở cảnh báo mới nếu mã mới có trong bảng). | task 01 |
| AC-03 | If D3 là mã không có trong bảng, the system shall không mở cảnh báo nào cho mã đó. | task 01 |
| AC-04 | If đọc D3 của một PLC thất bại, the system shall giữ nguyên cảnh báo D3 đang mở của khối đó. | task 01 |
| AC-05 | When bit động cơ ON trong lúc nguồn ON và (thuận ON hoặc nghịch ON), the system shall bắt đầu một lượt chạy; when bit động cơ chuyển OFF, the system shall ghi một dòng `motor_run` có giờ bắt đầu, kết thúc, thời lượng. | task 02 |
| AC-06 | While bit động cơ ON mà điều kiện nguồn + thuận/nghịch chưa thoả, the system shall không tính là đang chạy. | task 02 |
| AC-07 | When người dùng bấm một khối trên trang Bảo trì, the system shall hiện lỗi đang có, lịch sử lỗi và động cơ của đúng khối đó; thẻ khối đang có cảnh báo mở hiện viền đỏ và số lỗi. | task 03 |
| AC-08 | The `/BaoTri/Loi?block=X` shall trả các cảnh báo đang mở của khối X khớp `canh_bao` và tối đa `limit` cảnh báo gần nhất, mới nhất trước. | task 03 |
| AC-09 | If bảng `motor_run` chưa tồn tại, the `/BaoTri/DongCo` shall trả 200 với `co_du_lieu=false` và đủ động cơ của loại khối với số 0. | task 03 |
| AC-11 | The trang Bảo trì shall hiện thời gian hoạt động của từng động cơ và từng loại = số lần × 20 s (trượt) / 24 s (nâng) / 12 s (hàng rào); nam châm chỉ hiện số lần. | task 05 |
| AC-10 | When `motor_run` có lượt chạy của khối X, the trang Bảo trì shall hiện `tong` và `hom_nay` từng động cơ bằng số dòng tương ứng trong DB, cùng tối đa 50 lượt gần nhất. | task 04 |

## Tasks
| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Lỗi D3 hiện và tự đóng trên trang Cảnh báo | AC-01..AC-04 | `Services/Plc/PlcIoPoller.cs`, `Services/Plc/LoiD3Service.cs`, `Services/Plc/PlcConnection.cs` | - | done |
| 02 | Ghi lượt chạy động cơ vào `motor_run` | AC-05, AC-06 | `Services/Plc/DongCoDetector.cs`, `Services/MotorRunRepository.cs`, `Database/62_motor_run.sql` | task-01-loi-d3.md | done |
| 03 | Trang Bảo trì theo bản đồ khối: lỗi và động cơ của từng khối | AC-07..AC-09 | `Controllers/BaoTriController.cs`, `Views/Home/Maintenance.cshtml` | task-01-loi-d3.md | done |
| 04 | Số lượt chạy trên trang Bảo trì khớp `motor_run` | AC-10 | `tools/kiem_chung_bao_tri.ps1` | task-02-dem-dong-co.md, task-03-hien-thi-dong-co.md | pending |
| 05 | Thời gian hoạt động theo loại động cơ trên trang Bảo trì | AC-11 | `Controllers/BaoTriController.cs`, `Views/Home/Maintenance.cshtml` | task-03-hien-thi-dong-co.md | done |

## Review log
- Round 1 (13/10/2026): 3 reviewer độc lập (Fact Checker; Failure-mode; Assumption/Proof) → 15 phát hiện sau gộp.
  - Chấp nhận: F1 đường đọc CIO + `PlcConnection.cs` vào task 01; F2 `ReadIoAsync` dùng `WaitAsync(0)`, timeout riêng, không `Fail()`; F4 đóng kèm `NguoiXacNhanTuDong`; F6 hoà giải theo DB từng khối; F7 `Stop()` trong `PlcHost.Stop`; F8 phải thấy OFF trước khi bắt đầu lượt + bỏ lượt khi trống > 3 nhịp; F9 vòng chạy-hết-rồi-chờ + semaphore 16 riêng; F10 thời lượng bằng Stopwatch; F11 nạp `slot_count` từ DB, khối lạ bỏ qua; F12 probe `live_cio`; F13 proof live có thể FAIL (`NO_DATA`, `UNVERIFIED`, mốc chụp); F14 interface kho cho probe; F15 live trả `UNVERIFIED` khi không thấy mã nào.
  - F3: làm theo file, xác nhận nghĩa D3 với bên PLC sau. F5: chống rung 3 lần.
  - Ghi chú: một reviewer chỉ trích được 26 mã từ `error.xls` (sót mã 1); bản đọc qua Excel COM có đủ 27.
  - Sweep: 4 file đọc lại / delta: BLOCK 6, F1–F15 / sửa: ownership `PlcConnection.cs`, bảng bit BLOCK 6, trạng thái task 02, proof task 02–03 / mâu thuẫn còn lại: 0.
- Round 2 (13/10/2026, closure pass, reviewer mới):

  | ID | Decision | Original counterexample | Repaired at | Proved at | Replay | Closure |
  |---|---|---|---|---|---|---|
  | F1, F2, F4–F11, F13–F15 | accept | xem Round 1 | task-01 Scope, task-02 Scope/Verification, task-03 Verification | báo cáo closure 13/10 | chạy lại trên text đã sửa | PASS |
  | F12 | accept | bảng bit chưa kiểm phần cứng; `live_cio` PASS rỗng | task-02 Verification (`live_cio` → `UNVERIFIED` khi không thấy bit động cơ) | báo cáo closure 13/10 | đúng mã động cơ chỉ kiểm được tại hiện trường | PASS phần đọc bit; mã động cơ → C3 |
  | N1 (mới, High) | accept | PLC chết → kết nối lại ~3 s kéo dài cả nhịp → lượt chạy khối khác bị bỏ do "trống > 3 × nhịp" | task-01 Scope (`ReadIoAsync` không kết nối lại), task-02 Scope (đếm theo nhịp thất bại của chính khối) | — | chưa replay (giới hạn 2 vòng giấy; kiểm bằng ca `quy_tac` "nhịp dài") | chờ bằng chứng chạy |
  | N2 (Low) | accept | ghi "Hệ thống" ≠ hằng số `"System"` | task-01 Outcome | — | — | sửa chữ |

  - Sweep: 4 file đọc lại / delta: N1, N2, F12 / sửa: task-01 Outcome + Scope, task-02 Scope + Acceptance + Verification / mâu thuẫn còn lại: 0.
  - Đã hết 2 vòng review giấy. Phát hiện tiếp theo phải có bằng chứng chạy thật.
- Runtime (13/10/2026, task 02): đọc CIO thật 110/112 khối — RAO_CHAN bật ở 42 khối đứng yên, mọi động cơ khác tắt; nguồn và chiều không cùng bật ở khối nào trong 30 s. Người dùng chốt: RAO_CHAN bật lúc đứng yên là trạng thái giữ, giữ quy tắc NOTE FINAL. Probe `live_cio` sửa theo quyết định này (RAO_CHAN đứng yên không vi phạm, không tính là hoạt động); mọi động cơ khác giữ điều kiện cũ.
- Runtime (16/10/2026, task 01): 409 cảnh báo `PLC-D3-%` đã tự đóng trên hệ thống thật, cả 409 có `xac_nhan_boi = 'System'` — đóng giới hạn "chưa có cảnh báo D3 nào tự đóng" ghi trong Receipt task 01. Mã gặp nhiều nhất: 17 (330 lần, trung bình ~3 phút/lần).
- C1 bổ sung (16/10/2026): task 03 viết lại theo bố cục bản đồ khối, phụ thuộc đổi từ task 02 sang task 01; phần so khớp số lượt chạy tách thành task 04 (phụ thuộc 02 và 03). Sweep: plan + 4 task đọc lại; AC-07 viết lại, thêm AC-08..AC-10; CP-03 viết lại; bỏ dòng loại trừ "gỡ bảng chẩn đoán bịa"; mâu thuẫn còn lại: 0.
- C1 bổ sung (16/10/2026, task 05): người dùng chốt thời gian hoạt động = số lần × thời gian chuẩn (trượt 20 s, nâng 24 s, hàng rào 12 s); quy tắc nhận lượt chạy giữ nguyên (0→1 khi có nguồn + chiều, rồi 1→0); nam châm vẫn đếm số lần, không tính thời gian. Runtime cùng ngày: 5 lượt chạy thật đầu tiên trong `motor_run` — trượt 102/103 đo 18,2 s, nâng 201 đo 25,2 s, gần thời gian chuẩn; RAO_CHAN đo 32,3 s. Lưu ý ownership: task 04 và task 05 cùng sửa `tools/kiem_chung_bao_tri.ps1` — chạy tuần tự, không song song.
