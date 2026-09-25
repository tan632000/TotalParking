# Chặn đọc hụt mã thẻ khi ghi băng tải trọng
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-09-25, sửa sau C2)

- **Existing:**
  - `UpdateWeightBandAsync()` — `TotalParking/Services/Plc/PlcConnection.cs:522`
  - `_lastBandWritten` chống ghi lặp — `TotalParking/Services/Plc/PlcConnection.cs:67`
  - Ghi nhật ký lượt quẹt — `TotalParking/Services/Plc/PlcConnection.cs:539-543`
  - `CardCodeDecoder.IsEmpty()` chỉ coi TOÀN 0 là rỗng — `TotalParking/Services/Plc/CardCodeDecoder.cs:87-97`
  - Điểm gọi trong vòng poll — `TotalParking/Services/Plc/PlcConnection.cs:387`
- **Minimum change:** một điều kiện trong `UpdateWeightBandAsync()`: nếu đúng một
  trong hai word bằng 0 còn word kia khác 0 thì bỏ qua vòng này. **Không có trạng
  thái mới.** Một file.
- **Expansion signals:** không có. 1 file chạm (ngưỡng 8), 0 lớp mới (ngưỡng 2),
  1 hệ con (ngưỡng 3).
- **User decision (C1):** KEEP — chỉ sửa logic, không dựng hạ tầng kiểm thử.
- **User decision (C2):** **cắt bỏ luật "ổn định qua hai vòng poll"**; chỉ giữ
  luật chặn giá trị nửa vời; hành vi khi chặn là **giữ nguyên giá trị cũ**.

### Bằng chứng lỗi (đo trên hệ thật, toàn bộ nhật ký 22/09–25/09)

**5 lần đọc hụt, 3 block, 4 ngày — và cả 5 đều gây tín hiệu quá tải sai:**

| Thời điểm | Block | `D106` sau | Hậu quả `D1004` | Tồn tại |
|---|---|---|---|---|
| 22/09 20:58:52 | 75 | `0000 62A8` | `1 -> 3` | 4094 ms |
| 23/09 13:14:49 | 103 | `0000 9C25` | `1 -> 3` | 504 ms |
| 23/09 20:11:18 | 103 | `0000 A0AC` | `1 -> 3` | 510 ms |
| 24/09 14:26:45 | 64 | `0000 A0AC` | `1 -> 3` | 520 ms |
| 25/09 12:54:59 | 103 | `0000 A0F3` | `1 -> 3` | 513 ms |

Nguồn: `C:\Users\Admin\Documents\Web\totalParking\App_Data\plc_register_changes.log`.

Ví dụ đầy đủ (block 103, 25/09) — thẻ `a0f37660` là loại 2200kg, băng đúng là 1:

```
12:54:49.214  D106   0000 0000 -> 7660 A0F3   ma the: a0f37660
12:54:49.221  D1004  0 -> 1
12:54:59.897  D106   7660 A0F3 -> 0000 A0F3   ma the: a0f30000   <-- đọc hụt
12:54:59.899  D1004  1 -> 3                                      <-- quá tải SAI
12:55:00.410  D106   0000 A0F3 -> 7660 A0F3   ma the: a0f37660
12:55:00.412  D1004  3 -> 1
```

**Tần suất:** 5 lần trên 896 trạng thái `D106` khác rỗng trong 4 ngày (~0,6%).
Riêng ngày 25/09 có 187 lượt quẹt toàn hệ thống.

**Cơ chế:** ladder ghi **hoặc xoá** hai word ở hai chu kỳ khác nhau. Cả 5 lần đo
được đều có dạng `0000 XXXX` — word thấp bằng 0, tức ladder đang **xoá** word
thấp trước. Đọc FINS nguyên tử ở mức giao thức vẫn bắt trúng được trạng thái dở.

### Vì sao cắt luật "ổn định hai vòng" (quyết định C2)

Đo bền của từng trạng thái `D106` khác rỗng trên toàn bộ nhật ký:

```
Tổng trạng thái khác rỗng        : 896
Chỉ tồn tại đúng 1 nhịp poll     :  47
   ├─ đọc hụt (nửa vời)          :   4   <- luật 2 chặn ĐÚNG
   └─ mã thẻ đầy đủ, hợp lệ      :  43   <- luật 2 chặn NHẦM
```

Luật 2 đánh rơi **43 lượt quẹt thật** để chặn **4 lần đọc hụt** — tỉ lệ nhầm
10:1. Nó cũng không bắt được lần đọc hụt ở block 75 vì lần đó kéo dài 8 nhịp.
Luật 1 một mình bắt **5/5** và không đụng thẻ đăng ký nào (đã kiểm 610/610 thẻ).

## Out of scope

- Không dựng project test / `.sln` (quyết định C1).
- Không thêm luật "ổn định qua hai vòng poll" (quyết định C2).
- **Không vá luồng tìm xe `D1002`** (quyết định C2) — mặc dù nó dính **cùng một
  lỗi**, có bằng chứng: block 99, 23/09 20:40:12,
  `FEE0 62B2 -> 0000 62B2 -> 0000 0000`; và block 73 kẹt ở `0000 62B2` khoảng 4
  tiếng ngày 22/09. Để lại cho vòng sau.
