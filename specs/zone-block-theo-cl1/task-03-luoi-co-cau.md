# Task 03 — Lưới cơ cấu (`column_count` / `tier_count`)

Status: blocked

## Blocker

Chưa biết một block **5 ô, 1 hàng** có mấy chỗ ở **tầng dưới cùng**. Câu hỏi cần
bên thiết kế trả lời, nguyên văn để hỏi:

> Một block 5 ô, 1 hàng, kích thước ngoài 5870 × 7660 mm, pallet 5000 × 2500 mm —
> có mấy chỗ đỗ ở **tầng dưới cùng** (tầng xe vào thẳng, không phải nâng)?
> **2** hay **5**?

Không thể đoán: hai cách đọc tài liệu cho hai kết quả khác nhau, và con số này
là thứ luật định tuyến `2600KG` dùng để chọn chỗ.

## Outcome

`block.column_count` và `block.tier_count` của 112 block cơ khí khác `NULL` và
thoả `slot_count = tier_count * column_count`, `v_zone_capacity.total_tier0`
khác 0 ở cả 6 zone — mở lại luật định tuyến `2600KG` vốn đang chết
(`TotalParking/Services/ZoneRouter.cs:20-22`).

## Vì sao không làm cùng task 02

C2 từng chốt: `column_count` = số hàng ở cột D của CL1 (1 hoặc 2),
`tier_count` = `slot_count / column_count`. Cách đó **thoả bất biến của CSDL**
(`05_parking_topology.sql:87`) và cho `total_tier0 = 174`.

Nhưng đọc **kích thước ngoài** của block trong CL1 cùng với pallet 5000 × 2500
lại ra một cơ cấu 2 tầng kiểu xếp hình, trong đó một cụm C cột chứa `2C − 1` xe:

| hàng | ô | kích thước ngoài | đọc ra từ kích thước |
|---|---|---|---|
| 1 | 3 | 5870 × 5160 | 1 sâu × 2 rộng → 2 chỗ trên + 1 chỗ dưới |
| 1 | 5 | 5870 × 7660 | 1 sâu × 3 rộng → 3 chỗ trên + 2 chỗ dưới |
| 2 | 6 | 11370 × 5160 | 2 × 3 |
| 2 | 10 | 11370 × 7660 | 2 × 5 |

Cả bốn loại block đều khớp cách đọc này. Nếu nó đúng thì `tier_count = 2` ở
**mọi** block, tức `slot_count = tier_count * column_count` **không còn đúng**
(5 ≠ 2 × 3) — bất biến của CSDL và hình học thực tế mâu thuẫn nhau.

Hai cách đọc cho `total_tier0` khác nhau, và đó chính là con số `ZoneRouter`
dùng. Ghi bừa một trong hai sẽ làm hệ thống **im lặng xếp xe nặng sai chỗ**, tệ
hơn hẳn trạng thái hiện tại là luật không chạy và không ai bị xếp nhầm.

Người dùng đã duyệt tách task này ra và hoãn (28/09/2026).

## Scope

- **In:** một migration `53_*.sql` đặt `column_count` và `tier_count`, sau khi
  bên thiết kế trả lời. Nếu câu trả lời phá bất biến
  `slot_count = tier_count * column_count` thì phải sửa cả ràng buộc ở
  `TotalParking/Database/05_parking_topology.sql:87` — và đó là thay đổi lược
  đồ, phải quay lại C1.
- **Out:** `zone_id` (task 01), `slot_count` và `plc_slot_state` (task 02).

## Coverage

- CP-07

## Acceptance

- **AC-06:** `python tools/doi_chieu_zone_block.py --phan luoi` báo
  `LECH LUOI: 0` — mọi block cơ khí có
  `slot_count = tier_count * column_count`, không cột nào `NULL`, và
  `v_zone_capacity.total_tier0` khác 0 ở cả 6 zone.

## Dependencies

- `task-02-so-o-theo-cl1.md`

## Verification Plan

- **Command**: `python tools/doi_chieu_zone_block.py --phan luoi`
- **Named probe:** `tools/doi_chieu_zone_block.py`, mục `LECH LUOI` và
  `TOTAL_TIER0 THEO ZONE`.
- **Reachability:** `v_zone_capacity` cộng `b.column_count`
  (`TotalParking/Database/50_zone_capacity_ground_sensor.sql:39`), rồi
  `ZoneRouter.cs:20-22` dùng `total_tier0` cho luật `2600KG`.
- **Oracle:** `LECH LUOI: 0` và `total_tier0` khác 0 ở cả 6 zone.
- **Counterexample:** phép kiểm **hiện đang đỏ**, đo lúc 28/09/2026 ngay sau khi
  task 02 xong:

  ```
  LECH LUOI: 112
     block 1    5 o, tier=NULL column=NULL
     ...
  TOTAL_TIER0 THEO ZONE:
     zone 1   total_tier0 = 0
     ...
  KET QUA: CON 118 DIEM LECH        -> exit 1
  ```

  Nên khi nó xanh thì con số đó có nghĩa.

## Receipt
