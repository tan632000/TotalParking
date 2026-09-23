# Task 01 — Dashboard Tổng quan hiển thị dữ liệu thật

Status: done

## Outcome

Trang `/Home/Index` hiển thị sức chứa bãi, sức khoẻ thiết bị và danh sách xe vào
gần nhất lấy từ endpoint đang chạy, thay cho các con số cứng trong HTML. Khi
endpoint lỗi, các ô số về `—` và trang báo lỗi, không giữ số cũ.

## Scope

- **In**
  - Thêm `id` vào các ô KPI đang là HTML cứng: `Index.cshtml:40` (Tổng Pallet
    `184`), `:62` (Có xe `91`), `:65` (`49%`), `:83` (Còn trống `69`).
  - Khối fetch/render/poll theo khuôn `FloorPlan.cshtml:471,581-600`, gọi
    `Monitor/RoutingState` và `DeviceStatus/Summary`.
  - **Khác khuôn FloorPlan ở một điểm**: nhánh `catch` của FloorPlan
    (`:595-599`) chỉ hiện chữ lỗi và giữ nguyên số đã vẽ. Ở đây phải đặt cả bốn
    ô KPI về đúng ký tự `—`. Giữ số cũ khi mất nguồn là nói dối người trực.
  - Công thức KPI, cộng dồn 6 zone:
    - Tổng = `Σ z.total`
    - Có xe = `Σ z.in_use` — **nhãn "phiên đang mở"**, không phải "xe trong bãi"
    - Còn trống = `Σ z.total − Σ z.in_use`
    - `%` = `Math.round(Σin_use / Σtotal × 100)`; `Σtotal === 0` → `—`
  - Đổ danh sách xe vào gần nhất từ `RoutingState.recent`.
  - Nhãn sức khoẻ thiết bị dùng đúng từ vựng ở `plan.md` mục Từ vựng hiển thị.
- **Out**
  - **Toàn bộ khối alarm `Index.cshtml:944-1275`**, gồm `activeAlarms`, ba pool
    `Math.random()`, và ba lần `localStorage.setItem` ở `:1075`, `:1179`,
    `:1236`. Không sửa một byte nào trong vùng này.
  - Biểu đồ recharts đã dựng sẵn — giữ nguyên.
  - Không sửa `v_zone_capacity.in_use` (đang bằng 0 dù có 16 ô giữ thẻ) — xử lý
    bằng nhãn đúng, không bằng sửa dữ liệu.

## Coverage

- CP-01, CP-02, CP-03, CP-05, CP-07, CP-08

## Ownership

- Modify: `TotalParking/Views/Home/Index.cshtml`
- Create: `specs/monitoring-real-data/verify-dashboard.mjs`
- Deploy: sao chép `Index.cshtml` đã sửa sang
  `C:\Users\Admin\Documents\Web\totalParking\Views\Home\` — bản deploy là **bản
  copy độc lập**, không phải junction hay hardlink.
- Read: `TotalParking/Views/Home/FloorPlan.cshtml`,
  `TotalParking/Controllers/MonitorController.cs`,
  `TotalParking/Controllers/DeviceStatusController.cs`,
  `TotalParking/Models/VehicleRouting.cs`

## Acceptance

- AC-01: Bốn ô KPI hiển thị đúng `Σtotal`, `Σin_use`, `Σtotal − Σin_use`,
  `round(Σin_use/Σtotal×100)%`. Với stub JSON có `Σtotal = 0`, cả bốn ô hiện `—`
  chứ không phải `NaN` hay `0`.
- AC-02: Khu sức khoẻ hiển thị đúng `health` và `detail` của `camera`, `plc`,
  `led`, `pgs` từ `DeviceStatus/Summary`.
- AC-03: Bảng xe vào gần nhất có đúng số dòng và đúng `event_id` như
  `RoutingState.recent`.
- AC-04: Khi chặn `Monitor/RoutingState`, cả bốn ô KPI chứa đúng ký tự `—` và
  phần tử trạng thái chứa chữ "lỗi".
- AC-07: Năm dòng chứa `localStorage.` trong `Index.cshtml` (`:984`, `:998`,
  `:1075`, `:1179`, `:1236`) giống **hệt nguyên văn** bản trước khi sửa.
- AC-08: Nhãn của số thiết bị dùng đúng từ vựng ở `plan.md`; trang không chứa
  chữ "online" đứng một mình.
- AC-09: Hash `Index.cshtml` ở source và ở bản deploy giống nhau trước khi đo.

## Dependencies

- none

## Verification Plan

- Command: `& "C:\Program Files\nodejs\node.exe" "specs\monitoring-real-data\verify-dashboard.mjs"`
  — `node` **không nằm trên PATH** của shell này (`Get-Command node` trả rỗng),
  nhưng `C:\Program Files\nodejs\node.exe` là v24.19.0. Ghi đường dẫn đầy đủ để
  lệnh chạy lại được đúng như đã chạy.
- Named probe: `verify-dashboard.mjs` — chín kiểm tra có tên `deploy_khop_source`,
  `localstorage_nguyen_van`, `kpi_khop_routingstate`, `suc_khoe_khop_devicestatus`,
  `recent_khop_event_id`, `nhan_khong_dung_online_tran`, `kpi_stub_tong_bang_0`,
  `kpi_stub_cong_thuc_con_trong`, `loi_dat_o_ve_gach_ngang`.
- Reachability:
  - Site: đã biết — `curl -s http://localhost:8080/PlcStatus/Index` trả JSON có
    `poll_running`. Script phải gửi **một request đánh thức với timeout ≥ 10 s**
    trước khi đo, vì lần gọi nguội `DeviceStatus/Summary` đo được **2,54 s**
    (cache 15 s ở `DeviceProbeService.cs:26`) còn lần sau chỉ 1,3 ms.
  - Nhánh lỗi: đã biết — `page.setRequestInterception(true)` rồi `request.abort()`
    riêng URL `**/Monitor/RoutingState`. Kỹ thuật này **đã dùng trong repo** tại
    `.claude/skills/chrome-devtools/scripts/dg-proof.mjs:18`,
    `dg-measure.mjs:33`, `dg-check.mjs:44`. Không đụng server đang chạy.
  - Stub `Σtotal = 0`: đã biết — chặn URL rồi `request.respond()` với JSON
    `{"zones":[],"recent":[],"now":"00:00:00"}`.
  - Stub phân biệt công thức: đã biết — hai zone giả có `Σtotal=200`, `Σin_use=30`,
    `Σfree_mech=70`, `Σfree_tier0=20`, `Σfree_ground=50`. Công thức đúng ra **170**,
    công thức sai ra **140**. Stub `Σtotal = 0` KHÔNG phân biệt được hai công thức
    (cả hai cùng ra `—`), nên stub này là bắt buộc.
