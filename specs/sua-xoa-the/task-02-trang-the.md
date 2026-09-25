# Task 02 — Trang Cards sửa, tắt và bật lại được

Status: done

## Outcome

Người vận hành sửa được thông tin thẻ, tắt được thẻ và bật lại được, ngay trên
trang Quản lý thẻ; biết rõ mình đang tắt thẻ nào trước khi bấm; và thấy lý do khi
bị từ chối.

## Scope

- **In:** cột thao tác trong bảng; modal sửa; hộp xác nhận tắt có hiện **mã
  thẻ**; nút bật lại cho thẻ đang tắt; hiện lý do khi bị từ chối; làm mới bảng
  sau thao tác.
- **Out:** không đụng nhập hàng loạt (`openImport`, `doPreview`, `doImport`,
  `downloadErrors`); không thêm phân trang hay tìm kiếm; không đổi bố cục bảng
  ngoài việc thêm một cột.

### "Tắt" chứ không phải "Xoá" — nhãn phải nói đúng

Packet này **không xoá dòng nào**. Nút mang nhãn "Xoá" trong khi việc thật là
tắt cờ sẽ khiến người vận hành tin rằng thẻ đã biến mất, rồi ngạc nhiên khi nó
vẫn nằm trong bảng. Nhãn phải là **"Ngừng dùng"** và chiều ngược lại là
**"Dùng lại"**.

### Biểu mẫu sửa KHÔNG có ô nhập mã thẻ

Máy chủ đã khoá `card_code` (task 01), nên một ô nhập trên giao diện sẽ **im lặng
không có tác dụng**: người vận hành gõ vào đó, bấm lưu, thấy báo thành công, và
tin rằng mã đã đổi. Đó tệ hơn là không có ô nhập.

Mã thẻ vẫn **hiện ra dạng chữ** trong modal, để người sửa biết mình đang sửa thẻ
nào.

### Hai ô chọn phải lấy từ máy chủ

`customer_type_id` và `weight_class_id` là khoá ngoại. Task 01 trả kèm danh sách
hai bảng tra cứu trong `GET /Cards/List`; giao diện **phải dựng ô chọn từ danh
sách đó**, không viết cứng id.

Trang hiện đang viết cứng danh sách hạng tải ở `Cards.cshtml:54-56` cho bộ lọc.
Đừng nhân bản cách làm đó sang biểu mẫu sửa: thêm một hàng vào `weight_class` thì
biểu mẫu sẽ ghi sai id mà không có gì báo.

### Xác nhận phải hiện mã thẻ

Một hộp thoại chỉ hỏi "Bạn có chắc không?" là vô dụng: người vận hành vừa cuộn
qua 613 dòng và không chắc mình đang đứng ở dòng nào. Hộp xác nhận phải hiện
**mã thẻ và biển số** của đúng dòng sắp tắt.

### Bị từ chối phải nói lý do

Task 01 sinh ra ba đường từ chối (thẻ đang trong ô đỗ, có phiên đang mở, vừa quẹt
trong 24 giờ) cộng ba lỗi dữ liệu (trùng số thẻ, id tra cứu lạ, chuỗi quá dài).
Nếu giao diện chỉ hiện "Không thực hiện được" thì người vận hành bấm lại vài lần
rồi bỏ cuộc — và cả ba lý do đầu đều là điều họ **cần biết**: thẻ đó đang gắn với
một chiếc xe.

### Thẻ đã tắt vẫn phải nhìn thấy

Xoá mềm chỉ có nghĩa nếu người vận hành tìm lại được thẻ đã tắt để bật lên. Bảng
đã có cột **Trạng thái** (`Cards.cshtml:67-77`), nên chỉ cần bảo đảm dòng đã tắt
phân biệt được bằng mắt và nút đổi thành "Dùng lại".

## Coverage

- CP-07

## Ownership

- Modify: `TotalParking/Views/Home/Cards.cshtml`
- Create: `specs/sua-xoa-the/verify-trang-the.mjs`
- Read: `TotalParking/Controllers/CardsController.cs`, `TotalParking/Views/Home/Alarms.cshtml`

