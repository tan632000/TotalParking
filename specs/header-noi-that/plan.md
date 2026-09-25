# Header SCADA nói đúng sự thật
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 25/09/2026)

- **Existing:**
  - `TotalParking/Controllers/AlarmController.cs:27` — `GET /Alarm/List`, đo được
    **7.950 byte thân phản hồi cho 16 cảnh báo** (~500 byte/dòng); trả tất cả
    chưa xác nhận cộng tối đa 200 đã xác nhận
    (`TotalParking/Services/CanhBaoRepository.cs:44,66`).
  - `TotalParking/Views/Home/Alarms.cshtml:135` — nhịp làm mới 15 giây đã chạy.
  - `TotalParking/Views/Home/Index.cshtml:1489-1494` — khuôn bật/dừng poll kèm
    guard `if (!timer)` / `timer = null`, và `visibilitychange`.
  - `TotalParking/Views/Shared/_ScadaLayout.cshtml:77-125` — khối `<header>`,
    chứa toàn bộ phần cần sửa.
  - `TotalParking/Content/scada.css` — đã có sẵn `bg-green-950/60`,
    `border-green-700/50`, `text-green-300`, `bg-slate-800/50`, `text-slate-400`,
    nên chip xanh và chip xám không cần thêm CSS.

- **Minimum change:** hai file — thêm `GET /Alarm/Summary` và nối header vào nó,
  kèm đồng hồ chạy theo giờ máy và bỏ tên người dùng giả.

- **Expansion signals:** không có. 2 file mã + 1 script kiểm chứng, 0 dịch vụ
  mới, 0 bảng mới — dưới ngưỡng 8 file.

- **User decision: KEEP** — sửa cả 4 chỗ viết cứng, thêm endpoint tóm tắt riêng,
  và hiện chip xanh "Bình thường" khi sạch.

### Vì sao không dùng lại `GET /Alarm/List`

Header hiện trên **22 trang** (đếm được 22 file `.cshtml` khai báo
`_ScadaLayout`).

Nếu header poll `/Alarm/List` thì mỗi chu kỳ kéo cả danh sách chỉ để lấy hai con
số: 8 KB bây giờ, và **trên 100 KB khi có 200 cảnh báo chưa xác nhận** — phần
chưa xác nhận không có trần, nên con số xấu nhất còn cao hơn. Đúng lúc hệ thống
đang có sự cố thì header trở thành gánh nặng lớn nhất. Ngoài ra
`Alarms.cshtml:220` và `OperationControl.cshtml:985` **đã** poll endpoint đó.

### Vì sao chip phải xanh chứ không ẩn khi sạch

Ẩn chip thì "không có sự cố nào" và "header hỏng / mất kết nối máy chủ" trông
giống hệt nhau. Chip xanh là bằng chứng nhìn thấy được rằng header còn sống.

### Vì sao đây là rủi ro `elevated`

`_ScadaLayout.cshtml` là layout dùng chung của 22 trang. Một lỗi JavaScript ném
ra từ script layout làm hỏng **mọi trang cùng lúc**, kể cả những trang không
liên quan gì tới cảnh báo.

Bề mặt hợp đồng ngược lại thì **hẹp**: vùng header chỉ có đúng một `id`
(`#scada-aside-toggle`, `_ScadaLayout.cshtml:86`) và không file nào ngoài chính
layout tham chiếu tới nó. Rủi ro đến từ việc dùng chung layout, không từ ràng
buộc ngầm.

## Out of scope

- **Không sửa `_ScadaLayout.cshtml:291`** — `v2.4.1 – 08/06/2026` là nhãn phiên
  bản của thanh bên, nằm ngoài khối `<header>`. Mọi phép kiểm chuỗi chỉ được áp
  dụng cho `:77-125`.
- **Không sửa `Remote.cshtml:106,186`** — trang User & Permission vẫn trưng
  `supervisor01` như tài khoản đang trực tuyến. Đây là **hạn chế đã biết**: sau
  packet này danh tính giả vẫn còn ở đó, và phải nêu lại ở C3.
