# Task 01 — Bảng cảnh báo và API đọc/xác nhận

Status: done

## Outcome

Cảnh báo có nơi lưu bền trong CSDL. Đọc được danh sách có giới hạn qua API, xác
nhận được một cảnh báo, và lần xác nhận thứ hai không đè lên lần đầu.

## Scope

- **In:** bảng `canh_bao`; repository đọc/ghi; `AlarmController` với hai endpoint
  liệt kê và xác nhận; từ vựng `muc_do`/`nguon` chốt cứng; giới hạn trả về.
- **Out:** không đụng giao diện (task 02), không sinh cảnh báo tự động (task 03),
  không đụng nút `ESTOP` (task 04).

### Vì sao không tái dùng `parking_event`

Bảng đó có `session_id`, `from_status`, `to_status` — dành cho vòng đời một phiên
gửi xe. PLC block 21 chết lúc 3 giờ sáng không thuộc phiên của ai. Nhét vào đó thì
`session_id` phải NULL cho mọi dòng và hai cột status mất nghĩa.

### Từ vựng phải chốt ở task này, không phải task 02

Bộ lọc sẵn có ở `Alarms.cshtml:236-254` duyệt `.alarm-row` rồi so
`data-type` và `data-severity` với các giá trị **đã tồn tại trong markup**:

| Thuộc tính | Giá trị hợp lệ |
|---|---|
| `data-type` | `hardware`, `operation`, `maintenance` |
| `data-severity` | `critical`, `high`, `medium`, `low` |

Nếu task 01 định nghĩa `nguon`/`muc_do` bằng từ khác (`PLC`, `CAO`...) thì task 02
render ra thuộc tính không khớp, và người vận hành chọn "Nghiêm trọng" sẽ thấy
**bảng trống giữa lúc có sự cố thật** — trong khi mọi phép kiểm của task 02 vẫn
PASS. Nên `nguon` và `muc_do` dùng đúng hai tập giá trị trên.

### Các cột và lý do

| Cột | Vì sao cần |
|---|---|
| `nguon` | một trong `hardware`/`operation`/`maintenance`, khớp bộ lọc |
| `muc_do` | một trong `critical`/`high`/`medium`/`low`, khớp bộ lọc |
| `ma_loi` | tra cứu, và là thành phần của khoá chống trùng |
| `block_no`, `zone_id` | giao diện đã có hai cột này. **`plc_device` không có `block_no`** — phải `JOIN block b ON b.block_id = d.block_id` khi ghi |
| `thiet_bi`, `mo_ta` | hai cột giao diện đã có |
| `xay_ra_luc` | thời điểm sự cố |
| `het_luc` | khác NULL = đã qua. **Không xoá dòng** — mất lịch sử |
| `xac_nhan_boi`, `xac_nhan_ip`, `xac_nhan_luc` | xem mục dưới |
| `khoa_chong_trung` | khoá duy nhất cho cảnh báo đang mở; task 03 dùng |

### `xac_nhan_boi` là tên tự khai, không phải danh tính

Hệ thống **không có xác thực** — `grep -rn "Authorize|User.Identity"` trong
`Controllers/` không ra kết quả, và `Alarms.cshtml:110,133,156` đang viết cứng
`acknowledgeAlarm(this, 'operator01')`.

Nếu nhận tên từ client mà không nói rõ, cột này trở thành dấu vết kiểm toán giả:
bất kỳ ai trên mạng OT cũng `curl` được với tên người khác. Người dùng chốt ở C2:
**bắt nhập tên lúc bấm, lưu kèm IP máy khách**, và lời AC nói đúng bản chất —
"nhãn người dùng tự khai", không phải "người xác nhận".

### Khoá chống trùng dựa vào ràng buộc CSDL

`khoa_chong_trung` có ràng buộc `UNIQUE`, đặt giá trị khi cảnh báo mở và về
`NULL` khi đóng. InnoDB cho phép nhiều dòng `NULL` trong `UNIQUE` index, nên cách
này chặn được trùng ngay cả khi hai tiến trình chạy song song — điều mà
kiểm-rồi-ghi trong mã không làm được.

Ràng buộc định nghĩa ở task này nhưng chỉ được dùng ở task 03, nên task này phải
có probe riêng khẳng định nó tồn tại, nếu không lỗi sẽ chỉ lộ ra ở task sau.

## Coverage

- CP-01

## Ownership

