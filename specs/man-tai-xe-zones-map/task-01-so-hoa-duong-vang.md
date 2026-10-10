# Task 01 — Số hoá mạng lối đi dải vàng và vị trí block trên zones_map

Status: done

## Outcome
Có file `TotalParking/App_Data/driver_lanes.json` mô tả mạng lối đi trong khung
ảnh gốc `zones_map.jpeg` (4800×3584):
- tim các dải vàng;
- đúng một nút xuất phát tại R1;
- vị trí và nút đến của 112 block cơ khí và 6 khu đỗ nền 901–906.

Người dùng đã xem và xác nhận ảnh phủ trước khi task 02 bắt đầu.

## Scope
- In:
  - Script sinh dữ liệu `tools/so_hoa_duong_tai_xe.py` (chạy tay; chỉ đọc ảnh và `SELECT` CSDL):
    - **Tim đường** (tự động, sửa theo reviewer H2 khi thực thi):
      - mặt nạ vàng (định nghĩa một chỗ trong script kiểm: ngưỡng màu + closing + opening để bỏ viền vàng mảnh quanh block) trừ vùng `BO_VUNG` loại tay;
      - skeleton của mặt nạ → đồ thị pixel (networkx), cắt nhánh vụn < 80 px, đơn giản hoá RDP ε = 12 px;
      - gộp nút gần nhau ≤ 15 px; đoạn dài cắt thành cạnh ≤ 120 px; giữ thành phần liên thông lớn nhất.
    - **Nút xuất phát**: đỉnh đầu của polyline đi từ R1 (3480, 1860), `entry`.
    - **Vị trí 112 block cơ khí**:
      - toạ độ khung `plan_map` lấy từ `block.map_x/map_y` trong CSDL (cùng nguồn `GET /Monitor/BlockMap` trả về);
      - đổi sang khung `zones_map` bằng MỘT phép affine toàn cục, khớp bình phương tối thiểu trên bảng `NEO` ≥ 10 điểm neo chọn tay (block dễ nhận diện ở các góc và giữa mặt bằng);
      - in sai số từng neo; sai số tối đa ≥ 50 px là lỗi;
      - bảng `SUA_TAY` ghi đè vị trí block lệch sau khi người xem ảnh phủ;
      - không dò khối màu, không căn theo zone (C2-R4).
    - **Khu đỗ nền 901–906**:
      - lấy `block_no`, `zone_id` từ `SELECT` CSDL (`block_no >= 901`);
      - bảng `DO_NEN` gán mỗi khối một điểm đích trong khu ô đỗ thường của zone tương ứng, sát dải vàng;
      - điểm đích do Claude đề xuất và in trên ảnh phủ để người dùng xác nhận.
    - **Nút đến**: chiếu vị trí block lên cạnh gần nhất của mạng và chèn nút tại điểm chiếu.
    - Ghi JSON: `{ "frame": {"w":4800,"h":3584}, "entry": <id>, "nodes": [{"id","x","y"}], "edges": [[a,b]], "blocks": {"<block_no>": {"x","y","node"}} }`. Số nguyên, không có trường thừa.
  - Script kiểm `tools/kiem_tra_duong_tai_xe.py` (chỉ đọc JSON, ảnh, `SELECT` CSDL):
    - Kiểm AC-01..AC-03.
    - TỰ TÍNH LẠI phép affine và sai số từ bảng `NEO` (import từ `tools/so_hoa_duong_tai_xe.py`, không đọc số do script sinh ghi) để kiểm < 50 px; in danh sách block bị `SUA_TAY` ghi đè kèm độ lệch so với vị trí affine.
    - Vẽ ảnh phủ `specs/man-tai-xe-zones-map/artifacts/ban_do_duong_tai_xe.png` gồm:
      - mạng (nét xanh), R1;
      - số của 112 + 6 đích (chữ lớn, nền trắng);
      - đoạn block → nút đến;
      - bên phải là mảnh `plan_map.jpg` tương ứng (số block in đúng) để đối chiếu.
    - In SHA-256 của ảnh phủ.
- Out: sửa ảnh; ghi CSDL; dùng mạng ở runtime.

## Coverage
- CP-01

## Ownership
- Create: `tools/so_hoa_duong_tai_xe.py`
- Create: `tools/kiem_tra_duong_tai_xe.py`
- Create: `TotalParking/App_Data/driver_lanes.json`
- Create: `specs/man-tai-xe-zones-map/artifacts/ban_do_duong_tai_xe.png`
- Read: `TotalParking/Images/zones_map.jpeg`, `TotalParking/Images/plan_map.jpg`, bảng `block`

## Acceptance
- AC-01: `entry` trỏ tới đúng một nút, cách R1 ≤ 60 px.
- AC-02: mỗi cạnh lấy mẫu mỗi 10 px; ≥ 90% mẫu nằm trên mặt nạ vàng đã closing. Mặt nạ định nghĩa một chỗ trong script kiểm.
- AC-03:
  - đồ thị đúng 1 thành phần liên thông;
  - `blocks` có đúng tập `block_no` đang `is_active = 1` trong CSDL (112 + 901–906);
  - mọi `node` tồn tại và tới được từ `entry`;
  - đoạn block → nút đến ≤ 180 px và không đi gần vị trí block khác < 40 px;
  - không hai block nào cách nhau < 20 px.
