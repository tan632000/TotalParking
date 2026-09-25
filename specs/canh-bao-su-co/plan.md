# Cảnh báo và sự cố có nơi lưu thật
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-09-24)

- **Existing:** action `Alarms()` ở `TotalParking/Controllers/HomeController.cs:49`
  chỉ `return View()`; giao diện `Views/Home/Alarms.cshtml` đã hoàn chỉnh với 11
  cột nhưng **6 dòng dữ liệu viết cứng trong HTML** (dòng 92, 115, 138, 161, 184,
  207) cộng **4 thẻ số liệu cứng** (dòng 19-40); nguồn PLC lỗi đã có từ packet
  `specs/tach-vai-co-plc` (`plc_device.is_connected`, view `v_plc_lech_tang`);
  khuôn API CRUD ở `Controllers/CardsController.cs`; khuôn ghi nhật ký ở
  `Services/Plc/PlcAuditLog.cs`.
- **Minimum change:** một bảng cảnh báo; API đọc và xác nhận; trang `Alarms` đọc
  từ bảng đó; dịch vụ sinh cảnh báo khi PLC mất kết nối; nút `ESTOP` hiện có ghi
  được sự cố thật.
- **Expansion signals:** cả ba tín hiệu đều chạm ngưỡng nếu gộp chung với bảo trì
  và danh sách block (~14 file, 3 lớp mới, 3 hệ con độc lập).
- **User decision: tách 3 packet, làm lần lượt.** Đây là packet **A** — cảnh báo.

### Hai phát hiện trong khảo sát làm đổi hình dạng packet

**1. Nút `ESTOP` đã tồn tại và nó không làm gì.**
`Views/Home/OperationControl.cshtml:345` có nút nhãn `ESTOP` gọi
`setBlockMode('Emergency')` (`:946-967`). Hàm đó ghi `localStorage` của chính
trình duyệt rồi tô ô sang `Warning`. Không gửi gì xuống PLC, không ghi CSDL, máy
khác không thấy.

Một nút mang nhãn dừng khẩn cấp mà thực chất không dừng được gì là rủi ro an
toàn. Người dùng chốt: **đổi thành nút ghi nhận sự cố thật, và đổi nhãn cho đúng
việc nó làm.**

**2. `parking_event` không tái dùng được.** Bảng đó có `session_id`,
`from_status`, `to_status` — dành cho vòng đời một phiên gửi xe. Cảnh báo thiết
bị không gắn phiên nào.

### Hai lỗi đã được sửa ngoài packet

Review phát hiện `connected_changed_at` bị đặt `NOW(3)` vô điều kiện ở
`Services/PlcDeviceRepository.cs`, nên mỗi lần ứng dụng khởi động lại thì cả 112
dòng bị đặt mốc mới — đo được ngày 24/09: toàn bộ 112 dòng cùng mốc `22:56`
trong khi vài PLC đã chết từ hôm trước.

Đây là lỗi của packet `specs/tach-vai-co-plc`, và nó **chặn packet này**: task 03
dùng đúng cột đó để đo "PLC mất kết nối bao lâu rồi". Người dùng chốt sửa ngay,
tách riêng. Đã sửa và đã kiểm qua một lần ứng dụng nạp lại: mốc giữ nguyên
`22:56` trong khi `last_probe_at` vẫn cập nhật tới `23:38`.

**Lỗi thứ hai: ma trận khoá liên động an toàn không dựng được.**
`Safety.cshtml` và `Diagnostics.cshtml` đọc `activeAlarms` với ba lỗi trong cùng
một khối: dùng `a.message` trong khi nơi ghi dùng `a.msg`; so `a.type` với chuỗi
`"operator"`/`"system"` trong khi nó là số `1`/`2`/`3`; và thứ tự toán tử làm
`A && B || C` chạy vế `C` vô điều kiện. Kết quả: mỗi khi localStorage có bất kỳ
cảnh báo nào, `renderMatrix()` ném `TypeError` và **toàn bộ ma trận an toàn không
được dựng** — người vận hành nhìn vào trang trống mà tưởng bình thường.

Người dùng chốt sửa ngay. Đã sửa cả hai file và kiểm bằng trình duyệt với cảnh
báo thật: 5/5 phép kiểm PASS, ma trận dựng đủ 13 dòng, và dòng E-Stop bắt đúng
cảnh báo (`✗ LỖI — E-Stop khẩn cấp tại Block đang kích hoạt!`).

### Vì sao mức rủi ro là `critical`

Không phải vì bảng hay giao diện, mà vì **nút dừng khẩn là bề mặt an toàn**. Kể
cả khi nó chỉ ghi nhận sự kiện, nhãn và cách hiển thị phải được xử lý như một
yêu cầu an toàn.

## Out of scope

- **Trang bảo trì** và **màn hình danh sách block** — hai packet riêng theo C1.
- **Các nút điều khiển khác** trên `OperationControl` (Start, Stop, Pause, Lock,
  Bypass, Override...) cũng chỉ ghi `localStorage`. Chúng cần biết ladder nhận
  lệnh gì, mà chưa có thông tin đó.
