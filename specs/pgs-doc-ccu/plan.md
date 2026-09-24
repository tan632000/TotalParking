# Đọc cảm biến đỗ thường qua CCU
Specs-Contract: process-first-ready-v1

## Scope decision (C1 — 2026-09-24)

- **Existing:** vòng nền và vòng đời IIS ở `TotalParking/Services/Pgs/PgsHost.cs:25`;
  socket + giãn nhịp khi lỗi ở `TotalParking/Services/Pgs/PgsConnection.cs:61-73`;
  endpoint chẩn đoán ở `TotalParking/Controllers/PgsStatusController.cs:19`;
  khoá cấu hình `pgs:*` ở `TotalParking/Web.config:46-55`.
- **Minimum change:** thay đường đọc khung nhị phân ZCU (tự suy luận, không có
  đặc tả) bằng đường đọc CCU đúng giao thức nhà cung cấp, rồi phơi số liệu thật
  kèm cờ mất kết nối ở `/PgsStatus/Index`.
- **Expansion signals:** hai tín hiệu bị loại khỏi phạm vi — nối số vào bảng LED
  (`TotalParking/Services/LedPanelRepository.cs:111`) và ánh xạ từng cảm biến về
  từng ô đỗ theo bản vẽ. Cả hai đổi hành vi ngoài hiện trường.
- **User decision: KEEP** — lấy số đúng trước đã, để đối chiếu thực địa vài ngày
  rồi mới cho nó điều khiển bảng LED thật.

### Bằng chứng mở packet

Đo lúc 11:25 ngày 24/09 trên hệ thống đang chạy:

| Nguồn | Ô đỗ thường trống |
|---|---|
| `v_led_capacity.free_standard` → bảng LED | 80 |
| CCU `192.169.1.75:2000` | 55 (20 có xe, 4 lỗi, 79 cảm biến lắp) |

`/PgsStatus/Index` hiện báo `co_xe = 0` và `khong_giai_duoc = 235`. Mẫu nền mà
`TotalParking/Services/Pgs/PgsFrame.cs:46` giả định chỉ khớp 16/72 byte trên dữ
liệu thật. Người dùng đã xác nhận số liệu CCU (20 xe) đúng thực tế.

Tài liệu `docs/Giao-thức-giao-tiếp-giữa-máy-tính-và-thiết-bị-thu-thập-trung-tâm-CCU.pdf`
mục 2.2.1 và 2.2.2 mô tả đầy đủ giao thức; CRC ở mục 2.1 kiểm khớp 7/7 chuỗi
mẫu trong chính tài liệu.

### Số đo ràng buộc thiết kế (11:52 ngày 24/09, 40 giây)

| Đo được | Giá trị | Ràng buộc nó đặt ra |
|---|---|---|
| CCU phục vụ 2 client song song | có (33 và 61 gói) | script kiểm chứng được mở socket riêng, không đá site |
| Khe giữa 2 gói cùng một ZCU | 2,50 s, min = max | ngưỡng quá hạn 10 s; timeout đọc không được coi là thiết bị chết |
| Dạng CRC firmware gửi | chữ HOA 91/91 | vẫn so không phân biệt hoa/thường |
| Khung `$CCU,01,OK*50#` (3 trường) | 10 khung/40 s | bắt buộc lọc theo `$CCU,02` trước khi đọc trường |

## Out of scope

- **Cổng `ZONES` của bảng LED** và mọi view sức chứa trong CSDL giữ
  nguyên nguồn cũ. Cổng `TOTAL` đã được nối sang cảm biến ở task 02 theo
  chỉ đạo trực tiếp của người dùng sau khi số liệu được xác nhận đúng —
  xem `task-02-led-tong.md` mục "Vì sao task này nằm ngoài C1 ban đầu".
- Ánh xạ cảm biến về từng ô đỗ và chia số theo zone.
- Sửa nguyên nhân 4/5 ZCU mất kết nối với CCU (việc hiện trường).
- Cấu hình thiết bị: không ghi bảng map cảm biến, không reset, không đổi IP.
- Chống nhiễu theo từng cảm biến — đường CCU công bố mọi gói CRC hợp lệ.

## Coverage profile

