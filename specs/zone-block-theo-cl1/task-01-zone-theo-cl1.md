# Task 01 — Gán lại zone cho 29 block theo sheet CL1

Status: done

## Outcome

`block.zone_id` của toàn bộ 112 block cơ khí khớp sheet `CL1`, và 6 block đỗ nền
901–906 giữ nguyên zone. `BlockAllocator` từ đó chọn block đúng zone mà không
cần sửa mã nguồn.

## Scope

- **In:**
  - `TotalParking/Database/51_zone_theo_cl1.sql`: chụp bản sao `block` trước khi
    sửa, `UPDATE block SET zone_id` cho đúng 29 block **liệt kê tường minh**,
    kèm câu lệnh hoàn tác trong chú thích, và header ghi ràng buộc thứ tự chạy
    so với `14_zone_blocks_from_customer.sql`.
  - `tools/doi_chieu_zone_block.py`: đọc sheet `CL1` của
    `docs/TTP_LUMI_TYPE_BLOCK_RE01.xlsx` bằng thư viện chuẩn (`zipfile` +
    `xml.etree` — máy này **không có `openpyxl`**), so với CSDL đang chạy, in
    khác biệt, thoát khác 0 khi còn lệch. Hỗ trợ `--phan zone|o-do|luoi` và
    `--kiem-truoc-khi-xoa` (task 02 dùng các nhánh sau).
  - Chèn một dòng cảnh báo vào đầu `TotalParking/Database/14_zone_blocks_from_customer.sql`
    trỏ tới `51`/`52`.
  - **Bổ sung theo yêu cầu người dùng (28/09):** đổi tên zone theo công thức
    `Zone n (Hầm Bn CL1)` ở `zone.name` và ở hai danh sách viết cứng
    `TotalParking/Views/Home/OperationControl.cshtml`,
    `TotalParking/Views/Home/ZoneDetail.cshtml`. Không có bãi ngoài trời trong
    công trình này, nên các nhãn "Bãi ngoài trời A/B", "Khối cao tầng 1/2",
    "Tầng 1", "Tầng 2" đều sai và bị bỏ.
- **Out:** không đụng `slot_count`, `column_count`, `tier_count` (task 02);
  không đụng `led_panel_port`, `zone.polygon`, `gate_rank`, `vehicle_routing`,
  `canh_bao`; không đụng block `kind = 'Ground'`.

## Coverage

- CP-01, CP-02

## Ownership

- Create: `TotalParking/Database/51_zone_theo_cl1.sql`, `tools/doi_chieu_zone_block.py`
- Modify: `TotalParking/Database/14_zone_blocks_from_customer.sql` (chỉ thêm chú thích cảnh báo)
- Read: `docs/TTP_LUMI_TYPE_BLOCK_RE01.xlsx`,
  `TotalParking/Services/BlockAllocator.cs`, `TotalParking/Database/17_plc_slot_state.sql`

## Acceptance

- **AC-01:** `doi_chieu_zone_block.py --phan zone` báo **`LECH ZONE: 0`** và
  thoát 0.
- **AC-02:** `zone_id` của block 901–906 sau khi chạy **giống hệt** trước khi
  chạy; công cụ in riêng mục này để đối chiếu.

## Dependencies

- none

## Verification Plan

- **Command:** `python tools/doi_chieu_zone_block.py --phan zone`
- **Tiền đề (công cụ tự kiểm, thoát khác 0 nếu hỏng):**
  1. Đọc được sheet `CL1` và thấy **đúng 112 dòng** có số block. Đây là lá chắn
     chống chọn nhầm sheet: `CL2` có 91 dòng.
  2. Tổng `spaces qty` của CL1 bằng **764** — khớp bảng model in trên bản vẽ.
  3. Kết nối được CSDL `total_parking`.
- **Named probe:** `tools/doi_chieu_zone_block.py`, mục `LECH ZONE` và mục
  `BLOCK DO NEN`.
- **Reachability:** đã biết. `BlockAllocator.cs:54` lọc `b.zone_id = @zone_id`
  mỗi lần phân bổ; dữ liệu đọc lúc chạy, không nạp sẵn lúc khởi động.