- **Ghi lệnh dừng xuống PLC.** Người dùng chốt nút chỉ ghi nhận sự kiện.
- Gửi cảnh báo ra ngoài (email, tin nhắn, còi).
- Cảnh báo từ tầng LED, PGS, camera — đợt này chỉ PLC.
- **Lịch sử đầy đủ và phân trang.** `GET /Alarm/List` đợt này trả tập chưa xác
  nhận cộng tối đa 200 dòng gần nhất. Xem hết lịch sử là endpoint riêng, đợt sau.
- **Giữ-xoá dữ liệu cũ.** Chưa có cơ chế nào trong toàn dự án; đợt này chỉ chặn
  bằng giới hạn trả về, không xoá gì.
- **`Diagnostics` và `Safety` vẫn đọc `activeAlarms` (localStorage), chưa đọc bảng
  cảnh báo mới.** Hai trang này từng ném `TypeError` nên ma trận khoá liên động
  ở `Safety` không dựng được; lỗi đó **đã được sửa ngoài packet này** theo yêu
  cầu người dùng — xem mục "Hai lỗi đã được sửa ngoài packet". Giờ chúng chạy
  đúng, nhưng vẫn lấy dữ liệu từ localStorage chứ không từ bảng mới. Nối chúng
  vào bảng là packet kế tiếp.
- **Dashboard vẫn chạy trên `activeAlarms` với dữ liệu sinh giả.**
  `Index.cshtml:1085-1110` chèn ba cảnh báo bịa khi danh sách ngắn rồi dựng banner
  từ chính mảng đó. Sau packet này trang `Alarms` nói thật còn trang chủ nói số
  khác. Packet kế tiếp.
- **Xác thực người dùng.** Hệ thống không có đăng nhập (`grep Authorize` trong
  `Controllers/` không ra kết quả). `xac_nhan_boi` là tên người **tự khai** kèm
  IP máy khách, không phải danh tính đã xác thực.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | Cảnh báo có nơi lưu bền, đọc và xác nhận được qua API, có giới hạn trả về | bảng mới, API mới | CSDL, endpoint HTTP | đã rõ | elevated | live: ghi rồi đọc lại; xác nhận hai lần; kiểm giới hạn |
