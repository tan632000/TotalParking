# Task 02 — Sửa số ô đỗ và đồng bộ `plc_slot_state`

Status: done

## Outcome

`block.slot_count` của 112 block cơ khí khớp sheet `CL1` (tổng 764 ô), và số
dòng `plc_slot_state` khớp `slot_count` với `word_addr` đúng thứ tự — vá điểm mù
khiến SCADA không đọc những ô nó không biết là có tồn tại.

`column_count` / `tier_count` **đã tách sang task 03** vì còn ẩn số hình học;
xem phần Giới hạn của Receipt.

## Scope

- **In:** `TotalParking/Database/52_so_o_theo_cl1.sql`
  - Chụp bản sao `block` và `plc_slot_state` **trước** khi mở giao dịch.
  - `START TRANSACTION` bao trọn `DELETE` → `INSERT` → `UPDATE` → `COMMIT`.
  - **Xoá** dòng thừa ở block 6, 33, 88 bằng câu `DELETE` mang sẵn ba vị từ an
    toàn, rồi kiểm `ROW_COUNT() = 15`, sai thì `ROLLBACK`.
  - **Thêm** dòng cho block 5, 29, 87, 89 với `word_addr` đúng vị trí.
  - `UPDATE block SET slot_count` cho 7 block, liệt kê tường minh.
  - Câu lệnh hoàn tác dạng `INSERT … SELECT … ON DUPLICATE KEY UPDATE` trong chú thích.
  - Header ghi ràng buộc thứ tự so với `14_zone_blocks_from_customer.sql`.
- **Out:** không đụng `zone_id` (task 01), `column_count`/`tier_count` (task 03),
  `bay_length_mm`, `led_panel_port`, `canh_bao`, `vehicle_routing`, và block
  `kind = 'Ground'`.

## Coverage

- CP-03, CP-04, CP-05, CP-06

## Ownership

- Create: `TotalParking/Database/52_so_o_theo_cl1.sql`
- Read: `docs/TTP_LUMI_TYPE_BLOCK_RE01.xlsx`, `tools/doi_chieu_zone_block.py`,
  `TotalParking/Database/17_plc_slot_state.sql`,
  `TotalParking/Database/21_block_map_view.sql`,
  `TotalParking/Database/05_parking_topology.sql`,
  `TotalParking/Services/BlockAllocator.cs`,
  `TotalParking/Services/Plc/SlotOccupancyReader.cs`

## Acceptance

- **AC-03:** `--phan o-do` báo **`LECH SO O DO: 0`** và `SUM(slot_count)` của
  block cơ khí bằng **764**.
- **AC-04:** cùng lệnh báo **`LECH DONG BO O DO: 0`**, đọc từ
  `v_block_map.lech_so_o` (`TotalParking/Database/21_block_map_view.sql:46`, đã
  lọc sẵn `kind='Mechanical'`), cộng phép kiểm `word_addr` theo `slot_index`
  khớp tiền tố `400,202,204,206,208,300,302,304,306,308`.
- **AC-05:** `--kiem-truoc-khi-xoa` liệt kê mọi dòng sắp xoá **không** thoả cả ba
  điều kiện an toàn; khi danh sách khác rỗng, lệnh thoát khác 0.

## Dependencies

- `task-01-zone-theo-cl1.md`

## Verification Plan

- **Command**: `python tools/doi_chieu_zone_block.py --phan o-do`
- **Tiền đề (công cụ tự kiểm, thoát khác 0 nếu hỏng):**
  1. Sheet `CL1` có **đúng 112 dòng** có số block và tổng `spaces qty` = **764**.
  2. `--kiem-truoc-khi-xoa` trả về rỗng. Đo ngày 28/09 là rỗng, nhưng trạng thái
     này **đổi được** — phải chạy lại ngay trước khi áp migration.
- **Named probe:** `tools/doi_chieu_zone_block.py`, các mục `LECH SO O DO`,
  `LECH DONG BO O DO` (nguồn: `v_block_map.lech_so_o`), `SAI WORD_ADDR`.
- **Reachability:** đã biết.
  - `BlockAllocator.cs:45` đọc `b.slot_count` mỗi lần phân bổ.
  - `v_zone_capacity` cộng `b.slot_count`
    (`TotalParking/Database/50_zone_capacity_ground_sensor.sql:39`).
  - Vòng quét ô đỗ nạp bản đồ ô **từ chính `plc_slot_state`**
    (`SlotOccupancyReader.cs`), chỉ `UPDATE`, **không `INSERT`/`DELETE`** — nên
    nó không hoàn tác thay đổi này, nhưng **có ghi song song** (xem An toàn).
