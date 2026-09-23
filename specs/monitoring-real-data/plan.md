# Giám sát vận hành — thay dữ liệu giả bằng dữ liệu thật
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-09-23)

- **Existing, dùng lại được**
  - `TotalParking/Views/Home/FloorPlan.cshtml:471,509-530,581-600` — khuôn mẫu
    fetch/render/poll đã chạy thật. Sao chép khuôn này, **trừ một điểm**: nhánh
    `catch` ở `:595-599` chỉ hiện chữ lỗi và giữ nguyên số cũ; hai trang mới phải
    đặt ô số về `—` khi lỗi (xem AC-04).
  - `TotalParking/Controllers/MonitorController.cs` — `RoutingState` trả 6 zone và
    12 xe vào gần nhất (`RecentLimit = 12`).
  - `TotalParking/Controllers/DeviceStatusController.cs` — `Summary` trả sức khoẻ
    camera / PLC / LED / PGS. **Chỉ là dò TCP tới host**, xem Từ vựng hiển thị.
  - `TotalParking/Controllers/PlcStatusController.cs` — `Index` trả **86 block**
    trong `blocks[]`, cộng mảng thứ hai `chua_dua_vao_van_hanh.blocks` chứa các
    PLC sống nhưng `is_active = 0` (không có field `online`, không nằm trong
    `blocks[]`).
  - `TotalParking/Controllers/SlotStatusController.cs` — `Index` trả trạng thái ô
    đỗ. Phản hồi **~121 KB**; chu kỳ quét của chính nó là 45 s.

- **Minimum change**: hai trang `Index.cshtml` và `OperationControl.cshtml` hiện
  có **0 lời gọi fetch**. Thay phần HIỂN THỊ bằng dữ liệu từ endpoint đã có.
  Không thêm endpoint, không thêm service, không đụng tầng điều khiển.

- **Expansion signals**: không có. File bị đụng 2 view + 2 script (< 8);
  service/class mới 0 (< 2); subsystem độc lập 2 (< 3).

- **User decision: KEEP** — hai trang, không đụng alarm.

## Từ vựng hiển thị

Bốn endpoint **không nói cùng một sự thật**. Đo lúc 21:40:

| Khái niệm | Nguồn | Giá trị | Nhãn BẮT BUỘC dùng |
|---|---|---|---|
| Thiết bị trả lời gói tin mạng | `DeviceStatus/Summary` | 82/86 | "thiết bị phản hồi mạng" |
| Block SCADA đọc được thanh ghi | `PlcStatus/Index` `blocks[].online` | 25/86 | "block đọc được dữ liệu" |
| Phiên gửi xe đang mở | `RoutingState.in_use` | 0 | "phiên đang mở" |
| Ô đỗ đang giữ mã thẻ | `SlotStatus.occupied` | 16 | "ô đang có xe" |

Nguyên nhân khác biệt: `DeviceProbeService` chỉ mở TCP tới host; `PlcStatus`
là bắt tay FINS thật (62 block trả `0x00000020`). `in_use` đếm phiên trong
`v_zone_capacity`, `occupied` đếm ô đọc được thẻ từ PLC.

**Cấm dùng chữ "online" trần trên cả hai trang.** Hai trang nằm cạnh nhau trong
cùng nhóm sidebar; dùng chung một từ cho hai nguồn khác nhau là nói dối người trực.

## Out of scope

- **Alarm.** `Index.cshtml:944-1275` (toàn khối, gồm 3 lần
  `localStorage.setItem('activeAlarms', …)` ở `:1075,:1179,:1236`);
  `OperationControl.cshtml:511,1280,1538`. Giữ nguyên từng byte.
- **Hợp đồng `localStorage`.** `activeAlarms`, `palletCycleData`,
  `occBlockStates`, `occPalletOccupancy`, `occAdminOverride`,
  `occActiveRecovery`, `occBypassedSensors` — không đọc, không ghi, **không đổi
  shape**. `CLAUDE.md` mục 2 ghi rõ đây là hợp đồng liên trang.
- **Tầng điều khiển của `OperationControl`** — `btn-action-*`, `btn-mode-*`,
  `btn-plc-send`, `btn-recovery-*`, `chk-bypass-*`. Chỉ đổi phần HIỂN THỊ.
- **Sửa `v_zone_capacity.in_use`** — nó bằng 0 trong khi có 16 ô giữ thẻ. Đây là
  vấn đề dữ liệu riêng, không thuộc đợt này; giải quyết bằng cách gắn nhãn đúng.
