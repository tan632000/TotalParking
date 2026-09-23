# Task 02 — Điều khiển vận hành hiển thị trạng thái block thật

Status: done

## Outcome

Trang `/Home/OperationControl` hiển thị danh sách block, số block đọc được dữ
liệu, số block chờ kích hoạt, và ô đỗ đang giữ thẻ — lấy từ `PlcStatus/Index` và
`SlotStatus/Index`. Nút bấm và luồng điều khiển giữ nguyên hành vi hiện tại.

## Scope

- **In**
  - Khối fetch/render/poll theo khuôn `FloorPlan.cshtml:471,581-600`.
  - `PlcStatus/Index` ở nhịp 2 s; **`SlotStatus/Index` ở nhịp riêng ≥ 15 s** —
    phản hồi của nó ~121 KB và chu kỳ quét ô đỗ của chính nó là 45 s, nên poll
    nhanh hơn là lãng phí thuần.
  - Hiển thị `<online>/<blocks.length>` từ `blocks[]`, nhãn **"block đọc được
    dữ liệu"**.
  - Hiển thị riêng một dòng `chua_dua_vao_van_hanh.so_luong` — các PLC sống
    nhưng `is_active = 0`. Chúng **không** nằm trong `blocks[]`; bỏ qua sẽ khiến
    kỹ thuật viên vừa đấu nối một block, không thấy nó, và kết luận nhầm là hỏng.
  - Ô đỗ đang giữ thẻ từ `SlotStatus/Index`, nhãn **"ô đang có xe"**.
  - **Khác khuôn FloorPlan**: khi lỗi phải đặt các ô số về `—`, không giữ số cũ.
- **Out**
  - **Toàn bộ tầng điều khiển.** Không sửa hành vi của `btn-action-*`,
    `btn-mode-*`, `btn-plc-send`, `btn-recovery-*`, `chk-bypass-*`,
    `chk-simulate-stuck`. Không thêm đường ghi xuống PLC.
  - Alarm: `OperationControl.cshtml:511`, `:1280`, `:1538`. Không sửa.
  - 18 dòng `localStorage.` (`occBlockStates`, `occPalletOccupancy`,
    `occAdminOverride`, `occActiveRecovery`, `occBypassedSensors`,
    `activeAlarms`, `palletCycleData`) — không đọc, không ghi, **không đổi shape**.
  - `storingSteps` / `retrievingSteps` (`:1359`, `:1371`) — kịch bản mô phỏng,
    giữ nguyên.

## Coverage

- CP-04, CP-06, CP-07, CP-08

## Ownership

- Modify: `TotalParking/Views/Home/OperationControl.cshtml`
- Create: `specs/monitoring-real-data/verify-opcontrol.mjs`
- Deploy: sao chép `OperationControl.cshtml` đã sửa sang
  `C:\Users\Admin\Documents\Web\totalParking\Views\Home\` — bản deploy là **bản
  copy độc lập**, không phải junction hay hardlink.
- Read: `TotalParking/Views/Home/FloorPlan.cshtml`,
  `TotalParking/Controllers/PlcStatusController.cs`,
  `TotalParking/Controllers/SlotStatusController.cs`

## Acceptance

- AC-05: Trang hiển thị `<online>/<blocks.length>` khớp số phần tử có
  `online === true` trong `blocks[]`, **và** một dòng riêng bằng đúng
  `chua_dua_vao_van_hanh.so_luong`.
- AC-06: 18 dòng chứa `localStorage.` trong `OperationControl.cshtml` giống
  **hệt nguyên văn** bản trước khi sửa.
- AC-08: Nhãn dùng đúng từ vựng ở `plan.md`; trang không chứa chữ "online" đứng
  một mình.
- AC-09: Hash `OperationControl.cshtml` ở source và ở bản deploy giống nhau
  trước khi đo.

## Dependencies

- none

## Verification Plan

- Command: `& "C:\Program Files\nodejs\node.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"`
  — `node` không nằm trên PATH của shell này; ghi đường dẫn đầy đủ để lệnh
  chạy lại được đúng như đã chạy.
- Named probe: `verify-opcontrol.mjs` — sáu kiểm tra có tên `deploy_khop_source`,
  `block_khop_plcstatus`, `cho_kich_hoat_hien_ra`, `localstorage_nguyen_van`,
  `nut_dieu_khien_con_nguyen`, `nhan_dung_tu_vung`.
- Reachability:
  - Site: đã biết — `curl -s http://localhost:8080/PlcStatus/Index` trả JSON có
    `poll_running`. Gửi request đánh thức với timeout ≥ 10 s trước khi đo.
  - Nhánh lỗi: đã biết — `page.setRequestInterception(true)` rồi `request.abort()`,
    kỹ thuật đã dùng tại `.claude/skills/chrome-devtools/scripts/dg-proof.mjs:18`.
  - Danh sách nút: đã biết — đếm `id="btn-action-*"` trong file trước khi sửa làm
    mốc.