## Acceptance

- AC-11: bấm sửa, điền, lưu — bảng hiện giá trị mới và CSDL khớp.
- AC-12: hộp xác nhận tắt có hiện mã thẻ; huỷ thì **không** đổi, xác nhận thì đổi.
- AC-13: khi bị từ chối, trang hiện lý do đọc được và `is_active` không đổi.
- AC-14: `Cards.cshtml` không có ô nhập nào cho `card_code` trong biểu mẫu sửa.
- AC-15: dấu vân tay `Cards.cshtml` và DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-01-api-sua-tat.md

## Verification Plan

- **Build và deploy trước khi đo**, và chép `.cshtml` **riêng** ngoài `bin\*.dll`.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\sua-xoa-the\verify-trang-the.mjs"`

- **Named probe:** chín phép kiểm có tên — `deploy_khop_source`,
  `khong_co_o_nhap_ma_the`, `nhan_noi_dung_tat_khong_phai_xoa`,
  `o_chon_lay_tu_may_chu`, `sua_tu_giao_dien_ghi_duoc`, `xac_nhan_co_hien_ma_the`,
  `huy_thi_khong_doi`, `tu_choi_hien_ly_do`, `don_sach_du_lieu_thu`.

- **Reachability:** site chạy ở `http://localhost:8080`; Chrome tại
  `C:/Program Files/Google/Chrome/Application/chrome.exe`; CSDL truy vấn được;
  khuôn đo theo `specs/canh-bao-su-co/verify-trang-alarms.mjs`.

- **Oracle:**
  - **Chỉ thao tác trên thẻ do script tự tạo**, như task 01.
  - `khong_co_o_nhap_ma_the`: đọc `Cards.cshtml`, khẳng định trong khối biểu mẫu
    sửa không có `<input>` nào gắn với `card_code`. Không cấm chuỗi `card_code`
    trong cả file — nó xuất hiện hợp lệ ở cột bảng và khoá dữ liệu.
  - `nhan_noi_dung_tat_khong_phai_xoa`: khẳng định nút thao tác mang chữ "Ngừng
    dùng"/"Dùng lại", và **không** có chữ "Xoá" trong vùng nút thao tác của bảng.
  - `o_chon_lay_tu_may_chu`: đếm số `<option>` trong hai ô chọn của modal và so
    với `SELECT COUNT(*)` của `customer_type` và `weight_class`. Viết cứng thì
    hai số lệch ngay khi bảng tra cứu đổi.
  - `sua_tu_giao_dien_ghi_duoc`: bấm sửa của đúng dòng thẻ thử, đổi biển số, lưu,
    chờ bảng làm mới, rồi **đọc CSDL** khẳng định giá trị mới. Đọc lại bảng thôi
    là chưa đủ — một cài đặt chỉ sửa DOM mà không gọi API vẫn PASS.
  - `xac_nhan_co_hien_ma_the`: bắt sự kiện `dialog`, khẳng định nội dung **chứa
    mã thẻ** của đúng dòng sắp tắt.
  - `huy_thi_khong_doi`: `dialog.dismiss()`, khẳng định `is_active` vẫn là 1.
    Không có phép kiểm này thì một cài đặt tắt trước rồi mới hỏi vẫn PASS.
  - `tu_choi_hien_ly_do`: mượn một ô đỗ đang trống mang mã thẻ thử — theo **đúng
    ràng buộc ở task 01**: `UPDATE` chứ không chèn, chọn khối còn nhiều chỗ nhất,
    trả về `NULL` khi dọn, và khẳng định `COUNT(*) FROM plc_slot_state` không
    đổi. Bấm tắt và xác nhận, khẳng định trang hiện thông báo **có nội dung** và
    `is_active` vẫn là 1.
  - `don_sach_du_lieu_thu`: xoá thẻ thử và mọi dòng phụ; số thẻ và số ô đỗ trở
    lại như lúc bắt đầu.