- Oracle: script in `PASS`/`FAIL` cho từng kiểm tra, thoát mã 0 chỉ khi cả chín
  `PASS`. `deploy_khop_source` thất bại thì in "chưa deploy" và **dừng ngay**,
  không chạy tiếp các kiểm tra sau.
- Counterexample:
  - Để nguyên `184 / 91 / 69` cứng trong HTML → `kpi_khop_routingstate` FAIL vì
    `Σtotal` thật hiện là 835.
  - Dùng công thức cũ `free_mech + free_tier0 + free_ground` → **PASS trên dữ liệu
    live** (vì `in_use = 0` ở cả 6 zone) và **cũng PASS ở `kpi_stub_tong_bang_0`**
    (cả hai công thức cùng ra `—` khi `Σtotal = 0`). Chỉ
    `kpi_stub_cong_thuc_con_trong` bắt được: công thức đúng ra `170`, công thức
    sai ra `140`. Đây là kiểm tra duy nhất phân biệt được hai công thức.
  - Sửa code mà quên copy sang deploy → `deploy_khop_source` FAIL.
- Artifacts: `specs/monitoring-real-data/artifacts/dashboard.png`, ghi đè mỗi
  lần chạy, không so digest — chỉ để người đọc đối chiếu bằng mắt.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\monitoring-real-data\verify-dashboard.mjs"`
Exit: 0
Base: 8b9054d5928f712759cb69a5af2e21cf8c7334d6
Head: 8b9054d5928f712759cb69a5af2e21cf8c7334d6
Deploy: Index.cshtml sha256 e5488db43281... (source == deploy, kiem boi deploy_khop_source)

```text
  PASS  deploy_khop_source  | e5488db43281
  PASS  localstorage_nguyen_van  | 5 dong
  PASS  kpi_khop_routingstate  | trang 835/0/835/0% vs api 835/0/835/0%
  PASS  suc_khoe_khop_devicestatus  | plc="85/86 phan hoi" camera="Dang nhan su kien"
  PASS  recent_khop_event_id  | 12 dong vs 12 tu api
  PASS  nhan_khong_dung_online_tran  | khong co
  PASS  kpi_stub_tong_bang_0  | total="—" pct="—"
  PASS  kpi_stub_cong_thuc_con_trong  | free="170" (dung=170, cong thuc sai se ra 140)
  PASS  loi_dat_o_ve_gach_ngang  | total="—" free="—" trangThai="loi: Failed to fetch"

9/9 PASS
EXIT: 0
```

### Anh xa AC -> kiem tra

| AC | Kiem tra | Ket qua |
|---|---|---|
| AC-01 | `kpi_khop_routingstate`, `kpi_stub_tong_bang_0`, `kpi_stub_cong_thuc_con_trong` | PASS |
| AC-02 | `suc_khoe_khop_devicestatus` | PASS |
| AC-03 | `recent_khop_event_id` | PASS |
| AC-04 | `loi_dat_o_ve_gach_ngang` | PASS |
| AC-07 | `localstorage_nguyen_van` | PASS |
| AC-08 | `nhan_khong_dung_online_tran` | PASS |
| AC-09 | `deploy_khop_source` | PASS |

### Gioi han cua bang chung nay

- **Negative control cua cong thuc la SO HOC, khong phai da chay.** Stub cho
  `Sigma total=200, Sigma in_use=30, Sigma free_*=140`. Cong thuc dung ra 170 va
  script quan sat duoc 170. Chua chay bien the code sai de thay no ra 140 —
  bien the phai chay tren ban sao dung roi, khong duoc dung byte canonical.
- **`in_use` that hien bang 0 o ca 6 zone** trong khi `SlotStatus.occupied = 16`.
  Con so tren Dashboard dung voi nguon cua no, nhung nguon do chua phan anh xe
  that trong bai. Da xu ly bang nhan "Phien dang mo"; sua `v_zone_capacity` nam
  ngoai pham vi (xem plan.md Out of scope).
- **HMI Block luon hien "chua giam sat"** vi SCADA khong co nguon du lieu nao
  cho no. Day la su that duoc phoi bay, khong phai loi.
- Lenh nay do BAN DEPLOY. `deploy_khop_source` chan truong hop source lech deploy,
  nhung khong chung minh duoc gi ve cac may khac.
