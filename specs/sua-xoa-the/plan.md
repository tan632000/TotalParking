# Sửa và ngừng dùng thẻ RFID
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 25/09/2026, sửa lại sau vòng rà soát)

- **Existing:**
  - `TotalParking/Controllers/CardsController.cs:49-84` — `GET /Cards/List`; thân
    JSON ở `:60-77` **không có `card_id`**.
  - `TotalParking/Services/ParkingCardRepository.cs:94` — `GetAllRows()` trả 14
    cột; `:264-273` đã có một câu `UPDATE parking_card` (trong `WriteBatch`) và
    nó **không** đặt `card_code` trong `SET` — đúng khuôn cần dùng lại.
  - `TotalParking/Views/Home/Cards.cshtml:143` gọi `/Cards/List`; bảng 11 cột ở
    `:67-77`; khuôn modal ở `:91-131` với cặp bật/tắt ở `:246-259`.
  - `TotalParking/Services/Plc/PlcAuditLog.cs` — khuôn ghi nhật ký có xoay file.

- **Minimum change:** 4 file mã (một file mới) + `TotalParking.csproj` + 2 script
  kiểm chứng.

- **User decision (lần 2): xoá mềm** — đặt `is_active = 0`, bật lại được. Quyết
  định ban đầu là xoá cứng; người dùng đổi sau khi có bằng chứng mới ở vòng rà
  soát (xem mục dưới). Vẫn **khoá `card_code`**.

### Vì sao bỏ xoá cứng: bằng chứng mới ở vòng rà soát

`SlotOccupancyReader.cs:213-222` chọn bố cục giải mã thanh ghi ô đỗ bằng cách
thử từng bố cục rồi hỏi `CardExists()` — tức tra `parking_card`. Với
`plc:slotWordCount = 2` có đúng **hai** ứng viên (`Binary32Hi`, `Binary32Lo`),
nên bảng thẻ là **trọng tài duy nhất** giữa hai cách đọc; mất nó thì `:221` rơi
về `Binary32Lo`.

Xoá cứng một thẻ lúc nó đang rảnh (mọi lớp chặn đều cho qua) rồi khách dùng lại
đúng tấm thẻ nhựa đó: vòng quét giải mã mà không còn thẻ để đối chiếu, và nếu PLC
đó dùng `Binary32Hi` thì ghi vào `plc_slot_state` một mã **đảo byte**. Ô đó rơi
vào `Suspect` chứ không vào `Occupied` (`:166-174`) — bảng LED báo thừa chỗ, và
tra xe theo mã đúng thì không ra. Đường này chạy **bất kể**
`plc:requireRegisteredCard`.

Xoá mềm giữ dòng lại nên `CardExists()` vẫn tìm thấy, và toàn bộ hệ quả trên
biến mất.

### Xoá mềm không phải là vô hại

`CardScanService.cs:95` từ chối thẻ bị tắt **trước** khi tra phiên đang mở
(`:97-103`):

```csharp
if (!cardActive) return CardScanDecision.Deny(RejectReason.InactiveCard);
```

Nghĩa là tắt thẻ của một chiếc xe **đang nằm trong bãi** thì tài xế quẹt thẻ ở
cổng và bị từ chối — xe không lấy ra được. Vì vậy hai lớp chặn vẫn cần, chỉ đổi
lý do: không còn là "mất dấu xe" mà là "khoá tài xế ngoài cổng".

### Ba lớp chặn, sau khi sửa theo rà soát

1. **Thẻ đang có xe trong ô đỗ** (`plc_slot_state.card_code`) — chặn tắt.
2. **Thẻ có phiên gửi xe đang mở** (`parking_session`) — chặn tắt.
3. **Thẻ vừa quẹt trong 24 giờ** (`plc_request.received_at`) — chặn tắt.

Lớp 3 **phải có giới hạn thời gian**. `plc_request` là nhật ký chỉ ghi thêm
(`Database/06_plc_device.sql:61-63`, `Services/PlcRequestRepository.cs` chỉ có
`INSERT`). Đo được: **517 dòng / 18 mã, trong đó 13 mã khớp thẻ thật, nhưng chỉ
4 mã có lượt quẹt trong 24 giờ qua**. Chặn theo toàn bộ lịch sử là khoá vĩnh viễn
13 thẻ, và con số đó chỉ lớn lên theo thời gian.

Khoá ngoại `fk_session_card` không còn liên quan: không có `DELETE` nào nữa.

### Vì sao `card_code` bị khoá

Mã thẻ là giá trị nằm trong thanh ghi ô đỗ của PLC. Đổi nó khi xe đang gửi thì
thẻ trong tay tài xế và mã trong PLC không còn khớp. Nhập sai mã thì tắt thẻ rồi
thêm thẻ mới, không sửa tại chỗ.