- **`Diagnostics.cshtml`, `FloorPlan`, `Routing`, `DriverGuide`** — không sửa.
- **Endpoint mới, bảng mới, migration** — không có.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Dashboard hiển thị sức chứa thật, công thức `Σtotal − Σin_use` | view JS + DOM id | `Index.cshtml` | resolved | elevated — số hiện tại suy biến (`in_use=0`) che được lỗi công thức | live + stub JSON có `in_use > 0` |
| CP-02 | Dashboard hiển thị sức khoẻ thiết bị kèm nhãn đúng ngữ nghĩa | view JS + DOM id | `Index.cshtml` | resolved | elevated | live: so với API và kiểm chuỗi nhãn |
| CP-03 | Dashboard hiển thị xe vào gần nhất thật | view JS + DOM | `Index.cshtml` | resolved | routine | live: so `event_id` |
| CP-04 | `OperationControl` hiển thị block đọc được **và** block chờ kích hoạt | view JS + DOM id | `OperationControl.cshtml` | resolved | elevated | live: so `blocks[]` và `chua_dua_vao_van_hanh` |
| CP-05 | `Index.cshtml` giữ nguyên **nguyên văn** 5 dòng `localStorage.` | static | `Index.cshtml` | resolved | elevated — hợp đồng liên trang | source: so chuỗi từng dòng, không so số đếm |
| CP-06 | `OperationControl.cshtml` giữ nguyên **nguyên văn** 18 dòng `localStorage.` | static | `OperationControl.cshtml` | resolved | elevated | source: so chuỗi từng dòng |
| CP-07 | Khi endpoint lỗi, ô số về `—`, không giữ số cũ | view JS | `Index.cshtml`, `OperationControl.cshtml` | resolved — ép lỗi bằng `page.setRequestInterception`, đã dùng ở `.claude/skills/chrome-devtools/scripts/dg-proof.mjs:18` | elevated | live: chặn URL rồi đọc DOM |
| CP-08 | Bản deploy khớp source trước khi đo | static | `Documents\Web\totalParking\Views\Home\` | resolved | elevated — deploy là bản copy độc lập, không junction | source: so `Get-FileHash` |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | Khi mở Dashboard, hệ thống sẽ hiển thị `Σtotal`, `Σin_use`, `Σtotal − Σin_use` và `round(Σin_use/Σtotal×100)%`; khi `Σtotal = 0` sẽ hiển thị `—` thay vì `NaN`. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"` |
| AC-02 | Khi mở Dashboard, hệ thống sẽ hiển thị sức khoẻ 4 nhóm thiết bị khớp `DeviceStatus/Summary`. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"` |
| AC-03 | Khi mở Dashboard, hệ thống sẽ hiển thị xe vào gần nhất khớp `RoutingState.recent`. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"` |
| AC-04 | Khi `Monitor/RoutingState` bị chặn, hệ thống sẽ đặt các ô KPI về đúng ký tự `—` và hiện thông báo lỗi. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"` |
| AC-05 | Khi mở Điều khiển vận hành, hệ thống sẽ hiển thị `<online>/<blocks.length>` từ `blocks[]` và một dòng riêng cho `chua_dua_vao_van_hanh.so_luong`. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"` |
| AC-06 | Khi sửa `OperationControl.cshtml`, 18 dòng chứa `localStorage.` sẽ giống hệt nguyên văn trước khi sửa. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"` |
| AC-07 | Khi sửa `Index.cshtml`, 5 dòng chứa `localStorage.` sẽ giống hệt nguyên văn trước khi sửa. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"` |
| AC-08 | Khi hiển thị số thiết bị, mỗi trang sẽ dùng đúng nhãn ở mục Từ vựng hiển thị và không chứa chữ "online" đứng một mình. | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"`, `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"` |
| AC-09 | Trước khi đo, hệ thống sẽ so hash view giữa source và bản deploy; lệch thì thoát khác 0 và in "chưa deploy". | `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-dashboard.mjs"`, `& "C:\Program Files
odejs
ode.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"` |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Dashboard Tổng quan dùng dữ liệu thật | AC-01, AC-02, AC-03, AC-04, AC-07, AC-08, AC-09 | `TotalParking/Views/Home/Index.cshtml` | - | done |
| 02 | Điều khiển vận hành hiển thị trạng thái block thật | AC-05, AC-06, AC-08, AC-09 | `TotalParking/Views/Home/OperationControl.cshtml` | - | done |

## Review log

- Round 1 (2026-09-23): hai reviewer độc lập, 10 finding thô → khử trùng còn 6.
  Người dùng **nhận cả 6** tại C2.
  - F1 Critical — AC-01 cộng `free_mech + free_tier0 + free_ground` trừ `InUse`
    ba lần (`VehicleRouting.cs:61-65`); `Total` không gồm `TotalTier0`. Đã đổi
    sang `Σtotal − Σin_use` kèm guard chia 0 và case stub JSON.
  - F2 Critical — `DeviceStatus/Summary` (82/86) và `PlcStatus/Index` (25/86) là
    hai ngữ nghĩa khác nhau; `in_use=0` vs `occupied=16`. Đã thêm mục Từ vựng
    hiển thị và AC-08.
  - F3 High — không task nào sở hữu bước deploy; verification đo bản chưa sửa.
    Đã thêm `Deploy:` vào Ownership và AC-09 so hash.
  - F4 High — AC-04 không có đường tới nhánh lỗi, và khuôn `FloorPlan:595-599`
    giữ nguyên số cũ khi lỗi. Đã chốt `setRequestInterception` và oracle `—`.
  - F5 High — vùng Out `944-1044` cắt hụt ba `setItem` ở `:1075,:1179,:1236`;
    AC đếm số lần nên đổi shape vẫn lọt. Đã mở rộng thành `944-1275` và đổi
    sang so nguyên văn từng dòng.
  - F6 Medium — "85 block" sai (thực tế 86) và bỏ sót `chua_dua_vao_van_hanh`.
    Đã sửa và bổ sung vào AC-05.
  - Sweep: CP-01…08 đều có chủ; AC-01…09 đều map task và proof.