- **Counterexample:**
  - modal có ô nhập mã thẻ → `khong_co_o_nhap_ma_the` FAIL.
  - nút vẫn mang nhãn "Xoá" → `nhan_noi_dung_tat_khong_phai_xoa` FAIL.
  - viết cứng option → `o_chon_lay_tu_may_chu` FAIL.
  - chỉ sửa DOM không gọi API → `sua_tu_giao_dien_ghi_duoc` FAIL.
  - hộp xác nhận chung chung → `xac_nhan_co_hien_ma_the` FAIL.
  - tắt trước rồi mới hỏi → `huy_thi_khong_doi` FAIL.
  - nuốt lỗi từ chối → `tu_choi_hien_ly_do` FAIL.

- **Artifacts:** `specs/sua-xoa-the/artifacts/trang-the.png` và `trang-the.json`;
  ghi đè mỗi lần chạy. Dọn ở `finally`; dọn thất bại là FAIL nhìn thấy được kèm
  câu lệnh sửa tay.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\sua-xoa-the\verify-trang-the.mjs"`
Exit: 0
Base: 8ff7cd30588c81b15f02485fb1bb55a74fcc27ae
Head: 5051ac02b2e94ae7ceff468fa5311712b71218fc

```text
  PASS  deploy_khop_source  | 0986a5c144d3
  PASS  khong_co_o_nhap_ma_the  | khoi modal 4597 ky tu, 8 o nhap, khong o nao cho ma the
  PASS  nhan_noi_dung_tat_khong_phai_xoa  | vung o thao tac 830 ky tu: co "Ngung dung"/"Dung lai"=true, co chu "Xoa"=false
  PASS  o_chon_lay_tu_may_chu  | loai_khach 3/3, hang_tai 3/3, ma the trong modal="CC155708"
  PASS  sua_tu_giao_dien_ghi_duoc  | CSDL: bien so="99Z-12345", khach="Khach Da Sua", loai_khach_id=1, hang_tai_id=0; bang co hien bien so moi: true
  PASS  xac_nhan_co_hien_ma_the  | hop thoai: "Ngung dung the CC155708 (so the TUH38OUL-A, bien so 99Z-12345)?"
  PASS  huy_thi_khong_doi  | sau khi huy hop thoai, is_active=1 (1 = chua doi, dung)
  PASS  tu_choi_hien_ly_do  | bao: "The nay dang gan voi mot xe trong o do. Tat the thi tai xe quet o cong se bi tu "; is_active=1
  PASS  don_sach_du_lieu_thu  | the 613->613, o do 755->755

9/9 PASS
EXIT=0
```

### Vòng rà soát trả về FAIL, và phát hiện đầu tiên đúng chỗ tôi chủ quan

Bản đầu của script gọi thẳng `window.moSua()`, `window.luuSua()`,
`window.doiTrangThai()` — **không một `click` nào**. Nghĩa là `9/9 PASS` lúc đó
chứng minh *ba hàm JavaScript chạy đúng*, không chứng minh *người vận hành bấm
được*. Nếu `card_id` là `undefined`, hoặc `<th>` mới bị quên, hoặc `</td>` đóng
sai, cả ba trường hợp đều PASS.

Đã đổi sang bấm nút thật bằng nội dung nhãn trong `#cardRows`, và bấm `#edLuu`
thay vì gọi hàm lưu.