| ID | Outcome | Change kinds | Material surfaces | Ambiguity/action | Risk/evidence | Required proof |
|---|---|---|---|---|---|---|
| CP-01 | `/PgsStatus/Index` trả số cảm biến khớp dữ liệu CCU | code mới, thay lớp giải mã | endpoint HTTP, socket TCP tới CCU | đã rõ — giao thức có đặc tả, đã đo | elevated — tích hợp thiết bị ngoài | live: so endpoint với đọc trực tiếp CCU |
| CP-02 | Số của ZCU mất kết nối tách khỏi số sống, kèm tuổi dữ liệu | code mới | endpoint HTTP | đã rõ — C1 và C2 đều chốt tách hai nhóm | elevated — số đóng băng trông như số mới | live: đối chiếu cờ `X3` và đồng hồ từng ZCU |
| CP-03 | Khung sai CRC hoặc sai loại bị bỏ, không làm hỏng số | code mới | endpoint chẩn đoán loopback | đã rõ | elevated — khung rác làm số nhảy loạn | live: nạp khung hỏng qua cổng tự kiểm |
| CP-04 | SCADA không còn mở socket tới 5 ZCU, giữ đúng 1 tới CCU | gỡ đường cũ | socket TCP, cấu hình, csproj | đã rõ | routine | live: bảng kết nối TCP của máy chủ |
| CP-05 | Bản đang phục vụ đúng là bản vừa build | quy trình | IIS, thư mục deploy | đã rõ — app chạy từ `Documents\Web\totalParking` | elevated — đo nhầm bản cũ dẫn tới sửa nhầm chỗ | live: so dấu vân tay DLL source ↔ deploy |
| CP-06 | Bảng LED đầu hầm hiện số đỗ thường thật, cổng ZONES không đổi | code mới, đổi nguồn số | bảng LED vật lý ngoài hiện trường | đã rõ — người dùng chốt cách tính và hành vi khi mất kết nối | elevated — tài xế đọc số này để chọn hướng | live: so endpoint LED với endpoint PGS, cộng kiểm tĩnh đường zone |

## Acceptance criteria

| ID | EARS criterion | Proof |
|---|---|---|
| AC-01 | When CCU đang đẩy dữ liệu, `/PgsStatus/Index` shall trả bốn con số của nhóm ZCU đang kết nối khớp đúng với dữ liệu đọc trực tiếp từ CCU tại cùng thời điểm. | `node specs\pgs-doc-ccu\verify-ccu.mjs` |
| AC-02 | When một ZCU có `X3 = 0`, endpoint shall xếp số của ZCU đó vào nhóm đóng băng riêng, kèm số giây kể từ lần cuối `X3 = 1`, và shall không cộng nó vào nhóm đang kết nối. | `node specs\pgs-doc-ccu\verify-qua-han.mjs` |
| AC-03 | When một ZCU không có gói mới quá 10 giây, endpoint shall đánh dấu ZCU đó quá hạn kể cả khi `X3 = 1`. | `node specs\pgs-doc-ccu\verify-qua-han.mjs` |
| AC-04 | When nhận khung sai CRC hoặc khung không phải `$CCU,02`, hệ thống shall bỏ khung đó, tăng bộ đếm tương ứng, và giữ nguyên số đang công bố. | `node specs\pgs-doc-ccu\verify-ccu.mjs` |
| AC-05 | When đã chuyển sang đọc CCU, SCADA shall không còn kết nối TCP nào tới `.70`–`.74`, và shall giữ đúng một kết nối Established tới `.75`. | `node specs\pgs-doc-ccu\verify-ccu.mjs` |
| AC-06 | When đo bất kỳ tiêu chí nào ở trên, `bin\TotalParking.dll` ở cây mã nguồn và ở bản deploy shall trùng dấu vân tay; lệch thì phép đo dừng ngay. | `node specs\pgs-doc-ccu\verify-ccu.mjs` |
| AC-07 | When cảm biến có dữ liệu, `LedStatus/Index → capacity.free_standard` shall bằng tổng số cảm biến báo trống, và shall khác `total_standard` khi có xe đang đỗ. | `node specs/pgs-doc-ccu/verify-led-total.mjs` |
| AC-08 | When ghi đè số trống, `total_standard` shall giữ nguyên theo CSDL (sức chứa thật của bãi, không phải số cảm biến đã lắp). | `node specs/pgs-doc-ccu/verify-led-total.mjs` |
| AC-09 | When tính số theo zone, hệ thống shall không gọi tới nguồn cảm biến; toàn repo chỉ có đúng một chỗ gọi, nằm trong `GetCapacity()`. | `node specs/pgs-doc-ccu/verify-led-total.mjs` |

