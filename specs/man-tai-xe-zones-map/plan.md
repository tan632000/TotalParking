# Màn hình tài xế dùng bản đồ Mặt bằng hệ thống
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-10-09)
- Yêu cầu của khách:
  - Màn hình tài xế (`/Home/DriverGuide`) dùng bản đồ của màn Mặt bằng hệ thống (`zones_map.jpeg`).
  - Dải màu vàng là lối xe; đường đi phải bám dải vàng.
  - Điểm bắt đầu LUÔN là ram dốc 1.
- Ram dốc 1 = **R1**, user xác nhận trên ảnh đánh dấu (09/10):
  - Là cuối dải vàng ngang dài đi qua Zone 2, chạm dải xám gạch chéo trên ranh Zone 6 / Zone 1.
  - Toạ độ ≈ (3480, 1860) trên ảnh gốc 4800×3584.
  - Reviewer đo: tại y = 1860, x = 3480 còn vàng, từ x = 3500 hết vàng.
- Hiện trạng (sai):
  - Nút xuất phát `lane_node` 313 tại (1052, 1165) khung `plan_map`, cạnh block 110–112 (`Database/37_lane_network.sql:162`).
  - Mạng 313 nút dò từ MỌI khoảng trống trên `plan_map.jpg` (`37_lane_network.sql:16-31`), không theo dải vàng.
- Ràng buộc:
  - `zones_map.jpeg` được vẽ độc lập với bản vẽ CAD. Các cách căn tự động trước đây thất bại 68–79% (`TotalParking/Services/BlockMapRepository.cs:13-17`).
  - Đo lại 09/10 bằng **affine toàn cục từ 12 điểm neo chọn tay**: sai số tối đa 37 px; 112/112 điểm rơi đúng block; bố cục block hai ảnh khớp 1-1. → Vị trí block = affine có neo tay + kiểm sai số + bảng sửa tay. Mạng lối đi số hoá TRỰC TIẾP trên `zones_map` theo tim dải vàng.
  - Mặt nạ vàng vỡ nhiều mảnh (361–1018 tuỳ ngưỡng) → số hoá tim đường bán thủ công, kiểm dày dọc từng cạnh.
  - Số block in trên `zones_map` bị sai (block 17 in "9", 18 in "10", 10 in "18", 32 in "20") → màn hình phải tự vẽ số đúng.
- Existing:
  - Vẽ đường + vòng đích + poll 2 s: `TotalParking/Views/Home/DriverGuide.cshtml:156-268`.
  - Dijkstra xác định: `TotalParking/Services/LaneNetwork.cs:123-182`.
  - `/Monitor/DriverRoute`: `TotalParking/Controllers/MonitorController.cs:305-389`.
  - Chọn block (`BlockAllocator`; đỗ nền 901–906, mỗi zone một khối: `BlockAllocator.cs:57-58`, `:85-108`) không đổi.
- Minimum change:
  - Dữ liệu `App_Data/driver_lanes.json` (khung `zones_map`):
    - tim dải vàng;
    - nút xuất phát R1;
    - vị trí + nút đến cho 112 block cơ khí VÀ 6 khu đỗ nền 901–906.
  - Tìm đường trên mạng đó, có chặng cuối từ làn vào tới block.
  - `DriverRoute` trả đường đi khung `zones_map`.
  - `DriverGuide` đổi ảnh nền, khoanh block, vẽ số block đúng.
- Expansion signals: không (≈8 file, 1 lớp mới, một hệ con).
- Risk: `elevated` — dẫn đường cho tài xế (sai block = tới nhầm chỗ). Không ghi PLC, không đổi việc chọn block.
- User decision: KEEP; lưu mạng mới bằng file JSON trong `App_Data`.

## Out of scope
- Trang Điều hướng xe (`/Home/Routing`), kể cả nút Mô phỏng (`MonitorController.Simulate`, `MonitorController.cs:146`), giữ `plan_map.jpg` và mạng `lane_node`/`lane_edge` cũ. **Giới hạn chấp nhận (C2-R9):** đường xem trước trên trang Routing xuất phát từ cổng cũ, khác màn tài xế.
- Bảng `lane_*`, `block.lane_node_id`: không sửa.
- Logic chọn block.
- Màn Mặt bằng hệ thống và ảnh `zones_map.jpeg`: không sửa.
- Giảm dung lượng ảnh 7,7 MB.
- Chiều một chiều của làn: coi mọi đoạn vàng là hai chiều, như mạng cũ.

## Coverage profile
| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Mạng lối đi khung `zones_map`: cạnh nằm trên dải vàng, liên thông, đúng một nút xuất phát tại R1, đủ 112 block + 6 khu đỗ nền có vị trí và nút đến; người dùng xác nhận ảnh phủ TRƯỚC task 02 | add | Data/schema (JSON), Integration (ảnh, DB đọc) | examples-needed → human confirmation (gate) | elevated; affine neo tay đo 37 px | source (script kiểm JSON + mặt nạ), human (ảnh phủ, SHA-256) |
| CP-02 | Tìm đường R1 → làn → block; dữ liệu lỗi → `MESSAGE`, không 500, không vẽ đường đoán | add, modify | API, Async/state (nạp file, cache) | none | elevated; `LaneNetwork.cs:148,163` ném KeyNotFound | source (DLL: 118 đích + 4 ca lỗi) |
| CP-03 | Màn tài xế hiện `zones_map.jpeg`, đường xanh vào tới block, vòng khoanh block, số block đúng; bản deploy có file JSON | modify | Interaction/UI, Runtime/deploy | none | elevated; deploy trung gian vẽ lệch (C2-R1) | live (HTML, khung, file deploy SHA-256), human (màn thật) |

