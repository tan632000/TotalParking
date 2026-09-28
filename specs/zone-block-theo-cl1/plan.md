# Sửa phân bổ block theo zone, số ô đỗ và lưới cơ cấu theo file CL1
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-09-28, mở rộng sau C2)

- **Nguồn sự thật:** `docs/TTP_LUMI_TYPE_BLOCK_RE01.xlsx`, **sheet `CL1`**
  (112 block, 6 zone — khớp đúng hệ thống này). Sheet `CL2` là cụm vật lý khác
  (91 block, 4 zone) và **không được dùng**.
- **Existing:**
  - Chọn block trong zone — `TotalParking/Services/BlockAllocator.cs:54`
  - Tính chỗ trống theo `slot_count` — `TotalParking/Services/BlockAllocator.cs:45`
  - Ngưỡng tươi dữ liệu PLC đã có — `TotalParking/Services/BlockAllocator.cs:32`
    (`PlcFreshMinutes = 5`)
  - Luật `2600KG -> chỉ tier 0` — `TotalParking/Services/ZoneRouter.cs:20-22`
  - Bất biến lưới cơ cấu — `TotalParking/Database/05_parking_topology.sql:87`
    (`slot_count = tier_count * column_count`; tier 0 là tầng dưới cùng)
  - Bất biến lệch số ô **đã có sẵn** — `TotalParking/Database/21_block_map_view.sql:46`
    (`v_block_map.lech_so_o`, đã lọc `kind='Mechanical'`)
  - Gieo ô đỗ, chỉ block cơ khí — `TotalParking/Database/17_plc_slot_state.sql:69,78`
  - **Nguồn gieo `zone_id`/`slot_count` hiện hành** —
    `TotalParking/Database/14_zone_blocks_from_customer.sql:54` (xem Hiểm hoạ bên dưới)
  - Quy ước migration — `TotalParking/Database/NN_*.sql`, **mới nhất là `50`**
- **Minimum change:** hai file migration `51_` và `52_`. **Không đụng mã nguồn** —
  `BlockAllocator` và `ZoneRouter` đọc dữ liệu lúc chạy.
- **Bổ sung theo yêu cầu người dùng (28/09/2026):** đổi tên zone theo công thức
  `Zone n (Hầm Bn CL1)`. Không có bãi ngoài trời trong công trình này. Việc này
  chạm vào hai view có danh sách viết cứng (`OperationControl.cshtml`,
  `ZoneDetail.cshtml`) — là dữ liệu hiển thị, không phải logic.
- **Expansion signals:** không có. 2 migration + 1 công cụ đối chiếu (ngưỡng 8),
  0 lớp mới (ngưỡng 2), 2 hệ con (ngưỡng 3).
- **User decision (C1):** lấy cả zone và số ô đỗ, áp bằng migration có sao lưu và
  câu lệnh hoàn tác.
- **User decision (C2):** **mở rộng task 02** thêm `column_count` và `tier_count`
  lấy từ cột D của CL1, để mở lại luật định tuyến `2600KG`.
- **User decision (28/09/2026, sau khi verify):** **tách `column_count`/`tier_count`
  thành task 03 và hoãn.** Lý do: kích thước ngoài của block trong CL1 đọc ra một
  cơ cấu 2 tầng mâu thuẫn với bất biến `slot_count = tier_count * column_count`,
  nên số `total_tier0` chưa xác định được. Chi tiết và câu hỏi cần hỏi bên thiết
  kế nằm trong `task-03-luoi-co-cau.md`. Task 01 và 02 chạy ngay.

### ⚠ Hiểm hoạ: một migration cũ sẽ hoàn tác sạch việc này

`TotalParking/Database/14_zone_blocks_from_customer.sql`:

- dòng `2` tự khai *"Chay lai nhieu lan an toan"*;
- dòng `52` có `DELETE FROM block;`;
- dòng `54` `INSERT INTO block (zone_id, block_no, kind, slot_count, ...)` với
  **`zone_id` và `slot_count` viết cứng cho cả 118 block**.

Chạy lại file 14 sẽ đưa 29 block về zone cũ và 7 block về số ô cũ **mà không
cảnh báo gì**, đồng thời xoá `plc_request`, `parking_session`, `parking_slot`,
`plc_device`. Hai migration mới phải ghi rõ ràng buộc thứ tự, và file 14 phải
được chèn một dòng cảnh báo trỏ tới `51`/`52`.

### Bốn nguồn độc lập cùng xác nhận CL1