- AC-04:
  - sai số affine tối đa < 50 px (script kiểm tự tính lại từ `NEO`);
  - ảnh phủ được ghi và in SHA-256;
  - **người dùng xác nhận ảnh phủ có SHA-256 đó** — controller ghi câu xác nhận nguyên văn kèm SHA-256 vào mục `## Xac nhan` trước Receipt;
  - task không được `done` khi chưa có xác nhận.

## Dependencies
- none

## Verification Plan
- Command: `python tools\kiem_tra_duong_tai_xe.py; exit $LASTEXITCODE`
- Named probe: `tools/kiem_tra_duong_tai_xe.py`. In PASS/FAIL từng mục:
  - `entry_tai_R1`, `canh_tren_duong_vang`, `lien_thong`;
  - `du_block_active`, `block_toi_duoc`, `chang_cuoi`, `block_khong_trung`;
  - `affine_sai_so`, `anh_phu`.
  Thoát 0 chỉ khi đủ 9 PASS.
- Reachability: chạy từ gốc repo trên máy chủ SCADA. Python có Pillow/numpy/scipy; CSDL đọc qua `mysql.exe` theo cấu hình `TotalParking/Web.config` (như `tools/plc_register.py`).
- Oracle: 9 dòng PASS, exit 0, dòng `sha256=`; kèm câu xác nhận của người dùng trong `## Xac nhan`.
- Counterexample:
  - nút xuất phát ở chỗ cũ → `entry_tai_R1` FAIL;
  - cạnh cắt qua khối đỗ hoặc góc chữ L → `canh_tren_duong_vang` FAIL;
  - thiếu khối đỗ nền → `du_block_active` FAIL;
  - block bám làn phía sau tường (> 180 px) → `chang_cuoi` FAIL;
  - neo chọn sai → `affine_sai_so` FAIL.
- Artifacts: `specs/man-tai-xe-zones-map/artifacts/ban_do_duong_tai_xe.png`; SHA-256 ghi vào Receipt và câu xác nhận; sinh lại ⇒ SHA-256 khác ⇒ phải xác nhận lại.

## Xac nhan
- Ảnh phủ: `specs/man-tai-xe-zones-map/artifacts/ban_do_duong_tai_xe.png`, SHA-256 `c0130200d2121f0d696a45985b658ef8ca2dfb2882fb87ddc39dd5a0220ad398`.
- Người dùng (2026-10-09), nguyên văn: "um oke do, toi xac nhan".

## Receipt

Verification: PASS
Command: python tools\kiem_tra_duong_tai_xe.py; exit $LASTEXITCODE
Exit: 0
Base: d6da2c22525212cb13636124576a202a7cf84fd4
Head: a200871c82e71c7842bcd587627472dd1801416fffe5e582117f0fe26443af2f
```text
$ python tools\kiem_tra_duong_tai_xe.py; exit $LASTEXITCODE
PASS  entry_tai_R1           entry=154 toa_do=(3480, 1860) cach_R1=0 px
PASS  canh_tren_duong_vang   220 canh, 0 canh < 90% tren vang
PASS  lien_thong             219/219 nut toi duoc tu entry
PASS  du_block_active        CSDL 118, file 118; thieu []; thua []
PASS  block_toi_duoc         118 block, khong toi duoc: []
PASS  chang_cuoi             dai nhat 166 px; vi pham: []
PASS  block_khong_trung      cap < 20 px: []
      neo block 1: sai so 4.8 px
      neo block 13: sai so 9.8 px
      neo block 20: sai so 6.4 px
      neo block 24: sai so 6.2 px
      neo block 43: sai so 7.4 px
      neo block 44: sai so 5.2 px
      neo block 63: sai so 8.8 px
      neo block 66: sai so 2.8 px
      neo block 79: sai so 3.8 px
      neo block 90: sai so 4.7 px
      neo block 102: sai so 10.1 px
      neo block 112: sai so 7.5 px
PASS  affine_sai_so          12 neo, sai so lon nhat 10.1 px (block 102), trung binh 6.5 px; block lech vi tri ky vong: []
PASS  anh_phu                specs\man-tai-xe-zones-map\artifacts\ban_do_duong_tai_xe.png sha256=c0130200d2121f0d696a45985b658ef8ca2dfb2882fb87ddc39dd5a0220ad398

TONG KET: 9/9 PASS
```

Negative control (bản copy tạm của JSON, khôi phục nguyên byte sau đó): entry dời về (1052, 3400), xoá 901, block 57 +400 px → 5 FAIL (`entry_tai_R1`, `canh_tren_duong_vang`, `du_block_active`, `chang_cuoi`, `affine_sai_so`), exit 1.

Review: code-auditor FAIL (H1 đường bám viền block, H2 Scope sai phương pháp) → sửa (opening + tracer networkx; sửa Scope) → re-review PASS, 4 Low không chặn: script kiểm chưa kiểm schema JSON (task 02 kiểm khi nạp); script kiểm không trừ `BO_VUNG`; docstring mục nút xuất phát và chú thích 901 cũ; 902 có thể sát ranh Zone 1/2.
Hạn chế: 6 điểm đỗ nền 901–906 là điểm đề xuất, chỉ có xác nhận bằng mắt trên ảnh phủ, không có dữ liệu gốc.
