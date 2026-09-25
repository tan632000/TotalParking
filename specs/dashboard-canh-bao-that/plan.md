# Dashboard hiện cảnh báo thật, bỏ bộ sinh ngẫu nhiên
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 25/09/2026)

- **Existing:**
  - `TotalParking/Controllers/AlarmController.cs:42-54` — `GET /Alarm/List` trả
    `nguon`, `muc_do`, `ma_loi`, `block_no`, `zone_id`, `thiet_bi`, `mo_ta`,
    `xay_ra_luc` (dạng `yyyy-MM-dd HH:mm:ss`), `chua_xac_nhan`. Đủ mọi thứ cần.
  - `TotalParking/Views/Home/Alarms.cshtml` — khuôn tự làm mới nhịp 15 giây,
    phân biệt trạng thái rỗng với trạng thái lỗi, nút xác nhận có hỏi tên.
  - `TotalParking/Views/Home/Index.cshtml:1179-1187` — ba bộ đếm đã phân theo
    `type` 1/2/3, đúng ba nhóm mà bảng `canh_bao` dùng.

- **Minimum change:** một file (`Index.cshtml`) + một script kiểm chứng.

- **User decision: KEEP** — chỉ sửa Dashboard; **để nguyên khoá**
  `localStorage.activeAlarms`, nhưng **dọn một lần** phần tử giả trong đó.

### Sáu nguồn nội dung bịa, không phải một

| Dòng | Nội dung |
|---|---|
| `:987-1009` | `operatorErrorsPool`, `systemFaultsPool`, `maintenanceDuePool` |
| `:1012-1020` | `getRandomItem`, `formatCurrentTime` — chỉ phục vụ đường giả |
| `:1023-1035` | `initAlarms()` đọc `localStorage` rồi **`return` sớm ở `:1031`** |
| `:1060-1082` | Bộ sinh thứ năm: cảnh báo `MNT-DUE-100` suy từ `localStorage.palletCycleData` |
| `:1085-1113` | Ba cảnh báo bịa chèn khi `activeAlarms.length < 3` |
| `:1228-1295` + `:1298-1309` | `addRealtimeAlarm()` và `setInterval` 15 giây gọi nó |

Đo được: bảng `canh_bao` có **16 dòng, toàn `nguon = hardware`, tất cả chưa xác
nhận**; **0 dòng** nào mang mã `SYS-*`. Hệ thống dùng **Omron FINS/TCP**, không
dùng Modbus TCP — nên câu *"Mất kết nối truyền thông Modbus TCP với PLC chính"*
không thể là cảnh báo thật.

### Hai khối nguy hiểm nhất không chứa chuỗi cấm nào

`:1023-1035` và `:1060-1082` **không** chứa `SYS-FLT-`, `OP-ERR-`,
`addRealtimeAlarm` hay tên kho câu nào. Gỡ bốn khối còn lại mà để chúng lại thì
mọi phép kiểm tĩnh vẫn xanh, trong khi:

- `:1031` `return` sớm làm `fetch('/Alarm/List')` **không bao giờ chạy** nếu máy
  người dùng đã có sẵn `localStorage.activeAlarms` — mà màn hình tường thì chắc
  chắn có, do chính bộ sinh 15 giây ghi suốt nhiều ngày;
- `:1060-1082` tiếp tục đẻ cảnh báo `MNT-DUE-100` từ `palletCycleData`.

### `saveActiveAlarms` KHÔNG nằm trong `Index.cshtml`

Đo được: **0 lần** trong `Index.cshtml`. Hàm cùng tên chỉ tồn tại ở
`OperationControl.cshtml:618` với bốn nơi gọi (`:1053`, `:1402`, `:1432`,
`:1690`), trong đó `:1053` là **đúng đường ghi bản ghi E-Stop** mà ma trận an
toàn của `Safety.cshtml:256` dò tìm.

**Tuyệt đối không đụng `OperationControl.cshtml`.** Trong `Index.cshtml` chỉ có
ba câu `localStorage.setItem('activeAlarms', …)` trần, ở `:1115`, `:1219`,
`:1276`.

### Một tập duy nhất để đếm: CHƯA XÁC NHẬN

`CanhBaoRepository.cs:70-77` trả **tất cả** dòng chưa xác nhận **cộng** tối đa
200 dòng đã xác nhận gần nhất. Nên `COUNT(*) GROUP BY nguon` **không** bằng số
dòng bảng.