| Nguồn | Kết quả |
|---|---|
| Cột D + G của CL1 | 9×3sp, 41×5sp, 22×6sp, 40×10sp = 112 block / **764 ô** |
| Bảng model in trên bản vẽ — `TotalParking/Database/16_zone_fix_78_79.sql:32` | `9 x 3sp + 1 x 5sp-4800L + 40 x 5sp-5000L + 22 x 6sp + 40 x 10sp = 112 bo / 764 cho` |
| Bất biến `slot_count = tier_count × column_count` | chia hết ở **112/112** block |
| Phép cộng 7 thay đổi số ô | 755 **+9** = **764** |

`14_zone_blocks_from_customer.sql:26-28` từng ghi *"Tong o: file khach 755, bang
thong ke in tren ban ve ghi 'SUC CHUA: 764 XE'. Lech 9 o."* — **CL1 đóng đúng
khoảng lệch 9 ô đó.**

`14_...sql:19-22` còn ghi *"block 87/88: file ghi 3,10 — ghep tu dong ra 10,3,
tuc la loi cua phep ghep tham lam"*. CL1 nay nói 87=**10**, 88=**3**, tức phép
ghép nhãn CAD mới đúng và kết luận cũ sai. Hai nguồn độc lập giờ đồng thuận.

### Sai lệch đo được

**29 block sai zone:**

| Block | CSDL | CL1 |
|---|---|---|
| 21, 22, 23 | 5 | **6** |
| 29, 30, 31, 32 | 6 | **5** |
| 39, 48, 49, 50 | 3 | **4** |
| 58, 59 | 4 | **5** |
| 60 | 2 | **4** |
| 61, 62 | 2 | **5** |
| 63, 64, 65, 66, 67, 68, 69 | 2 | **6** |
| 76, 77, 78, 79 | 2 | **4** |
| 86, 96 | 2 | **1** |

**7 block sai số ô đỗ:**

| Block | CSDL | CL1 | Hướng |
|---|---|---|---|
| 5, 29 | 5 | **10** | thêm 5 ô mỗi block |
| 87, 89 | 3 | **10** | thêm 7 ô mỗi block |
| 6, 33 | 10 | **6** | bớt 4 ô mỗi block |
| 88 | 10 | **3** | bớt 7 ô |

**`column_count` / `tier_count`:** hiện **NULL ở toàn bộ 112 block cơ khí**, nên
`v_zone_capacity.total_tier0` luôn rỗng và luật `2600KG` không chạy được. Suy ra
từ cột D của CL1: `column_count` = số hàng (1 hoặc 2),
`tier_count` = `slot_count / column_count`.

### Bằng chứng điểm mù của `plc_slot_state` (đọc thẳng PLC ngày 28/09)

```
block 87  D300 = 0xE060, D301 = 0x62BA  ->  the 62bae060
```

Thẻ này **có thật trên thiết bị**, nhưng `plc_slot_state` không có dòng nào cho
block 87 ở `word_addr = 300`, và nhật ký thanh ghi chỉ từng ghi `D400`, `D202`,
`D204` cho block đó — **đúng 3 ô đang khai báo**. Đó chính là điểm mù: SCADA
không đọc ô nó không biết là có tồn tại. Nâng block 87 lên 10 ô sẽ vá.

*Hai người rà soát đều kết luận khẳng định này thiếu bằng chứng, một người tra
`plc_slot_state`, một người grep nhật ký. Cả hai nguồn đó đều nằm trong chính
điểm mù đang bàn, nên không dùng để bác bỏ được. Phép đọc PLC trực tiếp ở trên
là nguồn độc lập.*

## Out of scope

- **Không đụng block 901–906** (`kind='Ground'`). CL1 chỉ liệt kê block cơ khí.
- **Không sửa `vehicle_routing`** (1441 dòng) — nhật ký quyết định. Đã kiểm:
  **0 dòng** trỏ vào 29 block đổi zone.
- **Không sửa `canh_bao`** — bảng này lưu `zone_id` phi chuẩn hoá; 7 dòng cũ sẽ
  ghi zone cũ. Xem Giới hạn 5.
- **Không rà `led_panel_port.zone_list`** (22 dòng). Xem Giới hạn 1.
- **Không sửa `bay_length_mm`** — cột C của CL1 cho chiều dài đều là 5000, không
  có block 4800 nào; giá trị hiện tại đã đúng.