Khoá này phải nằm ở **máy chủ**, và cách làm là **không bao giờ đưa `card_code`
vào mệnh đề `SET`** — không phải nhận rồi so sánh rồi từ chối. Ít đường hỏng hơn.

### Định danh dòng bằng `card_id`, không bằng `card_code`

`/Cards/List` hiện không trả `card_id` (`CardsController.cs:60-77`), nên giao
diện không có gì để chỉ đúng dòng. Task 01 bổ sung cột đó — nó thuộc Ownership
của task 01 vì task 01 đã sở hữu `CardsController.cs`.

Định danh bằng `card_code` nhận từ thân POST là đường dẫn tới ghi đè nhầm một
thẻ khách thật, và phép kiểm "mã thẻ không đổi được" vẫn PASS khi mắc lỗi đó —
nên packet có thêm một phép kiểm chụp vân tay **toàn bảng**.

### Nhật ký ghi ra file riêng

`PlcAuditLog` xoay ở 8 MB và giữ đúng một file cũ (`PlcAuditLog.cs:31-34`), trong
khi vòng quét đổ hàng trăm dòng mỗi phút vào cùng file đó. Dấu vết một thao tác
quản trị sẽ biến mất sau vài ngày. Vì vậy thêm `CardAdminLog` ghi ra
`App_Data/card_admin.log` — **một file `.cs` mới, nên `TotalParking.csproj` phải
được cập nhật** (hard rule 1 của `AGENTS.md`).

## Out of scope

- **Không xoá cứng bất kỳ thẻ nào.** Không có `DELETE FROM parking_card` trong
  packet này.
- Không cho sửa `card_code`.
- Không sửa `SlotOccupancyReader.Decode` để nó thôi phụ thuộc `parking_card` —
  đó là gốc của vấn đề xoá cứng, nhưng nó chạm đường đọc ô đỗ của toàn bãi và là
  một packet riêng.
