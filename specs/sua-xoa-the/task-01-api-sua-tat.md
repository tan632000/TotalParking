# Task 01 — API sửa / tắt / bật thẻ, ba lớp chặn, nhật ký riêng

Status: done

## Outcome

Sửa được thông tin thẻ và tắt/bật lại được thẻ qua HTTP; mã thẻ không đổi được
bằng bất kỳ đường nào; không thẻ nào khác bị đụng; tắt bị chặn khi thẻ đang gắn
với một chiếc xe; mọi lỗi CSDL hiện thành câu đọc được; mọi thao tác để lại dấu
vết trong một file nhật ký riêng.

## Scope

- **In:** `card_id` thêm vào `GET /Cards/List`; `POST /Cards/Update`;
  `POST /Cards/SetActive`; ba lớp chặn; `CardAdminLog`; ánh xạ lỗi CSDL.
- **Out:** không `DELETE` dòng nào; không đụng `Preview`/`Import`/`Template`;
  không sửa `SlotOccupancyReader`; không thêm giao diện (task 02).

### Không có xoá cứng trong task này

Lý do đầy đủ ở `plan.md`. Tóm tắt: `SlotOccupancyReader.cs:213-222` dùng
`parking_card` làm trọng tài để chọn bố cục giải mã thanh ghi ô đỗ, nên xoá hẳn
một dòng làm hỏng việc đọc ô cho những lần gửi xe **sau đó** — ngoài tầm với của
mọi lớp chặn tại thời điểm thao tác.

**Không được thêm `DELETE FROM parking_card` vào bất kỳ đâu trong packet này.**

### Tắt thẻ cũng khoá được tài xế ngoài cổng

`CardScanService.cs:95`:

```csharp
if (!cardActive) return CardScanDecision.Deny(RejectReason.InactiveCard);
```

Dòng này chạy **trước** khi tra phiên đang mở (`:97-103`). Nên tắt thẻ của xe
đang nằm trong bãi nghĩa là tài xế quẹt thẻ ở cổng và bị từ chối. Ba lớp chặn
dưới đây tồn tại vì lý do đó, không phải vì thủ tục.

### Ba lớp chặn nằm TRONG câu lệnh, không kiểm trước

Kiểm trước rồi ghi sau để hở một cửa sổ: vòng quét ô đỗ chạy 45 giây một lượt và
một xe có thể vừa được gửi vào giữa hai bước.

```sql
UPDATE parking_card c
LEFT JOIN plc_slot_state s ON s.card_code = c.card_code
LEFT JOIN parking_session p ON p.card_id = c.card_id AND p.active_card_id IS NOT NULL
LEFT JOIN plc_request r ON r.card_code = c.card_code
                       AND r.received_at > NOW() - INTERVAL 24 HOUR
SET c.is_active = 0
WHERE c.card_id = @id
  AND s.card_code IS NULL AND p.card_id IS NULL AND r.card_code IS NULL
```

`ROW_COUNT() = 0` nghĩa là **hoặc** không tìm thấy **hoặc** bị chặn. Đọc lại dòng
sau đó chỉ để **dựng thông báo cho đúng lý do**, không phải để quyết định.

**Bật lại (`is_active = 1`) không cần lớp chặn nào** — nó chỉ mở ra, không khoá
ai cả.

### Lớp `plc_request` PHẢI có giới hạn 24 giờ

`plc_request` là nhật ký chỉ ghi thêm (`Database/06_plc_device.sql:61-63`;
`Services/PlcRequestRepository.cs` chỉ có `INSERT`, không có đường xoá). Đo được
trên CSDL sống: **517 dòng / 18 mã, 13 mã khớp thẻ thật, nhưng chỉ 4 mã có lượt
quẹt trong 24 giờ qua**.

Bỏ điều kiện thời gian là khoá vĩnh viễn 13 thẻ thật, và con số đó chỉ lớn lên
cho tới lúc không thẻ nào tắt được nữa — tính năng chết dần mà không ai bấm ra
được lý do.

### Định danh bằng `card_id`

`GET /Cards/List` hiện không trả `card_id` (`CardsController.cs:60-77`). Task này
bổ sung nó, vì task 02 không có gì khác để chỉ đúng dòng.