- Không đổi nhịp poll, không đổi `WeightBandService`.
- Không dọn yêu cầu tìm xe kẹt ở `D1002` block 62.
- Không sửa 4 mã thẻ nằm ngoài vùng ô đỗ khai báo (block 25/27/87/106).

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | `D1004` không nhận băng tính từ một giá trị `D106` nửa vời | thêm một điều kiện thoát sớm | `PlcConnection.UpdateWeightBandAsync`, thanh ghi `D1004` trên PLC thật | rõ; C2 đã chốt hành vi là giữ nguyên giá trị cũ | critical — ghi xuống thiết bị thật, ảnh hưởng xử lý xe theo tải trọng | live (dựng lại đọc hụt trên PLC thật) |
| CP-02 | Lượt quẹt bình thường vẫn ghi băng đúng như trước | không đổi hành vi | cùng hàm trên | rõ | elevated — nguy cơ chặn nhầm mã thẻ hợp lệ | live (bước 1 của probe) |
| CP-03 | Nhật ký vẫn ghi lại được cả giá trị nửa vời | vị trí chèn | `PlcRegisterLog.Track` tại `PlcConnection.cs:539-543` | rõ | elevated — mất khả năng quan sát lỗi này về sau | live (dòng `D106` xuất hiện trong nhật ký ở bước 2) |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | Khi `D106`/`D107` ở dạng đúng một word bằng 0 còn word kia khác 0, hệ thống **không** được ghi `D1004`; giá trị cũ giữ nguyên. | `python tools/kiem_chung_doc_hut.py --block <N>` bước 2 |
| AC-02 | Khi `D106` giải mã ra mã thẻ đầy đủ, hệ thống vẫn ghi băng đúng như trước khi sửa. | cùng lệnh, bước 1 |
| AC-03 | Khi gặp giá trị nửa vời, `PlcRegisterLog` vẫn ghi được dòng `D106` tương ứng. | cùng lệnh, kiểm nhật ký sau bước 2 |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Bỏ qua vòng poll khi `D106` ở trạng thái nửa vời | AC-01, AC-02, AC-03 | `TotalParking/Services/Plc/PlcConnection.cs` | - | done |

## Giới hạn đã biết (nêu trước, không giấu tới C3)

1. **Không có kiểm thử đơn vị** (quyết định C1). Bằng chứng là dựng lại đọc hụt
   trên PLC thật — chứng minh hành vi lúc chạy của bản đã triển khai, không
   chứng minh từng nhánh logic.
2. **Không bắt được đọc hụt khi cả hai word đều khác 0** (thẻ cũ lẫn thẻ mới
   trộn nhau). Chưa từng quan sát thấy trong 896 trạng thái; luật 2 vốn để che
   nhánh này đã bị cắt ở C2 vì cái giá quá đắt.
3. **Giả định miền giá trị:** không thẻ hợp lệ nào có nửa word bằng 0. Đã kiểm
   610/610 thẻ đăng ký. Đường nhập thẻ (`TotalParking/Services/CardImportParser.cs`)
   **không có chốt** ngăn tạo mã như vậy về sau. Chạy kiểm trước khi triển khai:
   ```sql
   SELECT card_code FROM parking_card
   WHERE is_active = 1 AND (card_code LIKE '0000%' OR card_code LIKE '%0000');
   ```
   Không rỗng thì phải quay lại C1.
4. **Khi giữ nguyên giá trị cũ**, trạng thái nửa vời kéo dài (đã đo 4094 ms ở
   block 75) làm `D1004` giữ băng của lượt quẹt trước trong khoảng đó. C2 đã
   chọn hành vi này; phương án ghi `0` đã được cân nhắc và loại.
5. **Phép kiểm chứng ghi vào `D106` của block đang trong vòng poll**, đi ngược
   cảnh báo ở `tools/plc_register.py:38-40` (hai kết nối FINS có thể làm lệch
   khung tin). Xử lý bằng điều kiện hợp lệ trong Verification Plan, không bỏ qua.

## Review log

- **Round 1 (2026-09-25)** — hai người rà soát ngữ cảnh mới, 10 phát hiện sau khử trùng lặp.
  - **Chấp nhận:** cắt luật "ổn định hai vòng" (Critical, số đo 43 nhầm / 4 đúng
    — đã tự đo lại và xác nhận); sửa tần suất `1/53 ~2%` thành `5/896 ~0,6%`;
    sửa mô tả cơ chế từ "ghi nửa chừng" thành "ghi hoặc xoá nửa chừng"; dùng
    đường dẫn tuyệt đối tới nhật ký ở thư mục deploy; thêm bước build + deploy;
    chốt vị trí chèn sau `PlcRegisterLog.Track`; ghi giả định miền giá trị mã
    thẻ vào Giới hạn; thêm điều kiện hợp lệ cho probe.
  - **Tự tiêu sau khi cắt luật 2:** kịch bản `D1004` kẹt 6,15 giây; probe không
    làm trượt được bản thiếu luật 2; nguy cơ tái dùng `_lastSeenRaw` phá chốt
    chống lặp của luồng tìm xe; nhu cầu thêm field và điểm reset.
  - **Loại:** đề xuất ghi `0` thay vì giữ giá trị cũ — người dùng chọn giữ giá
    trị cũ ở C2.
  - **Chuyển sang vòng sau:** `D1002` dính cùng lỗi (người dùng quyết ở C2).
  - Sweep: đã rà lại toàn bộ packet; CP-03 và AC-03 là hàng mới sinh từ phát
    hiện về vị trí chèn; AC cũ về ổn định hai vòng đã bị gỡ.