- Không đụng `zone.polygon`, `gate_rank`, `color_hex`. Không dùng sheet `CL2`.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | `block.zone_id` của 112 block cơ khí khớp CL1 | sửa dữ liệu vận hành | `block.zone_id`, `BlockAllocator`, `v_zone_capacity` | rõ | critical — đổi nơi xe được xếp vào | live (đối chiếu CL1 với CSDL đang chạy) |
| CP-02 | Block đỗ nền 901–906 giữ nguyên zone | không đổi | `block` với `kind='Ground'` | rõ | elevated — sửa nhầm làm mất khu đỗ nền | live (so trước/sau) |
| CP-03 | `block.slot_count` khớp CL1, tổng đạt 764 | sửa dữ liệu vận hành | `block.slot_count`, `v_zone_capacity` | rõ | critical — sai sức chứa làm xếp quá số chỗ | live (đối chiếu + tổng) |
| CP-04 | `plc_slot_state` đồng bộ với `slot_count` ở block cơ khí đang bật | sửa dữ liệu vận hành | `plc_slot_state`, `v_block_map.lech_so_o` | rõ | critical — lệch làm SCADA mù một phần ô đỗ | live (bất biến có sẵn) |
| CP-05 | Không ô nào có thể đang giữ xe bị xoá | chặn thao tác phá huỷ | `plc_slot_state.card_code`, `.read_at`, `.changed_at` | rõ; ba vị từ nằm **trong** câu `DELETE` | critical — mất dấu xe đang đỗ | live (vị từ + `ROW_COUNT`) |
| CP-06 | Migration là nguyên tử: hỏng giữa chừng không để lại trạng thái lệch | giao dịch | `plc_slot_state`, `block` | rõ | critical — lệch làm phát suất vào ô vô hình | live (kiểm bất biến sau khi chạy) |
| CP-07 | `column_count`/`tier_count` khớp CL1, thoả `slot_count = tier × column` | sửa dữ liệu vận hành | `block`, `v_zone_capacity.total_tier0`, `ZoneRouter` | rõ; C2 đã chốt mở rộng | elevated — mở lại luật `2600KG` đang chết | live (đối chiếu + bất biến) |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | Khi đối chiếu CSDL với CL1, số block lệch zone phải bằng **0**. | `python tools/doi_chieu_zone_block.py --phan zone` |
| AC-02 | Khi chạy migration zone, `zone_id` của block 901–906 phải **không đổi**. | cùng lệnh, mục "block đỗ nền" |
| AC-03 | Khi đối chiếu CSDL với CL1, số block lệch số ô phải bằng **0** và `SUM(slot_count)` của block cơ khí phải bằng **764**. | `python tools/doi_chieu_zone_block.py --phan o-do` |
| AC-04 | Với mọi block **`kind='Mechanical' AND is_active=1`**, số dòng `plc_slot_state` phải bằng `slot_count`, và `word_addr` theo `slot_index` phải khớp tiền tố của `400,202,204,206,208,300,302,304,306,308`. | cùng lệnh, đọc `v_block_map.lech_so_o` |
| AC-05 | Khi một ô sắp bị xoá đang giữ thẻ, **hoặc** dữ liệu của nó cũ hơn 5 phút, **hoặc** nó đã từng đổi trạng thái, migration phải **không xoá dòng nào** và kết thúc với `ROW_COUNT()` khác 15. | `python tools/doi_chieu_zone_block.py --kiem-truoc-khi-xoa` |
| AC-06 | Với mọi block cơ khí, `slot_count = tier_count * column_count` và cả hai cột đều khác `NULL`; `v_zone_capacity.total_tier0` khác 0 ở cả 6 zone. | `python tools/doi_chieu_zone_block.py --phan luoi` |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Gán lại zone cho 29 block theo CL1, đổi tên zone theo công thức | AC-01, AC-02 | `TotalParking/Database/51_zone_theo_cl1.sql`, `tools/doi_chieu_zone_block.py` | - | done |
| 02 | Sửa số ô đỗ và đồng bộ `plc_slot_state` | AC-03, AC-04, AC-05 | `TotalParking/Database/52_so_o_theo_cl1.sql` | `task-01-zone-theo-cl1.md` | done |
| 03 | Lưới cơ cấu (`column_count` / `tier_count`) | AC-06 | `TotalParking/Database/53_*.sql` (chưa viết) | `task-02-so-o-theo-cl1.md` | blocked |

## Giới hạn đã biết (nêu trước, không giấu tới C3)

1. ~~**Bảng LED chưa được rà.**~~ **ĐÃ GỠ ngày 28/09/2026** bằng
   `TotalParking/Database/53_led_cong_theo_block.sql`. Khách cung cấp
   `docs/LumiSlotsMatrix.xlsx` khai rõ mỗi cổng LED phục vụ những block và cảm
   biến nào. Không cổng nào trùng đúng một zone (độ giống cao nhất 0.85), nên
   mô hình `zone_list` bị thay bằng `led_port_block` + `led_port_sensor` và
   `scope = 'BLOCKS'`. Từ đây **bảng LED không còn phụ thuộc vào cách chia
   zone**, nên việc đổi zone theo CL1 không còn ảnh hưởng tới số hiển thị.