- Oracle: script in `PASS`/`FAIL` cho từng kiểm tra, thoát mã 0 chỉ khi cả sáu
  `PASS`. `deploy_khop_source` thất bại thì in "chưa deploy" và **dừng ngay**.
- Counterexample:
  - Thêm một lời gọi `localStorage.setItem` mới, hoặc đổi tên field trong dữ
    liệu đã ghi → `localstorage_nguyen_van` FAIL. So **nguyên văn từng dòng**,
    không so số đếm — vì đếm không đổi mà shape đổi vẫn phá `Diagnostics` và
    `Index`.
  - Xoá nhầm một nút `btn-action-*` → `nut_dieu_khien_con_nguyen` FAIL.
  - Chỉ hiện `blocks[]` mà bỏ `chua_dua_vao_van_hanh` → `cho_kich_hoat_hien_ra`
    FAIL.
  - Dùng chữ "online" trần → `nhan_dung_tu_vung` FAIL.
- Artifacts: `specs/monitoring-real-data/artifacts/opcontrol.png`, ghi đè mỗi
  lần chạy, không so digest.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\monitoring-real-data\verify-opcontrol.mjs"`
Exit: 0
Base: 8b9054d5928f712759cb69a5af2e21cf8c7334d6
Head: 8b9054d5928f712759cb69a5af2e21cf8c7334d6
Deploy: OperationControl.cshtml sha256 28e65d48eebf... (source == deploy)

```text
  PASS  deploy_khop_source  | 28e65d48eebf
  PASS  localstorage_nguyen_van  | 18/18 dong
  PASS  nut_dieu_khien_con_nguyen  | du 17 nut
  PASS  block_khop_plcstatus  | trang "26/86" vs api "26/86"
  PASS  cho_kich_hoat_hien_ra  | trang "26" vs api "26"
  PASS  nhan_dung_tu_vung  | khong co

6/6 PASS
EXIT: 0
```

### Anh xa AC -> kiem tra

| AC | Kiem tra | Ket qua |
|---|---|---|
| AC-05 | `block_khop_plcstatus`, `cho_kich_hoat_hien_ra` | PASS |
| AC-06 | `localstorage_nguyen_van` | PASS |
| AC-08 | `nhan_dung_tu_vung` | PASS |
| AC-09 | `deploy_khop_source` | PASS |
| (ngoai AC) | `nut_dieu_khien_con_nguyen` | PASS — 17 nut dieu khien con du |

### Gioi han cua bang chung nay

- **Chi them mot thanh so lieu that.** Luoi block ben duoi VAN la mo phong
  (dung `occBlockStates` trong localStorage) va giu nhan "Mo phong thoi gian
  thuc" cua no. Trang gio co hai loai du lieu canh nhau, phan biet bang nhan.
- **`nut_dieu_khien_con_nguyen` chi kiem SU TON TAI cua 17 `id`**, khong kiem
  hanh vi khi bam. Tang dieu khien nam ngoai pham vi task nay.
- **Con so bien dong giua cac lan chay**: `block_khop_plcstatus` doc duoc
  24 -> 25 -> 26 qua ba lan do trong mot gio. Do la thuc te PLC dang duoc cap
  nguon dan, khong phai kiem tra khong on dinh — script so voi API tai cung
  thoi diem nen luon khop.
- Lenh nay do BAN DEPLOY tren may nay. Khong chung minh duoc gi ve may khac.