## Tasks

| # | Task | Criteria | Primary ownership | Dependencies | Status |
|---|---|---|---|---|---|
| 01 | Chuyển tầng cảm biến đỗ thường sang đọc CCU | AC-01 … AC-06 | `TotalParking/Services/Pgs/CcuFrame.cs`, `CcuConnection.cs`, `PgsHost.cs`, `TotalParking/Controllers/PgsStatusController.cs`, `TotalParking/Services/DeviceProbeService.cs`, `TotalParking/Web.config`, `TotalParking/TotalParking.csproj` | - | done |
| 02 | Bảng LED đầu hầm hiện số chỗ đỗ thường thật | AC-07 … AC-09 | `TotalParking/Services/Led/StandardFreeSource.cs`, `TotalParking/Services/LedPanelRepository.cs`, `TotalParking/Services/Led/LedFrameBuilder.cs`, `TotalParking/TotalParking.csproj` | task-01-doc-ccu.md | done |

Task 01 gộp cả việc tắt đường ZCU và bật đường CCU là quyết định C2 của người
dùng: hai việc đó không tách được — để riêng thì task đầu đo trong điều kiện
không thật, và `PgsHost.cs` bị hai task cùng sửa. Đổi lại nó vượt mức "khoảng
năm file" thường lệ; đó là ngoại lệ có chủ ý, không phải sơ suất.

Task 02 mở sau, theo chỉ đạo trực tiếp của người dùng khi task 01 đã xong và số
liệu được xác nhận đúng thực tế. Nó không đi qua vòng C1/C2 riêng — lý do và
hai quyết định hành vi kèm theo ghi trong `task-02-led-tong.md`.

## Review log

- **Round 1 (24/09):** hai reviewer ngữ cảnh mới, vai trò Fact Checker + Contract
  Verifier và Failure-mode + Assumption destroyer + Scope critic. 20 phát hiện
  thô, gộp trùng theo nguyên nhân gốc còn 15.
  - **2 phát hiện bị bác bỏ bằng phép đo**, không phải bằng tranh luận: "CCU chỉ
    có một khe server nên script sẽ đá văng site" (đo: phục vụ được 2 client
    song song) và "CRC có thể là chữ thường" (đo: 91/91 chữ hoa).
  - **15 phát hiện còn lại: người dùng nhận toàn bộ.** Đã áp vào packet — xem
    bảng đối chiếu trong `task-01-doc-ccu.md`.
  - Sweep: gộp task "gỡ đường ZCU" (khi đó đánh số 02) vào task 01 — không liên
    quan tới task 02 hiện nay là task bảng LED; AC viết lại từ 4 thành
    6; thêm CP-03, CP-04, CP-05; sửa hai trích dẫn lệch (`PgsFrame.cs:52` → `:46`,
    `PgsConnection.cs:73` → `:61-73`).
- **Round 2 (24/09, sau khi viết mã):** reviewer ngữ cảnh mới soát mã, verdict
  `FAIL` với 4 việc chặn. Đã xử lý toàn bộ 9 phát hiện — bảng đối chiếu ở
  `task-01-doc-ccu.md`. Hai điều đáng ghi:
  - Phát hiện M1 là lỗi thật, vi phạm trực tiếp AC-02: hai lượt duyệt gọi
    `DateTime.UtcNow` riêng nên một ZCU đứng đúng mốc ngưỡng lọt vào cả hai
    nhóm và bị cộng hai lần.
  - Phát hiện M5 chỉ ra AC-02 và AC-03 đang PASS trên tập rỗng vì cả 5 ZCU đều
    khoẻ. Đã thêm `verify-qua-han.mjs` hạ ngưỡng xuống 1 giây để tạo tình huống
    quá hạn thật, nên hai tiêu chí đó giờ có bằng chứng.
- Round 3: chưa chạy. Theo B4, phát hiện sau vòng này phải có bằng chứng lúc chạy.