- **Oracle:** `LECH ZONE: 0`, và bảng `BLOCK DO NEN` cho thấy 901–906 vẫn ở
  zone 1–6 như bản chụp trước khi chạy.
- **Counterexample:** ba chiều sai đều làm phép thử trượt.
  - Migration chưa chạy hoặc chạy thiếu → `LECH ZONE` > 0, liệt kê block còn lệch.
  - Chạy nhầm sheet `CL2` → tiền đề 1 chặn ở "đúng 112 dòng".
  - `UPDATE` quét cả block đỗ nền → mục `BLOCK DO NEN` lệch so với bản chụp.
- **Artifacts:** `specs/zone-block-theo-cl1/artifacts/zone-truoc-sau.txt` —
  bảng `block_no, zone_id` trước và sau, cùng đầu ra đầy đủ của lệnh.

### An toàn

- `UPDATE` phải liệt kê **tường minh 29 số block**, không dùng `BETWEEN` hay
  điều kiện suy diễn. Danh sách không liên tục — block 60 sang zone 4 còn 61
  sang zone 5 — nên mọi cách viết gọn đều có nguy cơ quét nhầm.
- Bản chụp `block` phải tạo **trước** câu `UPDATE` đầu tiên, trong cùng file.
- `UPDATE` một cột là nguyên tử nên task này không cần giao dịch; rủi ro giao
  dịch nằm ở task 02.
- Không chạy `tools/xoa_thanh_ghi_o_do.py` trong cửa sổ migration.

## Receipt

**Verification: PASS**

**Command:** `python tools/doi_chieu_zone_block.py --phan zone`

**Exit: 0**

**Base:** `16889b5f68676d83ac33659a74ac5a59f35b2c07`
**Head:** `16889b5f68676d83ac33659a74ac5a59f35b2c07`

```
TIEN DE: CL1 co 112 block, tong 764 o  ->  DAT

==============================================================
LECH ZONE: 0

BLOCK DO NEN (phai khong doi):
   block 901   zone 1   14 o
   block 902   zone 2   26 o
   block 903   zone 3   2 o
   block 904   zone 4   14 o
   block 905   zone 5   11 o
   block 906   zone 6   13 o

==============================================================
KET QUA: SACH
```

**AC-01 đạt:** `LECH ZONE: 0`, thoát 0. Trước khi chạy migration cùng lệnh này
báo `LECH ZONE: 29` và thoát 1 — phép kiểm có đỏ trước, nên số 0 ở trên có nghĩa.

**AC-02 đạt bằng phép đo mạnh hơn bản in.** Không chỉ đọc lại bảng đỗ nền, mà so
trực tiếp từng dòng với bản chụp `block_sao_luu_51` tạo trước câu `UPDATE` đầu tiên:

```
-- block do nen bi doi zone (phai rong)
(khong co dong nao)
-- so block that su doi zone (phai la 29)
29
```

Đúng 29 block đổi, 0 block đỗ nền đổi. Nếu câu `UPDATE` quét lan sang
`kind='Ground'` thì hai con số này đều lệch.

**Bằng chứng đầy đủ:** `specs/zone-block-theo-cl1/artifacts/zone-truoc-sau.txt`
(bảng `block_no, zone_id, kind` và bảng `zone` trước và sau).

**Người dùng xác nhận trên app đang chạy (28/09/2026):** tên zone hiển thị đúng
theo công thức `Zone n (Hầm Bn CL1)`. Đây là phần giao diện mà phép kiểm bằng
CSDL không chạm tới được — hai danh sách trong `OperationControl.cshtml` và
`ZoneDetail.cshtml` là chuỗi viết cứng, không đọc `zone.name`.

**Giới hạn:** phép kiểm chứng minh CSDL khớp CL1, **không** chứng minh CL1 khớp
thực địa. Giới hạn 2 và 3 trong `plan.md` vẫn nguyên — riêng block 61 và 62 nên
hỏi đội thi công xác nhận.