| CP-02 | Trang Alarms hiện dữ liệu thật, tự làm mới, bộ lọc còn chạy | đổi nguồn dữ liệu | giao diện người vận hành | đã rõ | elevated — nơi người vận hành nhìn khi có sự cố | live: đo bằng trình duyệt, ép cả nhánh lỗi |
| CP-03 | PLC mất kết nối sinh cảnh báo tự động, không trùng lặp | dịch vụ nền | CSDL, vòng poll | đã rõ | elevated — sinh trùng thì bảng ngập, bỏ sót thì mất cảnh báo | live: ép một PLC rớt rồi khôi phục, chạy trọn hai chu trình |
| CP-04 | Nút khẩn ghi sự cố thật, nhãn nói đúng, trạng thái không nằm riêng trong một trình duyệt | đổi hành vi + nhãn | **bề mặt an toàn** | đã rõ — người dùng chốt ở C1 và C2 | **critical** — nhãn sai hoặc trạng thái cục bộ làm người vận hành tin nhầm | live: bấm nút, đo từ trình duyệt thứ hai; kiểm tĩnh nhãn |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When một cảnh báo được ghi, `GET /Alarm/List` shall trả về nó kèm thời điểm, mức độ, nguồn, trạng thái chưa xác nhận; và shall trả tối đa tập chưa xác nhận cộng 200 dòng gần nhất. | `node specs/canh-bao-su-co/verify-alarm-api.mjs` |
| AC-02 | When người vận hành xác nhận một cảnh báo, hệ thống shall lưu **nhãn người dùng tự khai** và **IP máy khách** cùng thời điểm; cảnh báo đó shall rời nhóm chưa xác nhận. | `node specs/canh-bao-su-co/verify-alarm-api.mjs` |
| AC-03 | When một cảnh báo đã được xác nhận, lần xác nhận thứ hai shall bị từ chối và shall không đè lên người xác nhận đầu tiên. | `node specs/canh-bao-su-co/verify-alarm-api.mjs` |
| AC-04 | When trang `Alarms` tải, nó shall hiển thị đúng số dòng API trả về, và shall không còn **dòng nào lẫn số liệu nào** viết cứng trong HTML. | `node specs/canh-bao-su-co/verify-trang-alarms.mjs` |
| AC-05 | When người vận hành chọn một giá trị bộ lọc bất kỳ, số dòng hiện ra shall khớp số cảnh báo cùng loại từ API. | `node specs/canh-bao-su-co/verify-trang-alarms.mjs` |
| AC-06 | When có cảnh báo mới xuất hiện sau lúc trang đã tải, trang shall tự hiện nó trong vòng một chu kỳ làm mới, và shall hiện thời điểm cập nhật cuối. | `node specs/canh-bao-su-co/verify-trang-alarms.mjs` |
| AC-07 | When một PLC mất kết nối quá ngưỡng, hệ thống shall sinh đúng **một** cảnh báo cho khối đó; PLC vẫn đang lỗi thì shall không sinh thêm qua nhiều vòng chạy. | `node specs/canh-bao-su-co/verify-canh-bao-plc.mjs` |
| AC-08 | When PLC đó kết nối lại, hệ thống shall đánh dấu cảnh báo đã hết và **giữ nguyên dòng**; và lần sự cố tiếp theo shall sinh được cảnh báo mới. | `node specs/canh-bao-su-co/verify-canh-bao-plc.mjs` |
| AC-09 | When `plc_device.is_connected` là `NULL`, hệ thống shall **không** sinh cảnh báo — không biết khác với biết là chết. | `node specs/canh-bao-su-co/verify-canh-bao-plc.mjs` |
| AC-10 | When bấm nút ghi nhận sự cố khẩn, hệ thống shall lưu một cảnh báo mức cao nhất kèm khối và thời điểm, và **một trình duyệt khác** shall đọc được nó. | `node specs/canh-bao-su-co/verify-nut-su-co.mjs` |
| AC-11 | When hiển thị nút đó, `Views/Home/OperationControl.cshtml` shall không còn chuỗi `ESTOP` / `dừng khẩn` / `emergency stop`, và shall có chú thích nói rõ nó chỉ ghi nhận. | `node specs/canh-bao-su-co/verify-nut-su-co.mjs` |
| AC-12 | When đo bất kỳ tiêu chí nào ở trên, `bin\TotalParking.dll` ở cây mã nguồn và bản deploy shall trùng dấu vân tay. | mọi script |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Bảng cảnh báo và API đọc/xác nhận | AC-01, AC-02, AC-03, AC-12 | `TotalParking/Database/45_canh_bao.sql`, `TotalParking/Services/CanhBaoRepository.cs`, `TotalParking/Controllers/AlarmController.cs`, `TotalParking/TotalParking.csproj`, `specs/canh-bao-su-co/verify-alarm-api.mjs` | - | done |
| 02 | Trang Alarms đọc dữ liệu thật, tự làm mới | AC-04, AC-05, AC-06, AC-12 | `TotalParking/Views/Home/Alarms.cshtml`, `specs/canh-bao-su-co/verify-trang-alarms.mjs` | task-01-bang-va-api.md | done |
| 03 | PLC mất kết nối sinh cảnh báo tự động | AC-07, AC-08, AC-09, AC-12 | `TotalParking/Services/Plc/CanhBaoPlcService.cs`, `TotalParking/Services/Plc/PlcHost.cs`, `TotalParking/Services/CanhBaoRepository.cs`, `TotalParking/TotalParking.csproj`, `specs/canh-bao-su-co/verify-canh-bao-plc.mjs` | task-01-bang-va-api.md | done |
| 04 | Nút ghi nhận sự cố khẩn thay cho ESTOP giả | AC-10, AC-11, AC-12 | `TotalParking/Views/Home/OperationControl.cshtml`, `TotalParking/Controllers/AlarmController.cs`, `specs/canh-bao-su-co/verify-nut-su-co.mjs` | task-01-bang-va-api.md | done |

Task 02, 03, 04 đều phụ thuộc task 01 nhưng độc lập với nhau. Vẫn chạy tuần tự
vì task 03 và 04 cùng sửa file mà task 01 tạo ra.

## Review log

- **Round 1 (24/09):** hai reviewer ngữ cảnh mới — Fact Checker + Contract
  Verifier, và Failure-mode + Assumption destroyer + Scope critic. 18 phát hiện
  thô, gộp trùng còn 15. **Người dùng nhận toàn bộ 15.**
  - **Phát hiện nặng nhất nằm ngoài packet:** `connected_changed_at` bị reset cả
    112 dòng mỗi lần ứng dụng khởi động — lỗi của packet trước, chặn task 03. Đã
    kiểm chứng bằng truy vấn thật và **đã sửa ngay** theo quyết định người dùng.
  - Hai phát hiện là **lỗi trình bày của tôi**: viết "block 89 mất đúng 3 giây"
    trong khi packet trước chỉ ghi `12/15 ping`, và viết "vòng chạy 30 giây"
    trong khi vòng ghi `is_connected` là 5 giây (`PlcTrangThaiWriter.NhipMs`).
    Cả hai là suy diễn trình bày như số đo. Đã sửa.
  - Sweep: AC viết lại từ 8 thành 12; thêm 6 mục Out of scope; thêm `csproj` vào
    Ownership task 03; đổi phạm vi kiểm nhãn về đúng một file; chốt từ vựng bộ
    lọc ở task 01; bỏ trạng thái màu cục bộ ở task 04.
- Round 2: chưa chạy. Theo B4, phát hiện sau vòng này phải có bằng chứng lúc chạy.