- Không đụng nhập hàng loạt (`Preview`, `Import`, `Template`).
- Không thêm đăng nhập — tên người thao tác vẫn **tự khai**.
- Không đổi `plc:requireRegisteredCard`, không sửa `CarLocatorService`.
- Không thêm phân trang hay tìm kiếm cho bảng thẻ.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Sửa được thông tin thẻ, giá trị mới đọc lại đúng, và **không thẻ nào khác bị đụng** | API, data | `ParkingCardRepository.cs`, `CardsController.cs` | đã rõ | critical — định danh sai làm ghi đè thẻ khách thật | live: sửa rồi so vân tay toàn bảng trước/sau |
| CP-02 | `card_code` không đổi được qua bất kỳ đường nào | API | `ParkingCardRepository.cs` | đã rõ | critical — đổi mã khi xe đang gửi thì mất dấu xe | live: `POST` thẳng kèm `card_code` mới |
| CP-03 | Tắt và bật lại được thẻ rảnh | API, data | `ParkingCardRepository.cs`, `CardsController.cs` | đã rõ | elevated — hai chiều, khôi phục được | live: tắt rồi bật lại thẻ tự tạo |
| CP-04 | Tắt bị chặn khi thẻ đang gắn với một chiếc xe | API | `ParkingCardRepository.cs` | đã rõ | critical — khoá tài xế ngoài cổng | live: ép cả ba đường chặn |
| CP-05 | Mọi thao tác để lại dấu vết trong nhật ký riêng, không bị vòng quét cuốn trôi | API | `CardAdminLog.cs` | đã rõ | elevated — thao tác quản trị không dấu vết | live: đọc file riêng sau thao tác |
| CP-06 | Lỗi CSDL hiện thành câu đọc được, không phải văn bản thô | API | `CardsController.cs` | đã rõ | elevated — người vận hành nhận thông báo vô nghĩa | live: ép 1062, khoá ngoại lạ, và chuỗi quá dài |
| CP-07 | Người vận hành sửa, tắt và bật lại thẻ từ trang Cards | view | `Cards.cshtml` | đã rõ | elevated — bấm nhầm | live: puppeteer bấm thật, đối chiếu CSDL |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When gửi `POST /Cards/Update` hợp lệ, hệ thống shall lưu giá trị mới và `GET /Cards/List` shall trả đúng giá trị đó. | `node specs/sua-xoa-the/verify-api-the.mjs` |
| AC-02 | When sửa một thẻ, mọi thẻ khác shall không đổi (vân tay toàn bảng trước và sau phải khớp). | cùng lệnh |
| AC-03 | When `POST /Cards/Update` kèm `card_code` khác, `card_code` của dòng đó shall không đổi. | cùng lệnh |
| AC-04 | When tắt một thẻ rảnh, `is_active` shall thành 0; when bật lại, shall thành 1. | cùng lệnh |
| AC-05 | When thẻ có mã trong `plc_slot_state`, hệ thống shall từ chối tắt và giữ `is_active = 1`. | cùng lệnh |
| AC-06 | When thẻ có phiên gửi xe đang mở, hệ thống shall từ chối tắt. | cùng lệnh |
| AC-07 | When thẻ có lượt quẹt trong 24 giờ qua, hệ thống shall từ chối tắt; lượt quẹt cũ hơn 24 giờ shall **không** chặn. | cùng lệnh |
| AC-08 | When sửa `card_no` trùng thẻ khác, hệ thống shall trả thông báo đọc được, **không** phải văn bản lỗi thô của CSDL. | cùng lệnh |
| AC-09 | When gửi `customer_type_id` hoặc `weight_class_id` không tồn tại, hoặc chuỗi vượt độ dài cột, hệ thống shall trả thông báo đọc được. | cùng lệnh |
| AC-10 | When thao tác thành công hoặc bị từ chối, hệ thống shall ghi một dòng vào `App_Data/card_admin.log` kèm mã thẻ, người thao tác, kết quả. | cùng lệnh |
| AC-11 | When người vận hành bấm sửa trên trang Cards, điền và lưu, bảng shall hiện giá trị mới và CSDL shall khớp. | `node specs/sua-xoa-the/verify-trang-the.mjs` |
| AC-12 | When bấm tắt thẻ, hệ thống shall hỏi xác nhận có hiện **mã thẻ**, và chỉ tắt sau khi xác nhận. | cùng lệnh |
| AC-13 | When bị từ chối, trang shall hiện lý do đọc được và trạng thái thẻ shall không đổi. | cùng lệnh |
| AC-14 | `Cards.cshtml` shall không có ô nhập nào cho `card_code` trong biểu mẫu sửa. | cùng lệnh |
| AC-15 | Dấu vân tay `TotalParking.dll` và `Cards.cshtml` ở cây mã nguồn shall trùng bản deploy. | cả hai lệnh |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | API sửa / tắt / bật thẻ, ba lớp chặn, nhật ký riêng | AC-01 … AC-10, AC-15 | `TotalParking/Services/ParkingCardRepository.cs`, `TotalParking/Services/CardAdminLog.cs`, `TotalParking/Controllers/CardsController.cs`, `TotalParking/TotalParking.csproj`, `specs/sua-xoa-the/verify-api-the.mjs` | - | done |
| 02 | Trang Cards sửa, tắt và bật lại được | AC-11 … AC-15 | `TotalParking/Views/Home/Cards.cshtml`, `specs/sua-xoa-the/verify-trang-the.mjs` | task-01-api-sua-tat.md | done |

## Review log

- **Round 1 (25/09/2026):** hai người rà soát ngữ cảnh mới trả về 12 phát hiện,
  khử trùng lặp còn 9. Người dùng **nhận cả 9**, và **đổi quyết định C1 từ xoá
  cứng sang xoá mềm** sau bằng chứng `SlotOccupancyReader.cs:213-222`.

  Đã áp dụng: bỏ toàn bộ đường xoá cứng; thêm `card_id` vào `/Cards/List` và đưa
  vào Ownership task 01; định danh bằng `card_id` kèm phép kiểm vân tay toàn
  bảng; giới hạn lớp `plc_request` về 24 giờ; tách nhật ký ra
  `App_Data/card_admin.log` qua file `.cs` mới kèm cập nhật `.csproj`; thêm AC
  cho trùng `card_no`, id tra cứu lạ, và chuỗi quá dài (`STRICT_TRANS_TABLES` ném
  1406 chứ không cắt); sửa cách ép lớp chặn để không làm sai sức chứa thật; sửa
  ba chỗ viện dẫn sai.

  **Bác bỏ bằng bằng chứng:** khẳng định "425/755 ô chưa từng đọc được" (trích
  từ chú thích trong `24_led_capacity_from_plc.sql`) đã lạc hậu — đo lại trên
  CSDL sống: **755 ô, 0 ô chưa đọc lần nào**. Lỗ hổng phía sau vẫn thật nhưng ở
  dạng khác: khi PLC mất điện, `plc_slot_state` giữ giá trị đọc được **lần
  cuối**, không phải `NULL`.

  **Phát hiện thêm khi viết lại:** `CardScanService.cs:95` từ chối thẻ bị tắt
  **trước** khi tra phiên đang mở, nên xoá mềm cũng khoá được tài xế ngoài cổng.
  Ba lớp chặn vì vậy được giữ nguyên chứ không nới ra.