- **Oracle:** `LECH SO O DO: 0` **và** `LECH DONG BO O DO: 0` **và** tổng 764.
  Phải đạt tất cả: sửa `slot_count` mà quên `plc_slot_state` sẽ làm hai nguồn
  nói khác nhau.
- **Counterexample:** bốn chiều sai đều làm phép thử trượt.
  - Không chạy migration → `LECH SO O DO` > 0.
  - Chỉ `UPDATE slot_count`, quên `plc_slot_state` → `LECH DONG BO O DO` > 0.
  - Thêm dòng sai thứ tự `word_addr` → `SAI WORD_ADDR` > 0 kèm block cụ thể.
  - Xoá ô không an toàn → `ROW_COUNT() <> 15` → `ROLLBACK`, không dòng nào mất.
- **Artifacts:** `specs/zone-block-theo-cl1/artifacts/so-o-truoc-sau.txt` —
  `block_no, slot_count, column_count, tier_count, số dòng plc_slot_state` trước
  và sau, kết quả `--kiem-truoc-khi-xoa`, số dòng thực tế bị xoá và được thêm,
  và đầu ra đầy đủ của lệnh.

### An toàn — phần phá huỷ

Task này xoá 15 dòng `plc_slot_state` (block 6 ô 7–10, block 33 ô 7–10, block 88
ô 4–10). Ba biện pháp, theo thứ tự quan trọng:

**1. Ba vị từ nằm TRONG câu `DELETE`, không kiểm trước rồi mới xoá.**

```sql
DELETE s FROM plc_slot_state s JOIN block b USING(block_id)
WHERE ( (b.block_no = 6  AND s.slot_index > 6)
     OR (b.block_no = 33 AND s.slot_index > 6)
     OR (b.block_no = 88 AND s.slot_index > 3) )
  AND s.card_code  IS NULL                         -- không giữ thẻ
  AND s.read_at    >= NOW() - INTERVAL 5 MINUTE    -- dữ liệu còn tươi
  AND s.changed_at IS NULL;                        -- chưa từng đổi trạng thái
-- ROW_COUNT() <> 15  ->  ROLLBACK
```

- Vị từ `read_at` là **bắt buộc**: khi PLC rớt mạng, `SlotOccupancyReader.cs:105-115`
  bắt lỗi rồi `continue`, **không đụng** `card_code` và `read_at`. Một xe được
  cất bằng HMI trong lúc PLC mất kết nối sẽ để lại `card_code` NULL cũ — kiểm
  bằng `card_code` một mình sẽ cho qua và xoá mất dấu chiếc xe đó. Ngưỡng 5 phút
  lấy đúng theo `BlockAllocator.cs:32` (`PlcFreshMinutes = 5`).
- Kiểm **trước** rồi xoá **sau** là cửa sổ TOCTOU: vòng quét 45 giây
  (`plc:slotScanMs`) ghi `card_code` bằng connection riêng, autocommit, nằm
  ngoài giao dịch của migration.

**2. Giao dịch bao trọn `DELETE` → `INSERT` → `UPDATE`.**

Nếu `DELETE` xong mà `UPDATE` hỏng, block 6 sẽ có `slot_count = 10` nhưng chỉ
còn 6 dòng `plc_slot_state`. `BlockAllocator.cs:44-50` tính chỗ trống từ
`slot_count` → phát 4 suất vào 4 ô mà SCADA không bao giờ đọc được.

**3. Bản chụp tạo TRƯỚC khi mở giao dịch.**

`CREATE TABLE … AS SELECT` gây **implicit COMMIT** trong MySQL, nên không được
đặt trong giao dịch. Nó cũng **không giữ** `PRIMARY KEY (block_id, slot_index)`,
`KEY ix_slot_state_card` và `CONSTRAINT fk_slot_state_block`
(`TotalParking/Database/17_plc_slot_state.sql:60-63`) — vì vậy hoàn tác **không
thể** dùng `RENAME TABLE`, phải là:

```sql
INSERT INTO plc_slot_state SELECT * FROM <ban_chup>
ON DUPLICATE KEY UPDATE word_addr = VALUES(word_addr), card_code = VALUES(card_code),
    raw_words = VALUES(raw_words), read_at = VALUES(read_at), changed_at = VALUES(changed_at);
```

Bản cài đặt dùng `CREATE TABLE … LIKE` + `INSERT … SELECT` thay cho
`CREATE TABLE … AS SELECT`, nên bản chụp **giữ được** khoá chính. Khoá ngoại thì
vẫn không, nên kết luận "không dùng `RENAME TABLE`" giữ nguyên.

### Ràng buộc khác

- Danh sách 7 block phải viết **tường minh**, không suy diễn từ `slot_count` hiện tại.
- Không chạy `tools/xoa_thanh_ghi_o_do.py` trong cửa sổ migration — nó ghi xuống
  PLC và sẽ phá giá trị bằng chứng của `changed_at`.
