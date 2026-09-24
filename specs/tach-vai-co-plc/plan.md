# Tách vai hai cờ trạng thái PLC
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-09-24)

- **Existing:** hai cột đã tồn tại và đã đúng vai —
  `plc_device.is_active` lọc PLC được poll ở `TotalParking/Services/PlcDeviceRepository.cs:39`;
  `block.is_active` là cổng vận hành, chi phối `TotalParking/Services/BlockAllocator.cs:54`
  và **5** view: `v_block_map`, `v_led_capacity`, `v_led_capacity_zone`,
  `v_slot_available`, `v_zone_capacity`.
  Bộ dò PLC sống có sẵn ở `TotalParking/Services/Plc/PlcReachabilityScanner.cs`;
  nạp lại nóng qua `POST /PlcStatus/Reload`; trạng thái kết nối đã có trong bộ
  nhớ ở `PlcConnection.IsOnline`.
- **Minimum change:** gỡ `AND b.is_active = 1` khỏi truy vấn nạp vòng poll; thêm
  `is_connected` để truy vấn được bằng SQL; tự hạ và tự bật cổng vận hành theo
  trạng thái kết nối; bật poll cho mọi PLC đã khai báo.
- **Expansion signals:** màn hình quản trị bật/tắt theo lô là UI mới — bị loại.
- **User decision: KEEP** — làm cả ba việc, không thêm màn hình quản trị.

### Quyết định C2 làm đổi hình dạng packet

Sau vòng review, người dùng chốt thêm hai điều làm packet khác bản C1:

1. **Tự hạ `block.is_active` khi PLC chết lâu** (phát hiện F4). Điều này **trái
   với nguyên tắc "cổng vận hành do người quyết"** mà bản C1 nêu. Tôi đã trình
   bày đánh đổi và người dùng giữ quyết định, nên packet thực hiện nó — kèm ba
   lớp an toàn ở task 03.
2. **Chấp nhận việc poll ghi `D1004`** (phát hiện F1), với tiền đề bắt buộc: xác
   nhận hiện trường trước khi bật một khối mới vào vòng poll.

### Đính chính một luận cứ sai của bản C1

Bản C1 viết: *"poll một khối chưa nghiệm thu không ghi gì xuống nó"*. **Sai.**

`TotalParking/Services/Plc/PlcConnection.cs:67` khởi tạo `_lastBandWritten = -1`,
nên nhịp poll đầu tiên của **mỗi kết nối mới** luôn ghi `D1004` một lần
(`PlcConnection.cs:558-564`). Đo trên nhật ký thật `App_Data/plc_audit.log`:

```text
tong dong WRITE D1004      : 3083
trong do D106 = 0000 0000  : 2285
17:56:09  block 90  D1004 <- 0   (dung khoi vua duoc bat vao poll)
```

Ngoài ra `PlcConnection.cs:580` đặt lại `-1` sau mỗi lỗi, nên PLC chập chờn ghi
lại mỗi lần hồi phục; và `Reload()` không gọi `ClearStaleAnswersAsync`
(`PlcConnectionManager.cs:100-157` so với `PlcHost.cs:91`), nên khối vừa vào
poll giữ nguyên `D1000` rác cho tới lượt quẹt đầu tiên.

**Hệ quả:** bật poll một khối **là** ghi xuống nó. Đó là lý do tiền đề xác nhận
hiện trường ở task 04 là bắt buộc, không phải thủ tục.

### Số đo mở packet (17:44 ngày 24/09)

| | Số khối | Số ô |
|---|---|---|
| `plc_active = 1`, `block_active = 1` | 107 | 723 |
| `plc_active = 0`, `block_active = 1` | 5 | 32 |

Năm khối còn lại (`14 20 28 57 66`) đều ping được và mở cổng 9600.

Đo lúc 17:56 còn cho thấy **CSDL 107 nhưng vòng poll chỉ giữ 87** — 20 PLC đã
bật trong CSDL mà chưa gọi `Reload`, nên chưa thực sự được giám sát.

## Out of scope

- Màn hình quản trị bật/tắt tầng vận hành theo lô.
- Dọn thẻ rác trong `D200/D300/D400` (cần người xác nhận tại chỗ ô trống).
- Đổi thuật toán xếp xe hay công thức sức chứa.
- `v_zone_coverage` không lọc cờ nào nên độ phủ vẫn đếm cả khối đã tắt vận hành —
  chấp nhận, không sửa trong packet này.
- 6 block đỗ thường (`block_no 901-906`) không có `plc_device`: chúng lấy số từ
  cảm biến PGS qua CCU, xem packet `specs/pgs-doc-ccu`.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Truy vấn nạp poll chỉ còn phụ thuộc `plc_device.is_active` | sửa truy vấn | vòng poll | đã rõ | elevated | source: grep xác nhận + live: transaction rollback phân biệt hai truy vấn |