- Create: `TotalParking/Database/45_canh_bao.sql`
- Create: `TotalParking/Services/CanhBaoRepository.cs`
- Create: `TotalParking/Controllers/AlarmController.cs`
- Create: `specs/canh-bao-su-co/verify-alarm-api.mjs`
- Modify: `TotalParking/TotalParking.csproj`
- Read: `TotalParking/Controllers/CardsController.cs`, `TotalParking/Views/Home/Alarms.cshtml`

## Acceptance

- AC-01: ghi một cảnh báo rồi `GET /Alarm/List` trả về đủ trường; danh sách không
  vượt tập chưa xác nhận cộng 200 dòng gần nhất.
- AC-02: `POST /Alarm/Ack` lưu nhãn người tự khai, IP máy khách, và thời điểm.
- AC-03: xác nhận lần hai bị từ chối, người xác nhận đầu tiên giữ nguyên.
- AC-12: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- none

## Verification Plan

- **Thứ tự triển khai bắt buộc:** chạy `45_canh_bao.sql` và xác nhận bảng cùng
  ràng buộc `UNIQUE` tồn tại **trước** khi chép DLL. Chiều ngược lại sinh
  `Table doesn't exist` mỗi lần gọi API.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-alarm-api.mjs"`

- **Named probe:** tám phép kiểm có tên — `deploy_khop_source`,
  `khoa_chong_trung_la_duy_nhat`, `tu_vung_khop_bo_loc`, `ghi_roi_doc_lai_duoc`,
  `xac_nhan_luu_ten_va_ip`, `ack_lan_hai_khong_de_lan_dau`,
  `danh_sach_co_gioi_han`, `don_sach_du_lieu_thu`.

- **Reachability:** site chạy ở `http://localhost:8080`; CSDL truy vấn được bằng
  `mysql.exe`; route mặc định `{controller}/{action}/{id}` trong `RouteConfig.cs`
  tự nhận `/Alarm/List` — chưa có controller nào tên `Alarm` nên không xung đột.

- **Oracle:**
  - `khoa_chong_trung_la_duy_nhat`: truy vấn `information_schema.STATISTICS`
    khẳng định có index `UNIQUE` trên `khoa_chong_trung`. Không có probe này thì
    lỗi chỉ lộ ra ở task 03 khi bảng đã ngập cảnh báo trùng.
  - `tu_vung_khop_bo_loc`: đọc `Alarms.cshtml`, trích các giá trị `data-type` và
    `data-severity` đang có, rồi khẳng định ràng buộc/`ENUM` của hai cột
    `nguon`/`muc_do` phủ đúng tập đó. Đây là chỗ giữ hợp đồng giữa task 01 và 02.
  - `ghi_roi_doc_lai_duoc`: chèn một cảnh báo có `ma_loi` mang dấu thời gian để
    không đụng dữ liệu thật, rồi `GET /Alarm/List` phải trả đúng dòng đó.
  - `xac_nhan_luu_ten_va_ip`: `POST /Alarm/Ack` với một tên; đọc CSDL khẳng định
    `xac_nhan_boi`, `xac_nhan_ip`, `xac_nhan_luc` đều khác NULL.
  - `ack_lan_hai_khong_de_lan_dau`: gọi `Ack` lần hai với tên khác; phải bị từ
    chối và `xac_nhan_boi` vẫn là tên đầu tiên. Cài bằng
    `UPDATE ... WHERE xac_nhan_luc IS NULL` rồi xét số dòng bị ảnh hưởng.
  - `danh_sach_co_gioi_han`: chèn 210 cảnh báo đã xác nhận; `GET /Alarm/List`
    phải trả không quá 200 dòng đã xác nhận.
  - `don_sach_du_lieu_thu`: cuối lần chạy mọi dòng do script tạo đều bị xoá; số
    dòng trong bảng trở lại đúng như trước khi chạy.

- **Counterexample:**
  - nếu `nguon`/`muc_do` dùng từ vựng khác thì `tu_vung_khop_bo_loc` FAIL — bắt
    được lỗi trước khi nó thành "bảng trống khi lọc" ở task 02.
  - nếu `Ack` không có điều kiện `xac_nhan_luc IS NULL` thì
    `ack_lan_hai_khong_de_lan_dau` FAIL.
  - nếu API trả toàn bộ bảng thì `danh_sach_co_gioi_han` FAIL.
  - nếu script để lại dòng thử thì `don_sach_du_lieu_thu` FAIL — một phép kiểm
    không được làm bẩn dữ liệu vận hành.