- Nếu `14_zone_blocks_from_customer.sql` được chạy lại, **phải chạy lại `51` và
  `52`** — file 14 có `DELETE FROM block` rồi `INSERT` cứng `zone_id`/`slot_count`.

## Receipt

Verification: PASS
Command: python tools/doi_chieu_zone_block.py --phan o-do
Exit: 0
Base: cd91a0b8b7df337f63d3cc96c5d2e793baff4da0
Head: c4c53446d643872a42324d7296f9f06955880dc7328e0f525364fcf031d0fa53

```
TIEN DE: CL1 co 112 block, tong 764 o  ->  DAT

==============================================================
LECH SO O DO: 0
TONG O CO KHI: 764  (phai la 764)
LECH DONG BO O DO: 0
SAI WORD_ADDR: 0

==============================================================
KET QUA: SACH
```

**AC-03 và AC-04 đạt.** Trước khi chạy migration cùng lệnh báo
`LECH SO O DO: 7`, `TONG O CO KHI: 755` và thoát 1 — phép kiểm đã đỏ trước.

**AC-05 đạt bằng phép kiểm âm tính thật, không phải bằng lập luận.**
Chốt an toàn được chạy ngay sát lúc áp migration:

```
DONG SAP XOA: 15  (ky vong 15)
DONG KHONG AN TOAN: 0
KET QUA: SACH                 -> thoat 0
```

Sau đó chạy **lại** migration khi 15 dòng đã bị xoá, tức điều kiện an toàn không
còn thoả. Cơ sở dữ liệu chặn lại:

```
mysql tra ve: ERROR 1644 (45000) tai dong 176: HUY: DELETE khong xoa
dung 15 dong.
Chay tools/doi_chieu_zone_block.py --kiem-truoc-khi-xoa. Khong dong nao bi mat.
Ma thoat cua mysql: 1
```

Dữ liệu trước và sau lần chạy bị chặn: `tong_o=764 tong_dong=764` giữ nguyên.
Giao dịch huỷ sạch, không dòng nào mất.

**Phép đếm độc lập với công cụ**, so trực tiếp với bản chụp:

```
da_xoa  = 15     (dong co trong ban chup, khong con trong bang song)
da_them = 24     (dong co trong bang song, khong co trong ban chup)
dong CU bi doi changed_at hoac card_code:  (rong)
755 - 15 + 24 = 764
```

`card_code`, `read_at`, `changed_at` của 16 dòng cũ ở block 5/29/87/89 đều
nguyên vẹn — `ON DUPLICATE KEY UPDATE` chỉ ghi `word_addr`.

**Bằng chứng đầy đủ:** `specs/zone-block-theo-cl1/artifacts/so-o-truoc-sau.txt`.

### Một lỗi đã phát hiện và sửa trong lúc thực thi

Bản đầu của cả `51` và `52` chụp sao lưu bằng
`CREATE TABLE IF NOT EXISTS … LIKE` rồi `INSERT IGNORE … SELECT *`. `INSERT
IGNORE` bỏ qua dòng trùng khoá, nên bản chụp gốc **không** bị ghi đè — lập luận
đó đúng nhưng không đủ. Lần chạy thứ hai (chính là phép kiểm âm tính ở trên) đã
đẩy thêm **24 dòng mới sinh** vào bản chụp, nâng nó từ 755 lên 779 dòng. Câu
hoàn tác `DELETE … WHERE NOT EXISTS (… trong bản chụp)` khi đó sẽ coi 24 dòng
mới là dữ liệu gốc và không xoá chúng — tức **hoàn tác đã hỏng mà không báo gì**.

Phát hiện được là nhờ phép đếm độc lập ở trên trả về `da_them = 0` thay vì 24.

Đã sửa: chỉ chụp khi bảng sao lưu còn rỗng.

```sql
SET @da_chup := (SELECT COUNT(*) FROM plc_slot_state_sao_luu_52);
INSERT IGNORE INTO plc_slot_state_sao_luu_52
SELECT * FROM plc_slot_state WHERE @da_chup = 0;
```

Bản chụp đã bị bẩn được dọn lại đúng 755 dòng. Chạy migration lần thứ ba xác
nhận bản vá: bản chụp **giữ nguyên 755** trong khi lệnh vẫn bị chặn đúng cách.

### Giới hạn

- **`column_count` / `tier_count` vẫn NULL ở toàn bộ 112 block**, nên
  `v_zone_capacity.total_tier0` vẫn bằng 0 ở cả 6 zone và luật định tuyến
  `2600KG` vẫn chưa chạy được. Đây là task 03, đang chờ bên thiết kế.
- Phép kiểm chứng minh cơ sở dữ liệu khớp CL1, **không** chứng minh CL1 khớp
  thực địa.