- **Không đụng `lucide.createIcons()` ở `_ScadaLayout.cshtml:305`** và không ghim
  CDN `unpkg` ở `:10`. Đây là quyết định, không phải bỏ sót — xem CP-05.
- Không đụng thanh bên, nút thu gọn, hay khoá `scadaSidebarCollapsed`.
- Không thêm đăng nhập — bỏ `supervisor01` khỏi header chứ không thay bằng danh
  tính thật.
- Không đổi trang Alarms, không đổi `GET /Alarm/List`.
- Không thêm chuông, còi, hay thông báo ra ngoài trình duyệt.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Header hiện đúng số cảnh báo chưa xác nhận và đúng màu theo mức cao nhất, trên mọi trang | view, API | `_ScadaLayout.cshtml`, `AlarmController.cs` | đã rõ | elevated — sai số trên 22 trang, và chip đỏ vĩnh viễn dạy người vận hành phớt lờ cảnh báo | live: đo trên ≥2 trang khác nhau, ép **ba** trạng thái (có critical / chỉ mức thấp / sạch) |
| CP-02 | Máy chủ không trả lời được hiện khác hẳn trạng thái sạch | view | `_ScadaLayout.cshtml` | đã rõ | elevated — hiện "Bình thường" lúc đang mù là lỗi an toàn | live: ép **ba** dạng hỏng (503, 200 + `{}`, 200 + HTML) |
| CP-03 | Endpoint tóm tắt nhẹ và kích thước không tăng theo số cảnh báo | API | `AlarmController.cs` | đã rõ | elevated — 22 trang nhân với kích thước phản hồi | live: đo **byte thân phản hồi** với 0 và với ≥40 cảnh báo |
| CP-04 | Đồng hồ chạy theo giờ máy, không còn danh tính giả trong header | view | `_ScadaLayout.cshtml` | đã rõ | routine — hiển thị | live: đọc DOM hai lần cách nhau; kiểm chuỗi **trong khối `<header>`** |
| CP-05 | Poll không chồng nhau và dừng khi tab ẩn; script mới không bị lỗi có sẵn nuốt mất | view | `_ScadaLayout.cshtml` | đã rõ | elevated — màn hình tường mở nhiều ngày; lucide hỏng ở mạng OT thì nuốt cả khối script | live: đếm request sau nhiều lần ẩn/hiện; lọc `pageerror` theo nguồn |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When có N cảnh báo chưa xác nhận, header shall hiện đúng N trên **ít nhất hai trang khác nhau**. | `node specs/header-noi-that/verify-header.mjs` |
| AC-02 | When không còn cảnh báo chưa xác nhận, header shall hiện chip xanh "Bình thường" thay cho chip đỏ. | cùng lệnh |
| AC-03 | When có ít nhất một cảnh báo `critical` đang mở, chip shall đỏ; when chỉ còn mức thấp hơn, chip shall **không** đỏ. | cùng lệnh |
| AC-04 | When `/Alarm/Summary` trả lỗi **hoặc** trả `200` với thân không dùng được, header shall hiện dấu hiệu mất kết nối, shall **không** giữ số cũ và shall **không** hiện "Bình thường". | cùng lệnh |
| AC-05 | When trang mở qua hai lần đọc cách nhau, đồng hồ header shall đổi giá trị. | cùng lệnh |
| AC-06 | Khối `<header>` (`_ScadaLayout.cshtml:77-125`) shall không còn `4 Alarm`, `supervisor01`, `16:02:42`, `08/06/2026`. | cùng lệnh |
| AC-07 | `GET /Alarm/Summary` shall có **byte thân phản hồi** không tăng quá 50 byte khi số cảnh báo đi từ 0 lên ≥40. | cùng lệnh |
| AC-08 | Script header shall không sinh lỗi JavaScript trên trang không liên quan tới cảnh báo; lỗi `lucide is not defined` có sẵn shall được ghi nhận riêng chứ không tính là lỗi của task. | cùng lệnh |
| AC-09 | Dấu vân tay `TotalParking.dll` và `_ScadaLayout.cshtml` ở cây mã nguồn shall trùng **bản deploy `C:\Users\Admin\Documents\Web\totalParking`**, không phải bản sao trong `obj/`. | cùng lệnh |
| AC-10 | When tab bị ẩn rồi hiện lại nhiều lần, số lượt gọi `/Alarm/Summary` shall không vượt quá số chu kỳ đã trôi qua. | cùng lệnh |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Header đọc số thật từ endpoint tóm tắt | AC-01 … AC-10 | `TotalParking/Controllers/AlarmController.cs`, `TotalParking/Services/CanhBaoRepository.cs`, `TotalParking/Views/Shared/_ScadaLayout.cshtml`, `specs/header-noi-that/verify-header.mjs` | - | done |