- **Artifacts:** `specs/canh-bao-su-co/artifacts/alarm-api.json` — số dòng trước
  và sau, từ vựng đối chiếu, và nội dung cảnh báo thử; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-alarm-api.mjs"`
Exit: 0
Base: 383ad696b1bf127f30367c194abd9d20c337eb0c
Head: 750d145401f4b29d3f1ef68bb1b7837af3a1e33e

```text
  PASS  deploy_khop_source  | 46e96c01df97
  PASS  khoa_chong_trung_la_duy_nhat  | NON_UNIQUE=0 (0 la duy nhat)
  PASS  tu_vung_khop_bo_loc  | data-type [hardware,maintenance,operation] vs nguon [hardware,maintenance,operation]; data-severity [critical,high,low,medium] vs muc_do [critical,high,low,medium]
  PASS  ghi_roi_doc_lai_duoc  | tim thay id=1, muc_do=critical, chua_xac_nhan=true
  PASS  xac_nhan_luu_ten_va_ip  | HTTP 200, boi="Nguoi Kiem Thu", ip="::1", luc=co
  PASS  ack_lan_hai_khong_de_lan_dau  | HTTP 409 (mong doi 409), nguoi xac nhan van la "Nguoi Kiem Thu"
  PASS  danh_sach_co_gioi_han  | API tra 200 dong, trong do 200 da xac nhan (gioi han 200)
  PASS  don_sach_du_lieu_thu  | so dong 0 -> 0

8/8 PASS
EXIT=0
```

### Hợp đồng từ vựng đã được khoá lại bằng phép kiểm

`tu_vung_khop_bo_loc` đọc thẳng `Alarms.cshtml`, trích tập giá trị mà bộ lọc đang
dùng, rồi đối chiếu với `ENUM` của hai cột. Cả hai tập khớp chính xác.

Đây là phép kiểm quan trọng nhất về mặt hợp đồng: nếu `nguon`/`muc_do` dùng từ
khác, task 02 sẽ render ra thuộc tính không khớp và người vận hành chọn một mức
lọc sẽ thấy **bảng trống giữa lúc có sự cố thật** — trong khi mọi phép kiểm về số
dòng của task 02 vẫn PASS.

### Ba hành vi được chứng minh bằng phép thử thật

| Hành vi | Bằng chứng |
|---|---|
| Xác nhận lưu cả tên tự khai lẫn IP | `boi="Nguoi Kiem Thu", ip="::1"` |
| Xác nhận lần hai bị từ chối | HTTP `409`, người đầu tiên giữ nguyên |
| Danh sách có trần | chèn 210 dòng đã xác nhận, API trả đúng 200 |

Phép kiểm trần là loại dễ bỏ sót nhất: không có nó thì lỗi chỉ lộ ra sau vài
tháng, khi bảng đủ lớn để làm chết trang cảnh báo.

### Ngoài phạm vi đã ghi nhưng làm luôn

`44_poll_du.sql` chưa được thêm vào `csproj` ở packet trước — thiếu sót của tôi.
Đã thêm cùng lúc với `45_canh_bao.sql`. File `.sql` không cần biên dịch nên nó
không gây lỗi build, nhưng để sót thì nó biến mất khỏi solution.

### Hạn chế còn lại

1. **`xac_nhan_boi` không phải danh tính đã xác thực.** Hệ thống chưa có đăng
   nhập; bất kỳ ai trên mạng OT cũng gọi được `POST /Alarm/Ack` với tên người
   khác. Đã ghi rõ trong chú thích của controller để không ai dựa vào cột đó như
   bằng chứng.
2. **Chưa có cơ chế giữ-xoá.** Bảng chỉ lớn lên; đợt này chỉ chặn ở đầu ra bằng
   trần 200 dòng, không xoá gì.
3. **Chưa ai đọc bảng này.** Trang `Alarms` (task 02) và dịch vụ sinh cảnh báo
   (task 03) chưa làm, nên API mới hiện chỉ có phép kiểm gọi tới.
4. `GET /Alarm/List` trả tất cả cảnh báo chưa xác nhận **không giới hạn**. Nếu
   một sự cố diện rộng sinh hàng nghìn cảnh báo chưa ai xác nhận thì phản hồi vẫn
   lớn. Trần chỉ áp cho phần đã xác nhận.