**Không định danh bằng `card_code` nhận từ thân POST.** Khuôn sẵn có
(`ParkingCardRepository.cs:273`) dùng `WHERE c.card_code = @code` vì nó phục vụ
nhập file; sao chép khuôn đó vào đây thì một mã gửi lên sẽ trỏ sang dòng khác, và
phép kiểm `ma_the_khong_doi_duoc` **vẫn PASS** vì thẻ thử không đổi. Đó là lý do
có thêm phép kiểm vân tay toàn bảng.

### `card_code` khoá ở máy chủ

Câu `UPDATE` **không được có `card_code` trong `SET`** — không nhận rồi so sánh
rồi từ chối, chỉ đơn giản không bao giờ ghi nó.

### Bốn lỗi CSDL phải dịch thành câu đọc được

`@@sql_mode` trên máy chủ có `STRICT_TRANS_TABLES`, nên vượt độ dài là **lỗi**
chứ không phải cắt âm thầm.

| Mã | Khi nào | Câu trả về phải nói |
|---|---|---|
| 1062 | `card_no` trùng thẻ khác (`uq_parking_card_no`) | số thẻ đã có ở thẻ nào |
| 1452 | `customer_type_id`/`weight_class_id` không tồn tại | giá trị nào không hợp lệ |
| 1406 | chuỗi vượt độ dài cột | cột nào, giới hạn bao nhiêu |
| 1451 | không xảy ra trong packet này | — |

Repo đã có tiền lệ bắt theo `ex.Number`: `CanhBaoRepository.cs:121` (1062),
`CardScanService.cs:22`, `MonitorController.cs:336`.

Độ dài cột: `card_no` 16, `card_type` 16, `plate` 16, `weight_text` 32,
`vehicle_name` 64, `customer_name` 128. `CardImportParser.cs:54` đã có tiền lệ
"giới hạn độ dài theo đúng schema".

### Trường được sửa

`card_no`, `card_type`, `vehicle_name`, `plate`, `customer_name`, `weight_text`,
`expiry_date`, `customer_type_id`, `weight_class_id`.

`is_active` **không** nằm trong `Update` — nó có endpoint riêng (`SetActive`) vì
nó là thao tác có lớp chặn, còn sửa thông tin thì không.

`customer_type_id` và `weight_class_id` cần nguồn giá trị cho giao diện; task này
trả kèm danh sách hai bảng tra cứu trong `GET /Cards/List` để task 02 dựng ô chọn
mà không phải viết cứng id.

### Nhật ký ra file riêng

`PlcAuditLog` xoay ở 8 MB giữ một file cũ (`PlcAuditLog.cs:31-34`) và đang nhận
hàng trăm dòng mỗi phút từ vòng quét — dấu vết quản trị sẽ biến mất sau vài ngày.

`CardAdminLog` là **file `.cs` mới**, ghi ra `App_Data/card_admin.log`. Vì là file
mới nên **`TotalParking.csproj` phải được cập nhật** (hard rule 1 của `AGENTS.md`);
thiếu bước đó thì file không được biên dịch và `CardsController` gọi nó sẽ lỗi
`CS0103`.

Mỗi dòng ghi: thời điểm, thao tác, `card_id`, `card_code`, người thao tác (tự
khai), địa chỉ máy khách, kết quả, và với `Update` thì cả **giá trị cũ lẫn mới**.
Với `SetActive` phải đọc dòng **trước** khi ghi — sau đó thì không còn giá trị cũ.

## Coverage

- CP-01, CP-02, CP-03, CP-04, CP-05, CP-06

## Ownership

- Modify: `TotalParking/Services/ParkingCardRepository.cs`
- Modify: `TotalParking/Controllers/CardsController.cs`
- Modify: `TotalParking/TotalParking.csproj`
- Create: `TotalParking/Services/CardAdminLog.cs`
- Create: `specs/sua-xoa-the/verify-api-the.mjs`
- Read: `TotalParking/Services/Plc/PlcAuditLog.cs`, `TotalParking/Services/CardScanService.cs`, `TotalParking/Services/CardImportParser.cs`, `TotalParking/Controllers/AlarmController.cs`

## Acceptance

- AC-01 … AC-10 như `plan.md`.
- AC-15: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- none

## Verification Plan

- **Build và deploy trước khi đo.**

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\sua-xoa-the\verify-api-the.mjs"`