Bảng, ba bộ đếm và biểu ngữ **đều tính trên cùng một tập**: `canh_bao[]` đã lọc
`chua_xac_nhan === true` phía trình duyệt. Trường đó có sẵn trong thân JSON
(`AlarmController.cs:54`) nên không cần đổi endpoint.

Trộn hai tập là tạo ra đúng cảnh "bộ đếm 14 đứng trên bảng 2 dòng" — loại lỗi
packet này viết ra để diệt.

### Dọn rác một lần, giữ lại bản ghi của trang khác

`Index.cshtml` hiện là nơi **duy nhất** giới hạn mảng (`:1272-1274` cắt còn 8
phần tử) và ghi đè nó mỗi lần tải trang. `OperationControl.cshtml` chỉ `unshift`,
không có giới hạn nào.

Gỡ `Index` mà không dọn thì `Diagnostics.cshtml:85` và `Safety.cshtml:256` **đóng
băng vĩnh viễn** trên rác giả còn kẹt — ô "sự cố đang chờ xử lý khẩn cấp" nhấp
nháy mãi không tự hết.

Nên khi Dashboard tải: loại phần tử có `code` khớp `^(SYS-FLT|OP-ERR|MNT-DUE)-`,
**giữ** phần tử của `OperationControl` (`code === "SU-CO-KHAN"` hoặc `msg` chứa
`"E-Stop"`), rồi ghi lại. **Không** `removeItem`.

### Từ vựng đã khớp sẵn

| `nguon` | `type` | Nhãn đang dùng |
|---|---|---|
| `operation` | 1 | Lỗi Thao Tác |
| `hardware` | 2 | Lỗi Hệ Thống |
| `maintenance` | 3 | Đến Hạn Bảo Trì |

## Out of scope

- **Không sửa `OperationControl.cshtml`** — kể cả hàm `saveActiveAlarms` ở
  `:618`.
- Không sửa `Diagnostics.cshtml`, `Safety.cshtml`. Sau packet này chúng chỉ còn
  đọc được bản ghi do `OperationControl` ghi — **hạn chế đã biết**, nêu lại ở C3.
- Không xoá khoá `localStorage.activeAlarms`.
- Không đổi `GET /Alarm/List` hay `POST /Alarm/Ack`.
- Không đụng phần còn lại của Dashboard (KPI, sức chứa, bản đồ khối). Khối dữ
  liệu thật ở `Index.cshtml:1318-1503` là IIFE riêng, không tham chiếu
  `activeAlarms`.