## Acceptance criteria
| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | The lane data shall contain exactly one entry node, within 60 px of R1 (3480, 1860) in the 4800×3584 frame. | `tools/kiem_tra_duong_tai_xe.py` |
| AC-02 | Every lane edge, sampled every 10 px, shall have ≥ 90% of its samples on the closed yellow mask of `zones_map.jpeg`. | `tools/kiem_tra_duong_tai_xe.py` |
| AC-03 | The lane graph shall be one connected component, and every active block (112 mechanical + 901–906) shall have a position and an arrival node reachable from the entry; the block→arrival leg shall be ≤ 180 px and pass no closer than 40 px to another block's position. | `tools/kiem_tra_duong_tai_xe.py` |
| AC-04 | Block positions shall come from a global affine fit on hand-picked anchors with max residual < 50 px; the tool shall render an overlay (graph, R1, numbers, block→arrival legs, side-by-side `plan_map` crop) and print its SHA-256, and the user shall confirm that overlay before task 02 starts. | `tools/kiem_tra_duong_tai_xe.py` + user confirmation recorded in task 01 |
| AC-05 | Given the lane file, routing to any active block shall return a route that starts at the entry node and ends at the block position, all points inside 4800×3584. | `tools/kiem_chung_duong_tai_xe.ps1 -ChiDll` |
| AC-06 | If the lane file is missing, malformed, has a frame other than 4800×3584, or references an unknown node, `DriverLaneMap.RouteToBlock` shall return not-found with a reason and never throw (which `DriverRoute` already maps to `MESSAGE`, `MonitorController.cs:323-324`); it shall never produce a fallback line. | `tools/kiem_chung_duong_tai_xe.ps1 -ChiDll` |
| AC-07 | After a single publish of tasks 02 + 03, the deployed `App_Data/driver_lanes.json` shall match the source SHA-256, `/Monitor/DriverRoute` shall report 4800×3584, and the DriverGuide page shall load `zones_map.jpeg` (mtime cache-busted) with no `plan_map.jpg` reference. | `tools/kiem_chung_duong_tai_xe.ps1` |
| AC-08 | When the state is `ROUTE`, the DriverGuide page shall draw the route to the block, a ring around the block position and the correct block number, sized from `view_w`. | `tools/kiem_chung_duong_tai_xe.ps1` (source check) + user confirmation at C3 |

## Tasks
| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Số hoá mạng lối đi dải vàng + vị trí block, người dùng xác nhận ảnh phủ | AC-01, AC-02, AC-03, AC-04 | `TotalParking/App_Data/driver_lanes.json` | - | done |
| 02 | Tìm đường trên mạng mới và trả qua DriverRoute (chưa publish) | AC-05, AC-06 | `TotalParking/Services/DriverLaneMap.cs` | task-01-so-hoa-duong-vang.md | done |
| 03 | Màn tài xế dùng zones_map, publish một lần | AC-07, AC-08 | `TotalParking/Views/Home/DriverGuide.cshtml` | task-02-tim-duong-driverroute.md | done |

## Review log
- Round 1 (2026-10-09): 2 reviewer độc lập.
  - Reviewer A: Fact/Contract.
  - Reviewer B: Assumption/Failure-mode, có đo trên ảnh.
  - Xác nhận đúng: R1, bố cục block khớp 1-1, mọi block ≤ 107 px tới làn.
  - C2: user chấp nhận R1, R2, R4, R6, R7, R8, R9; chọn R3 = vẽ đường tới khu đỗ nền; R5 = khoanh block + vẽ số đúng.
  - R1 deploy trung gian vẽ lệch → task 02 chỉ kiểm DLL; publish MỘT lần ở task 03.
  - R2 xác nhận ảnh phủ trước task 02 → AC-04, task 01 Acceptance.
  - R3 đỗ nền 901–906 (~65% lượt ROUTED 7 ngày) → CP-01, AC-03, task 01/02.
  - R4 ghép block bằng affine toàn cục neo tay → task 01 Scope, AC-04.
  - R5 vòng đích trùng giữa block hai bên làn, số in sai → chặng cuối tới block, vẽ số → AC-05, AC-08, task 02/03.
  - R6 oracle thưa → lấy mẫu 10 px, block→làn ≤ 180 px → AC-02, AC-03.
  - R7 oracle không bắt được thiếu file deploy → hằng khung, SHA-256 file deploy → AC-06, AC-07.
  - R8 dữ liệu treo → 500 → kiểm toàn vẹn, không cache lỗi → AC-06, task 02.
  - R9 Mô phỏng Routing dùng mạng cũ → Out of scope (giới hạn chấp nhận).
- Closure (reviewer mới): R1–R9 PASS. Sửa thêm 3 mâu thuẫn: status blocked không lý do → pending; AC-06 thu hẹp về lớp DLL (RouteToBlock không ném lỗi); affine_sai_so tự tính lại từ NEO thay vì đọc file của script sinh.
- Sweep: 4 file / 3 delta / 0 tham chiếu cũ (affine.txt đã gỡ) / 0 mâu thuẫn còn lại.