| Phát hiện | Sửa |
|---|---|
| Không probe nào bấm nút thật | Bấm qua DOM; không tìm thấy nút là ném lỗi |
| Một thẻ `div` có **hai** thuộc tính `style` — trình duyệt vứt cái thứ hai nên dải mã thẻ mất nền và viền | Gộp thành một thuộc tính |
| `hover:bg-amber-950` và `hover:bg-green-950/60` **không có** trong `scada.css` (bản Tailwind trích xuất) nên nút không phản hồi khi rê chuột | Đổi sang `hover:bg-slate-700` — đã kiểm là có thật |
| `veOChon` với `chon=null` để trình duyệt tự chọn option đầu → ghi thầm một id người dùng không chọn | Chèn option rỗng và chọn nó; máy chủ trả 400 thay vì ghi thầm |
| Probe cắt khối modal bằng mốc chứa `\n\n` — hỏng khi file lưu bằng CRLF, và cửa sổ dự phòng 6000 ký tự làm nhánh báo lỗi không bao giờ chạy được | Mốc không chứa xuống dòng; không tìm thấy là FAIL ngay |
| Probe nhãn: khẳng định dương quét **cả file**, khẳng định âm quét cửa sổ theo offset cố định, kèm một biến chết | Cắt vùng ô thao tác bằng hai mốc có thật, chạy cả hai khẳng định trong đúng vùng đó |
| AC-11 đòi "bảng hiện giá trị mới **và** CSDL khớp" nhưng probe chỉ đọc CSDL | Đọc cả hai, và đọc thêm `customer_type_id`/`weight_class_id` |

### Sửa sau khi bàn giao: dropdown không đọc được giá trị

Người dùng báo ô chọn "Hạng tải" trong modal sửa không nhìn ra giá trị. Đo bằng
`getComputedStyle` cho thấy **hai** lỗi chồng nhau:

```text
truoc:  modal_select { color: rgb(226,232,240), bg: rgba(0,0,0,0) }
        modal_option { color: rgb(226,232,240), bg: rgba(0,0,0,0) }
sau:    modal_select { color: rgb(226,232,240), bg: rgb(15,23,42) }
        modal_option { color: rgb(226,232,240), bg: rgb(15,23,42) }
```

1. Lớp `bg-slate-900` **không có** trong `scada.css` (bản Tailwind trích xuất)
   nên nền ô chọn ra trong suốt — trong khi `bg-slate-900/50` mà bộ lọc sẵn có
   đang dùng thì có.
2. Thẻ `<option>` không có nền riêng, nên popup của Chrome trên Windows dùng nền
   trắng hệ thống trong khi chữ thừa kế `text-slate-200` màu sáng.

Sửa bằng một khối `<style>` đặt nền và màu chữ **tường minh** cho
`#editModal select/input/option`, không phụ thuộc lớp Tailwind nào còn hay mất.

## Hạn chế còn lại

1. **Chỉ đo trên một thẻ do script tự tạo.** 613 thẻ thật không bị đụng — đúng
   chủ ý, nhưng nghĩa là hành vi trên dữ liệu thật (hồ sơ DEC thiếu trường, mã
   chữ lạ) chưa được đo.
2. **Nhánh "không đối chiếu được mã chữ sang id" chưa ép được.** `GetAllRows`
   dùng `JOIN` chứ không `LEFT JOIN`, và `code` có ràng buộc `UNIQUE`, nên mọi
   thẻ hiện ra đều có đúng một hàng tra cứu khớp. Option rỗng là phòng thủ chiều
   sâu, **chưa từng chạy**.
3. **Chỉ đo trên Chrome headless.** Hộp `confirm`/`prompt` gốc hành xử khác nhau
   giữa các trình duyệt; chưa đo trên trình duyệt thật của phòng điều khiển.
4. **Tên người thao tác lấy bằng `prompt`** cho đường tắt/bật, và bằng ô nhập
   trong modal cho đường sửa. Hai cách khác nhau, và cả hai đều là tên tự khai.
5. **Bảng vẫn giới hạn 500 dòng hiển thị** (nợ có sẵn). Với 613 thẻ, một thẻ nằm
   ngoài 500 dòng đầu chỉ tìm được qua ô tìm kiếm.
6. **Hai ô lọc ở đầu trang (`filterLabel`, `filterWeight`) có cùng lỗi hiển
   thị** và **chưa sửa** — đo được `loc_option` vẫn là nền trong suốt. Đó là nợ
   có sẵn từ trước packet này; khối `<style>` mới chỉ phủ `#editModal`.
7. **Không có bộ lọc "chỉ xem thẻ đã tắt".** Thẻ tắt phân biệt được bằng nhãn
   trạng thái và nút "Dùng lại", nhưng muốn tìm nhanh thì vẫn phải gõ tay.