2. **Đối chiếu chứng minh dữ liệu khớp file, không chứng minh file khớp thực địa.**
   Nếu CL1 sai so với công trình thì phép kiểm vẫn PASS. Bốn nguồn đồng thuận ở
   trên làm giảm rủi ro này nhưng không loại bỏ được.
3. **Kiểm không gian không kết luận được.** Theo `block.origin_x/y`, tính liên
   mạch của `block_no` trong zone xấu đi sau khi đổi (block 62 lệch nhất: 5963 px
   tới tâm zone 5 mới so với 2419 px tới tâm zone 2 cũ), trong khi
   `16_zone_fix_78_79.sql:13-20` từng coi liên mạch là dấu hiệu tốt. Toạ độ chỉ
   chính xác ±23 px nên không đủ để bác bỏ CL1 — **nên hỏi đội thi công xác nhận
   riêng block 61 và 62**.
4. **Dữ kiện "không ô nào đang giữ thẻ" đổi được.** Đo ngày 28/09: cả 15 dòng
   sắp xoá đều `card_code NULL`, `changed_at NULL`, `read_at` mới 0 phút. AC-05
   tồn tại chính vì trạng thái này không bền.
5. **`canh_bao` lưu `zone_id` phi chuẩn hoá** — 7 dòng cảnh báo cũ trên các block
   đổi zone sẽ vẫn ghi zone cũ. Ảnh hưởng nhỏ, không sửa ngược (là lịch sử).
6. **Cấm chạy `tools/xoa_thanh_ghi_o_do.py` trong cửa sổ migration.** Công cụ đó
   ghi xuống PLC và sẽ phá giá trị bằng chứng của `changed_at`.

## Review log

- **Round 1 (2026-09-28)** — hai người rà soát ngữ cảnh mới, 11 phát hiện sau khử trùng lặp.
  - **Chấp nhận (Critical):** thêm vị từ độ tươi `read_at >= NOW() - INTERVAL 5 MINUTE`
    vào câu xoá (PLC rớt mạng làm `card_code` giữ NULL cũ —
    `SlotOccupancyReader.cs:105-115` xác nhận lỗi đọc không đụng tới hai cột đó);
    đưa vị từ vào **trong** câu `DELETE` thay vì kiểm trước, kèm `ROW_COUNT()=15`
    (vòng quét 45 s ghi song song, TOCTOU); bao `DELETE/INSERT/UPDATE` trong một
    giao dịch, và ghi câu hoàn tác dạng `INSERT…SELECT` vì `CREATE TABLE AS SELECT`
    gây implicit commit và mất khoá chính; **đổi số migration từ 79/80 thành 51/52**
    (mới nhất thật là 50); ghi hiểm hoạ `14_zone_blocks_from_customer.sql:52`.
  - **Chấp nhận (High):** giới hạn AC-04 vào `kind='Mechanical' AND is_active=1`
    (6 block đỗ nền có 0 dòng `plc_slot_state` theo đúng thiết kế —
    `17_plc_slot_state.sql:78`); thêm `changed_at IS NULL` làm lớp chặn thứ tư.
  - **Chấp nhận (Medium/Low):** dùng `v_block_map.lech_so_o` có sẵn thay vì viết
    lại phép kiểm; ghi `canh_bao` và lệnh cấm chạy công cụ ghi PLC vào Giới hạn.
  - **Bác bỏ:** khẳng định "block 87 có thẻ ở `D300` không có bằng chứng". Cả hai
    người rà soát dùng `plc_slot_state` và nhật ký thanh ghi để bác bỏ, mà cả hai
    nguồn đó đều nằm trong chính điểm mù đang bàn. Phép đọc PLC trực tiếp cho
    `D300/D301 = E060 62BA`. Đã ghi bằng chứng vào plan.
  - **Mở rộng theo quyết định người dùng:** thêm `column_count`/`tier_count` vào
    task 02.
  - **Bằng chứng ủng hộ do rà soát mang lại, đã đưa vào plan:** tổng 764 khớp bản
    vẽ và đóng đúng khoảng lệch 9 ô; chú thích block 87/88 trong file seed cũ nay
    được CL1 xác nhận ngược lại.
  - Sweep: đã rà toàn bộ packet; CP-06 và CP-07 là hàng mới; AC-06 mới; AC-03 thêm
    mệnh đề tổng 764; AC-04 và AC-05 siết lại.