- **Named probe:** mười lăm phép kiểm có tên — `deploy_khop_source`,
  `list_co_card_id`, `sua_luu_duoc`, `khong_the_nao_khac_bi_dung`,
  `ma_the_khong_doi_duoc`, `tat_va_bat_lai_duoc`, `chan_tat_khi_trong_o_do`,
  `chan_tat_khi_co_phien`, `chan_tat_khi_vua_quet_24h`,
  `quet_cu_hon_24h_khong_chan`, `khong_o_do_nao_bien_mat`,
  `loi_csdl_thanh_cau_doc_duoc`, `thieu_truong_khong_xoa_du_lieu`,
  `nhat_ky_ghi_du`, `khoi_phuc_nguyen_trang`.

- **Reachability:** site chạy ở `http://localhost:8080`; CSDL truy vấn được bằng
  `mysql.exe`; bản deploy ở `C:\Users\Admin\Documents\Web\totalParking`; nhật ký
  đọc từ `<deploy>\App_Data\card_admin.log`.

- **Oracle:**
  - **Chỉ thao tác trên thẻ do script tự tạo.** 613 thẻ thật không được sửa hay
    tắt. Thẻ thử mang `source_label` có tiền tố riêng; `finally` xoá theo nhãn đó
    (xoá cứng một dòng **do script tự chèn** là hợp lệ — nó chưa bao giờ nằm
    trong thanh ghi PLC nào).
  - `khong_the_nao_khac_bi_dung`: chụp
    `SELECT COUNT(*), MD5(GROUP_CONCAT(card_id, card_code, card_no, COALESCE(plate,''), COALESCE(customer_name,''), is_active ORDER BY card_id))`
    của **toàn bảng trừ thẻ thử**, trước và sau mỗi phép sửa. Lệch là FAIL. Đây
    là phép kiểm duy nhất bắt được lỗi định danh bằng `card_code`.
  - `chan_tat_khi_trong_o_do`: **không chèn dòng mới** vào `plc_slot_state` —
    bảng seed sẵn theo khoá `(block_id, slot_index)` (`17_plc_slot_state.sql:60`)
    nên chèn là tạo ô ma, còn xoá là **mất vĩnh viễn một ô thật** khỏi vòng quét.
    Thay vào đó `UPDATE` một ô **đang trống của khối còn nhiều chỗ nhất** rồi
    `UPDATE ... SET card_code = NULL` khi dọn.

    Ô đó tính ngay vào `v_slot_taken`, chảy vào `BlockAllocator.cs:46`
    (`free_capacity`) và `BlockMapRepository.cs:59` — tức **bớt một chỗ trống
    thật** trong lúc đo. Vì vậy: chọn khối nhiều chỗ trống nhất để `-1` không đẩy
    nó về 0; giữ cửa sổ dưới 20 giây; và khẳng định `COUNT(*) FROM v_slot_taken`
    trở về đúng số cũ. Nếu trong cửa sổ đó có `vehicle_routing` nào `ROUTED` thì
    **ghi vào artifact**, không giấu.
  - `khong_o_do_nao_bien_mat`: `COUNT(*) FROM plc_slot_state` trước và sau phải
    bằng nhau — chốt chặn cho đúng lỗi "dọn bằng DELETE".
  - `chan_tat_khi_co_phien`: tạo một `parking_session` trỏ tới thẻ thử với
    `active_card_id` khác NULL; dọn ở `finally`.
  - `chan_tat_khi_vua_quet_24h` và `quet_cu_hon_24h_khong_chan`: chèn một dòng
    `plc_request` mang mã thẻ thử với `received_at = NOW()`, khẳng định bị chặn;
    rồi đổi `received_at` về `NOW() - INTERVAL 30 HOUR`, khẳng định **không** bị
    chặn. Thiếu phép thứ hai thì một cài đặt bỏ quên điều kiện thời gian vẫn PASS
    — và đó chính là lỗi khoá vĩnh viễn 13 thẻ thật.
  - `loi_csdl_thanh_cau_doc_duoc`: ép cả ba — `card_no` trùng một thẻ thử khác
    (1062), `customer_type_id` = 99 (1452), `plate` dài 40 ký tự (1406). Với cả
    ba: mã HTTP khác 200, thân JSON có trường lỗi, và **không chứa** chuỗi thô
    như `Duplicate entry` hay `Data too long`.
  - `nhat_ky_ghi_du`: đọc `card_admin.log`, đếm **số dòng tăng thêm** và khẳng
    định các dòng mới chứa mã thẻ thử cùng tên người thao tác. File này chỉ nhận
    ghi từ đường quản trị nên không bị vòng quét làm nhiễu — khác hẳn
    `plc_audit.log`.
  - `khoi_phuc_nguyen_trang`: số thẻ trở lại như lúc bắt đầu; không còn dòng
    `plc_request`/`parking_session` nào do script tạo; `plc_slot_state` đủ số ô
    và ô mượn đã về `NULL`. Khôi phục thất bại là FAIL nhìn thấy được kèm câu
    lệnh sửa tay.