| CP-02 | `is_connected` phản ánh đúng trạng thái, chỉ ghi khi đổi, không giết vòng poll | cột mới, dịch vụ nền | CSDL, vòng poll | đã rõ — người dùng chốt lưu vào bảng | elevated — ghi sai nhịp thì đốt CSDL; đặt sai chỗ thì chết vòng poll | live: ép một chuyển trạng thái thật rồi khôi phục |
| CP-03 | Khối có PLC chết lâu tự rời vận hành; sống lại ổn định thì tự vào lại | cột mới, dịch vụ nền | `BlockAllocator`, 5 view, bảng LED | đã rõ — người dùng chốt ở C2 | **critical** — tự đổi sức chứa ngoài hiện trường | live: ép chuyển trạng thái, đo cả hai chiều, có trần chống dao động |
| CP-04 | Mọi PLC đã khai báo đều nằm trong vòng poll | dữ liệu + thao tác | vòng poll, thiết bị thật | đã rõ | **critical** — bật poll là ghi `D1004` xuống thiết bị | live: đếm khối chưa poll về 0, sau khi có xác nhận hiện trường |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When nạp danh sách thiết bị, truy vấn shall không còn tham chiếu `b.is_active`, và trong một transaction hạ cờ thử, truy vấn mới shall giữ khối trong khi truy vấn cũ loại nó. | `node specs/tach-vai-co-plc/verify-tach-vai.mjs` |
| AC-02 | When trạng thái kết nối của một PLC đổi thật, `is_connected` và `connected_changed_at` shall đổi theo trong vòng một chu kỳ ghi. | `node specs/tach-vai-co-plc/verify-is-connected.mjs` |
| AC-03 | When trạng thái không đổi, `connected_changed_at` shall giữ nguyên; và tổng số lần ghi trong cửa sổ quan sát shall không vượt trần cấu hình. | `node specs/tach-vai-co-plc/verify-is-connected.mjs` |
| AC-04 | When lệnh ghi CSDL hỏng, vòng poll shall tiếp tục đọc PLC và shall ghi lại lỗi, không dừng. | `node specs/tach-vai-co-plc/verify-is-connected.mjs` |
| AC-05 | When một PLC mất kết nối quá ngưỡng, hệ thống shall hạ `block.is_active = 0` cho khối đó, ghi nhật ký, và khối shall biến mất khỏi sức chứa. | `node specs/tach-vai-co-plc/verify-tu-ha-bat.mjs` |
| AC-06 | When PLC đó kết nối lại ổn định quá ngưỡng, hệ thống shall bật `block.is_active = 1` trở lại. | `node specs/tach-vai-co-plc/verify-tu-ha-bat.mjs` |
| AC-07 | When số lần tự đổi cờ của một khối vượt trần trong ngày, hệ thống shall ngừng tự đổi khối đó và chờ người xử lý. | `node specs/tach-vai-co-plc/verify-tu-ha-bat.mjs` |
| AC-08 | When đã bật poll cho mọi PLC khai báo, số khối trong vòng poll shall bằng số dòng `plc_device.is_active = 1`. | `node specs/tach-vai-co-plc/verify-poll-du.mjs` |
| AC-09 | When đo bất kỳ tiêu chí nào ở trên, `bin\TotalParking.dll` ở cây mã nguồn và bản deploy shall trùng dấu vân tay. | mọi script |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Truy vấn poll thôi phụ thuộc cờ vận hành | AC-01, AC-09 | `TotalParking/Services/PlcDeviceRepository.cs`, `specs/tach-vai-co-plc/verify-tach-vai.mjs` | - | done |
| 02 | `is_connected` ghi an toàn vào CSDL | AC-02, AC-03, AC-04, AC-09 | `TotalParking/Database/42_is_connected.sql`, `TotalParking/Services/PlcDeviceRepository.cs`, `TotalParking/Services/Plc/PlcTrangThaiWriter.cs`, `TotalParking/Services/Plc/PlcHost.cs`, `specs/tach-vai-co-plc/verify-is-connected.mjs` | task-01-tach-vai.md | done |
| 03 | Tự hạ và tự bật cổng vận hành theo kết nối | AC-05, AC-06, AC-07, AC-09 | `TotalParking/Database/43_tu_ha_bat.sql`, `TotalParking/Services/Plc/CongVanHanhService.cs`, `TotalParking/Controllers/PlcStatusController.cs`, `specs/tach-vai-co-plc/verify-tu-ha-bat.mjs` | task-02-is-connected.md | done |
| 04 | Đưa mọi PLC đã khai báo vào vòng poll | AC-08, AC-09 | `TotalParking/Database/44_poll_du.sql`, `specs/tach-vai-co-plc/verify-poll-du.mjs` | task-03-tu-ha-bat.md | done |

Task 04 tách riêng khỏi task 01 vì một bên là thay đổi mã, một bên là thay đổi dữ
liệu làm đổi tập thiết bị được kết nối. Gộp lại thì khi có sự cố không biết do
bên nào, và rollback không đối xứng: revert DLL không tắt lại PLC đã bật.

## Review log

- **Round 1 (24/09):** hai reviewer ngữ cảnh mới — Fact Checker + Contract
  Verifier, và Failure-mode + Assumption destroyer + Scope critic. 18 phát hiện
  thô, gộp trùng còn 15. **Người dùng nhận toàn bộ 15.**
  - **1 phát hiện bị bác bỏ bằng bằng chứng:** "6 block có sức chứa mà không có
    PLC" — đó là 6 block đỗ thường, lấy số từ cảm biến PGS qua CCU (packet
    `specs/pgs-doc-ccu`), reviewer không biết việc đó.
  - **Phát hiện nặng nhất (F1)** lật đổ luận cứ an toàn của bản C1 và đã được
    kiểm chứng độc lập bằng 2285 dòng nhật ký thật — xem mục đính chính ở trên.
  - Sweep: sửa 6 view thành 5; đổi phép kiểm AC-01 sang transaction rollback
    thay vì hạ cờ thật trên hệ thống đang chạy; tách task 04; thêm task 03 theo
    quyết định C2; viết lại CP từ 4 và AC từ 5 thành 9.
- Round 2: chưa chạy. Theo B4, phát hiện sau vòng này phải có bằng chứng lúc chạy.