Một task chứ không hai: tách endpoint thành task riêng sẽ tạo ra một task
preparation-only — một API không ai gọi thì không có kết quả quan sát được, và
không chứng minh được gì ngoài "nó trả về JSON".

## Review log

- **Round 1 (25/09/2026):** hai người rà soát ngữ cảnh mới (Fact Checker +
  Contract Verifier; Failure-mode + Assumption destroyer + Scope critic) trả về
  10 phát hiện, khử trùng lặp còn 9. Người dùng **nhận cả 9**.

  Đã áp dụng: giới hạn phép kiểm chuỗi vào khối `<header>` `:77-125` (F1); thêm
  oracle và đường dẫn deploy tuyệt đối cho `deploy_khop_source`, loại trừ `obj/`
  (F2); thêm hai dạng hỏng `200` vào AC-04 (F3); thêm CP-05, AC-10 và probe
  `poll_khong_chong_nhau` kèm yêu cầu guard `if (!timer)` (F4); lọc `pageerror`
  theo nguồn và cấm viết vào khối script `:304-306` (F5); sửa số dòng
  `:111`/`:112`/`:116` (F6); sửa khẳng định "script duy nhất" (F7); đo byte thân
  phản hồi thay cho `Content-Length` (F8); ghi `Remote.cshtml` vào Out of scope
  (F9).

  Quét lại: AC-01…AC-10 đều map tới task 01; CP-01…CP-05 đều được task 01 sở
  hữu; `## Dependencies` của task là `none`; `## Receipt` để trống.

- **Round 2 (25/09/2026, sau khi cài đặt):** rà soát độc lập trên diff trả về
  verdict **FAIL** với 6 phát hiện. Bốn cái là lỗ hổng thật và đã sửa:
  `poll_khong_chong_nhau` không tái hiện được kịch bản thiếu guard (đã thêm ba
  lần *hiện* không xen *ẩn*, và **chứng minh ngược**: gỡ guard → 12 lượt gọi,
  probe FAIL); `chip_do_khi_co_critical` phụ thuộc dữ liệu sản xuất tình cờ và
  dùng oracle sao chép chính câu SQL của bản cài đặt (đã chèn dòng `critical`
  riêng và đọc thẳng `/Alarm/Summary`); `apDung` không kiểm `muc_cao_nhat` nên
  một `critical` có thể hiện thành chip hổ phách (đã thêm kiểm); regex `doDo`
  viết `\\s` thay vì `\s` (đã sửa). Một phát hiện là lệch bảng Ownership
  (`CanhBaoRepository.cs`) — đã cập nhật cả hai file.

  **Một phát hiện bị bác bỏ bằng bằng chứng:** `border-amber-800` được báo là
  thiếu trong `scada.css`; `grep -F ".border-amber-800"` cho thấy lớp này **có**
  (cả biến thể trơn lẫn `/30`). Không sửa.

- Hai khẳng định người rà soát đánh dấu `[UNVERIFIED]` đã được kiểm sau đó:
  `/Alarm/List` **có** phát `Content-Length: 7968` cho 16 cảnh báo, nên kịch bản
  "chunked nên PASS giả" của F8 không tái hiện được trên máy chủ này — cách sửa
  vẫn được nhận vì nó diệt kiểu PASS-im-lặng khi phép đo trả 0.