- **Counterexample:**
  - định danh bằng `card_code` nhận từ POST → `khong_the_nao_khac_bi_dung` FAIL
    trong khi `ma_the_khong_doi_duoc` vẫn PASS; đó là lý do cần cả hai.
  - đưa `card_code` vào `SET` → `ma_the_khong_doi_duoc` FAIL.
  - bỏ điều kiện 24 giờ ở `plc_request` → `quet_cu_hon_24h_khong_chan` FAIL.
  - dọn `plc_slot_state` bằng `DELETE` → `khong_o_do_nao_bien_mat` FAIL.
  - trả nguyên văn lỗi CSDL → `loi_csdl_thanh_cau_doc_duoc` FAIL.
  - quên cập nhật `.csproj` → build lỗi `CS0103`, không probe nào chạy được.

- **Artifacts:** `specs/sua-xoa-the/artifacts/api-the.json` — thẻ thử, từng bước
  và kết quả, vân tay toàn bảng trước/sau, ô đỗ đã mượn, số dòng nhật ký tăng
  thêm, và mọi `vehicle_routing` xảy ra trong cửa sổ đo; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\sua-xoa-the\verify-api-the.mjs"`
Exit: 0
Base: e2ef8ac07838eb6f22a7f4e95fa7ce5ef69a51b9
Head: b21e85bac4946d594d61ce2dd62a0e1c978648ad

```text
  PASS  deploy_khop_source  | 33fe7e26a50e
  PASS  list_co_card_id  | card_id=638 (mong 638), loai_khach 3/3, hang_tai 3/3
  PASS  sua_luu_duoc  | HTTP 200; doc lai: 51H-99999 | Khach Thu A sua | 2 | 1 | 31/12/2027
  PASS  khong_the_nao_khac_bi_dung  | van tay toan bang (tru the thu) khong doi: 613 dong
  PASS  ma_the_khong_doi_duoc  | ma the AAF4DC5B -> AAF4DC5B
  PASS  tat_va_bat_lai_duoc  | tat HTTP 200 -> is_active=0; bat HTTP 200 -> 1
  PASS  chan_tat_khi_trong_o_do  | HTTP 409, is_active=1; khoi 6 con 10 cho; xep xe trong cua so do: 0
  PASS  khong_o_do_nao_bien_mat  | so o trong plc_slot_state: 755 -> 755
  PASS  chan_tat_khi_co_phien  | HTTP 409, is_active=1, ly_do=CoPhienDangMo
  PASS  chan_tat_khi_vua_quet_24h  | vua quet: HTTP 409, is_active=1
  PASS  quet_cu_hon_24h_khong_chan  | quet 30h truoc: HTTP 200, is_active=0 (0 = tat duoc, dung)
  PASS  loi_csdl_thanh_cau_doc_duoc  | trung=409 "So the TCH1Z2FB-B da thuoc ve mot the khac."; id la=409; qua dai=400
  PASS  thieu_truong_khong_xoa_du_lieu  | HTTP 200; sau khi POST thieu 4 truong, doc lai: Xe can giu | 1800KG | TAI | 31/01/2028
  PASS  nhat_ky_ghi_du  | 12 dong mang ma the thu; co dong OK: true; co dong TU_CHOI: true; moi dong deu co ten nguoi: true
  PASS  khoi_phuc_nguyen_trang  | the 613->613, o do 755->755, van tay toan bang khop

15/15 PASS
EXIT=0
```