- Không thêm phân trang hay bộ lọc.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Bảng hiện đúng cảnh báo chưa xác nhận trong CSDL, kể cả cột Nguồn phát | view | `Index.cshtml` | đã rõ | elevated — số đúng cạnh nội dung sai là loại lỗi khó phát hiện nhất | live: so mã lỗi và cột Nguồn phát với `GET /Alarm/List` |
| CP-02 | Không còn nguồn nội dung bịa nào trong sáu nguồn | view | `Index.cshtml` | đã rõ | critical — hai nguồn không chứa chuỗi cấm nên qua mặt mọi phép kiểm tĩnh | live: gieo rác vào `localStorage` **trước khi** tải, rồi đo |
| CP-03 | Ba bộ đếm và biểu ngữ tính trên đúng tập chưa xác nhận | view | `Index.cshtml` | đã rõ | elevated — ba con số to và dễ tin | live: đối chiếu với `WHERE xac_nhan_luc IS NULL GROUP BY nguon` |
| CP-04 | Không đọc được máy chủ hiện khác hẳn trạng thái sạch | view | `Index.cshtml` | đã rõ | critical — "an toàn" lúc đang mù | live: ép ba dạng hỏng, đòi **khẳng định dương** một câu báo lỗi cố định |
| CP-05 | Vòng tự làm mới thật sự chạy | view | `Index.cshtml` | đã rõ | elevated — không làm mới thì màn hình tường đứng im cả đêm | live: đếm lượt gọi `/Alarm/List` trong cửa sổ 42 giây |
| CP-06 | Nút xác nhận ghi được xuống CSDL | view | `Index.cshtml` | đã rõ | elevated — nút chỉ làm mờ dòng là nút giả | live: bấm nút **thật trong DOM**, đọc `xac_nhan_luc` |
| CP-07 | Rác giả được dọn, bản ghi của trang khác được giữ | view | `Index.cshtml` | đã rõ | elevated — dọn quá tay thì cắt đầu vào của `Safety` | live: gieo cả hai loại phần tử, đo lại sau khi tải |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When có N cảnh báo **chưa xác nhận**, bảng shall hiện đúng N dòng với đúng mã lỗi. | `node specs/dashboard-canh-bao-that/verify-dashboard-canh-bao.mjs` |
| AC-02 | Cột "Nguồn phát" shall dựng từ `zone_id`/`block_no`/`thiet_bi`, **bỏ qua phần thiếu**, và shall không bao giờ hiện chuỗi `null`. | cùng lệnh |
| AC-03 | When `localStorage.activeAlarms` có sẵn dữ liệu trước khi tải trang, bảng shall vẫn hiện đúng dữ liệu từ máy chủ. | cùng lệnh |
| AC-04 | Ba bộ đếm shall khớp số cảnh báo **chưa xác nhận** từng nhóm, và tổng ba bộ đếm shall bằng số dòng trong bảng. | cùng lệnh |
| AC-05 | Biểu ngữ shall hiện đúng số cảnh báo **chưa xác nhận**, và shall ẩn khi con số đó bằng 0. | cùng lệnh |
| AC-06 | `Index.cshtml` shall không còn `SYS-FLT-`, `OP-ERR-`, `MNT-DUE-`, `addRealtimeAlarm`, ba tên kho câu, `getRandomItem`; và `getItem('activeAlarms')` shall xuất hiện **đúng một lần**, nằm trong hàm dọn rác — không phải trên đường dựng bảng. | cùng lệnh |
| AC-07 | When không đọc được `/Alarm/List` (ba dạng hỏng), bảng shall hiện **một câu báo lỗi cố định**, shall không hiện "Hệ thống vận hành an toàn", và biểu ngữ shall không bị ẩn. | cùng lệnh |
| AC-08 | Trang shall gọi `/Alarm/List` **ít nhất 2 lần** trong 42 giây, và số dòng shall chỉ đổi theo dữ liệu máy chủ. | cùng lệnh |
| AC-09 | When bấm xác nhận một dòng, `xac_nhan_luc` của cảnh báo đó shall khác NULL. | cùng lệnh |
| AC-10 | When tải trang, phần tử `localStorage.activeAlarms` có mã `SYS-FLT`/`OP-ERR`/`MNT-DUE` shall bị loại, phần tử chứa `"E-Stop"` hoặc mã `SU-CO-KHAN` shall được **giữ**, và khoá shall **không** bị xoá. | cùng lệnh |
| AC-11 | Bảng shall sắp xếp theo `xay_ra_luc` giảm dần, và biểu ngữ shall nêu cảnh báo mới nhất theo khoá đó. | cùng lệnh |
| AC-12 | Dấu vân tay `Index.cshtml` ở cây mã nguồn shall trùng bản deploy. | cùng lệnh |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Dashboard đọc cảnh báo thật, gỡ sáu nguồn bịa | AC-01 … AC-12 | `TotalParking/Views/Home/Index.cshtml`, `specs/dashboard-canh-bao-that/verify-dashboard-canh-bao.mjs` | - | done |

## Review log

- **Round 1 (25/09/2026):** hai người rà soát ngữ cảnh mới trả về 12 phát hiện,
  khử trùng lặp còn 9, trong đó **2 Critical**. Người dùng **nhận cả 9** và chọn
  **dọn rác một lần giữ lại bản ghi E-Stop**.

  Đã áp dụng: sửa chỉ dẫn `saveActiveAlarms` thành cảnh báo tuyệt đối không đụng
  `OperationControl.cshtml:618` (F1); bảng "bốn chỗ" thành **sáu nguồn**, thêm
  nhánh `return` sớm `:1023-1035` và bộ sinh `MNT-DUE-100` `:1060-1082` (F2);
  chốt **một tập đếm duy nhất** là chưa xác nhận, sửa AC-04 và AC-05 (F3); cấm
  cả đường **đọc** `localStorage` và đòi khẳng định dương một câu báo lỗi cố
  định (F4); thêm CP-07 + AC-10 cho việc dọn rác (F5); chốt nhịp 15 giây và đổi
  probe thành **đếm lượt gọi API** thay vì so số dòng (F6); viết công thức cột
  "Nguồn phát" và cấm chuỗi `null` (F7); sắp xếp theo `xay_ra_luc` thay vì
  `HH:mm:ss` (F8); sửa các số dòng lệch và số phép kiểm (F9).