### Chứng minh ngược phép kiểm quan trọng nhất

`khong_the_nao_khac_bi_dung` là phép kiểm duy nhất bắt được lỗi định danh dòng
bằng `card_code` nhận từ thân POST. Mô phỏng đúng cài đặt sai đó trong một
transaction rồi cuộn lại, trên CSDL thật:

```text
truoc        sau_khi_ghi_de  sau_rollback   ket_luan
7cad4c58cea9 4355dbe0efa7    7cad4c58cea9   probe FAIL (bat duoc loi)
```

Không có phép kiểm này, một cài đặt ghi đè biển số của khách thật vẫn PASS toàn
bộ 14 phép còn lại.

### Vòng rà soát trả về FAIL, và năm phát hiện đã được sửa

| Phát hiện | Sửa |
|---|---|
| `Update` ghi `NULL` đè lên mọi trường **vắng mặt** trong POST — mất hồ sơ DEC của thẻ khách | Phân biệt "vắng mặt" với "gửi lên rỗng"; vắng mặt thì giữ giá trị cũ. Thêm probe `thieu_truong_khong_xoa_du_lieu` |
| Probe mượn ô đỗ thật không có điều kiện bảo vệ — vòng quét 45 giây có thể vừa ghi mã thật vào đó | Mượn kèm `AND card_code IS NULL`, trả về kèm `AND card_code='<mã thử>'`; mượn hụt là ném lỗi, không ghi đè |
| `LyDoBiChan` trả `Ok` trong cửa sổ đua → API báo 200 trong khi `is_active` không đổi | Đọc thêm `c.is_active`; chỉ `Ok` khi cờ đã đúng, ngược lại trả `KhongDoiDuoc` |
| Probe lỗi CSDL chỉ đòi "khác 200" nên không phân biệt lớp kiểm trước với ánh xạ lỗi | Khẳng định đúng từng tầng: 400 cho lớp kiểm trước, 409 cho lỗi CSDL |
| `CardAdminLog` không khử tab/xuống dòng → giá trị người dùng nhập giả mạo được một dòng nhật ký | Khử `\t`, `\r`, `\n` khỏi mọi trường trước khi ghép |

### Hai bẫy của chính script kiểm chứng

`card_no` chỉ `varchar(16)` nên tiền tố thử dài 19 làm bước chèn dữ liệu ném
1406. Và `active_card_id` là **cột sinh** (`status IN (ASSIGNED, ENTERING,
PARKING, PARKED, RETRIEVING) → card_id`), liệt kê nó trong `INSERT` bị từ chối
bằng lỗi 3105. Cả hai là lỗi của phép kiểm, không phải của sản phẩm.

## Hạn chế còn lại

1. **Nhánh `1406` của `DichLoi` không với tới được.** Mọi cột chuỗi sửa được đều
   nằm trong bảng kiểm độ dài ở controller, nên CSDL không bao giờ ném 1406 qua
   đường này. Nhánh đó là phòng thủ chiều sâu, **chưa từng được chạy**.
2. **Nhánh `KhongThay` của `LyDo` cũng không với tới được** — controller đã trả
   404 trước đó.
3. **Nhánh `KhongDoiDuoc` chưa được ép thật.** Nó chỉ xảy ra khi có một cuộc đua
   giữa câu lệnh và lần đọc lại; phép kiểm hiện chứng minh logic bằng đọc mã
   nguồn, không bằng ép cửa sổ đua.
4. **Tên người thao tác là tự khai.** Hệ thống chưa có đăng nhập, nên
   `card_admin.log` ghi tên do người gửi POST đặt. Đã khử ký tự phân tách để
   không giả mạo được *dòng*, nhưng *tên* thì vẫn không xác thực được.
5. **`CardAdminLog` nuốt lỗi ghi file.** Không ghi được nhật ký thì thao tác vẫn
   thành công — chủ ý, để người vận hành không thấy thất bại giả sau khi CSDL đã
   đổi. Đổi lại: AC-10 không được bảo đảm ở runtime nếu thư mục mất quyền ghi.
6. **`verify-api-the.mjs` nhúng tài khoản `root` và mật khẩu CSDL**, theo đúng
   khuôn của các script kiểm chứng đã có trong kho. Đây là nợ chung của cả bộ
   script, không riêng packet này.
