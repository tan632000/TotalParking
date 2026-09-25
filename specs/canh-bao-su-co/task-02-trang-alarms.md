# Task 02 — Trang Alarms đọc dữ liệu thật, tự làm mới

Status: done

## Outcome

Trang `Alarms` hiển thị cảnh báo từ bảng thật, tự làm mới, bộ lọc vẫn chạy, và
không còn dòng hay số liệu nào viết cứng trong HTML.

## Scope

- **In:** thay thân bảng bằng dữ liệu từ `GET /Alarm/List`; thay 4 thẻ số liệu
  cứng bằng số tổng hợp từ cùng nguồn; nút xác nhận gọi `POST /Alarm/Ack` kèm ô
  nhập tên; tự làm mới định kỳ kèm dấu hiệu thời điểm cập nhật; phân biệt trạng
  thái rỗng với trạng thái lỗi.
- **Out:** không đổi bố cục, không đổi luật của bộ lọc, không đụng trang khác.

### Bốn thẻ số liệu cứng cũng phải đi

`Alarms.cshtml:19-40` có bốn thẻ viết cứng: "4 hoạt động", "2 hoạt động",
"1 cảnh báo", "3 sự cố". Nếu chỉ thay thân bảng mà để chúng lại, trang sẽ hiện
bảng trống bên dưới dòng chữ "4 hoạt động — Yêu cầu can thiệp kỹ thuật". Người
vận hành tin con số to hơn. Đó đúng là hạng lỗi mà CP-02 muốn diệt.

### Bộ lọc phải còn chạy sau khi đổi nguồn

`filterAlarms()` (`Alarms.cshtml:236-254`) duyệt `.alarm-row` và so `data-type`
với `data-severity`. Dòng render từ API **phải mang đúng hai thuộc tính đó** với
từ vựng mà task 01 đã chốt. Nếu thiếu, người vận hành chọn "Nghiêm trọng" sẽ thấy
bảng trống giữa lúc có sự cố thật — và không phép kiểm nào về số dòng bắt được.

### Không tự làm mới thì CP-03 mất tác dụng

Trang tự nhận là *"Giám sát toàn bộ sự cố thời gian thực"* (`Alarms.cshtml:11`)
nhưng `grep setInterval` không ra kết quả; làm mới duy nhất là nút
`window.location.reload()` (`:13`).

Màn hình tường phòng điều khiển mở từ 22:00; PLC rớt lúc 03:00; task 03 ghi cảnh
báo đúng thiết kế; màn hình vẫn hiện nội dung của 22:00 cho tới khi có người bấm.
Toàn bộ task 03 vô nghĩa ở đúng bề mặt mà nó phục vụ.

### Trạng thái rỗng phải khác trạng thái lỗi

Bảng rỗng vì **không có cảnh báo nào** là tin tốt. Bảng rỗng vì **không gọi được
API** là sự cố. Hai tình huống cho cùng một màn hình trắng nếu không phân biệt,
và người vận hành sẽ tưởng hệ thống bình thường trong khi nó đang mù.

## Coverage

- CP-02

## Ownership

- Modify: `TotalParking/Views/Home/Alarms.cshtml`
- Create: `specs/canh-bao-su-co/verify-trang-alarms.mjs`
- Read: `TotalParking/Controllers/AlarmController.cs`, `TotalParking/Views/Home/Index.cshtml`

## Acceptance

- AC-04: số dòng trên trang bằng số API trả về; không còn dòng nào **lẫn số liệu
  nào** viết cứng.
- AC-05: chọn một giá trị bộ lọc bất kỳ, số dòng hiện ra khớp số cảnh báo cùng
  loại từ API.
- AC-06: cảnh báo chèn sau khi trang đã tải tự hiện trong vòng một chu kỳ, và
  trang có dấu hiệu thời điểm cập nhật cuối.
- AC-12: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-01-bang-va-api.md

## Verification Plan

- **Build và deploy trước khi đo.** Razor biên dịch lúc chạy nên `.cshtml` phải
  được chép sang bản deploy **riêng**, ngoài `bin\*.dll`. Chép `.cshtml` không
  làm khởi động lại ứng dụng; chép `bin\` thì có.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-trang-alarms.mjs"`

- **Named probe:** tám phép kiểm có tên — `deploy_khop_source`,
  `khong_con_du_lieu_cung`, `so_dong_khop_api`, `bo_loc_van_chay`,
  `tu_lam_moi_khi_co_canh_bao_moi`, `trang_rong_khac_trang_loi`,
  `nut_xac_nhan_ghi_duoc`, `don_sach_du_lieu_thu`.

- **Reachability:** site chạy ở `http://localhost:8080`; Chrome tại
  `C:/Program Files/Google/Chrome/Application/chrome.exe`; đo bằng puppeteer
  theo khuôn `specs/monitoring-real-data/verify-dashboard.mjs`.

- **Oracle:**
  - `khong_con_du_lieu_cung`: **không** so chuỗi `<tr class="alarm-row">` —
    markup thật có thêm nhiều class nên chuỗi đó không tồn tại và phép kiểm sẽ
    PASS giả. Và không cấm chuỗi `alarm-row`, vì template JS **bắt buộc** phải
    sinh lại class đó cho bộ lọc chạy. Thay vào đó: đếm số thẻ `<tr` tĩnh nằm
    trong `<tbody id="alarmsTableBody">` của Razor (phải bằng 0), và khẳng định
    các chuỗi dữ liệu đặc trưng như `E-0408`, `Safety-Relay`, `4 hoạt động`
    không còn trong file.
  - `so_dong_khop_api`: chèn ba cảnh báo thử, tải trang, đếm dòng và so với
    `GET /Alarm/List`.
  - `bo_loc_van_chay`: với mỗi giá trị `data-severity`, bấm bộ lọc rồi đếm dòng
    hiện ra, so với số cảnh báo cùng mức từ API.
  - `tu_lam_moi_khi_co_canh_bao_moi`: tải trang, **rồi mới** chèn một cảnh báo,
    chờ quá một chu kỳ làm mới, khẳng định nó xuất hiện mà không cần tải lại.
  - `trang_rong_khac_trang_loi`: chặn lời gọi API bằng
    `page.setRequestInterception` rồi tải trang; phải hiện thông báo lỗi, **không**
    phải bảng rỗng im lặng. Kỹ thuật này đã chứng minh chạy được ở
    `specs/monitoring-real-data/verify-dashboard.mjs:154-171`. Nó chỉ ép được
    nhánh lỗi nếu trang gọi API **từ trình duyệt** — task này chốt render phía
    client, không truyền model từ `HomeController.Alarms()`.
  - `nut_xac_nhan_ghi_duoc`: nhập tên, bấm xác nhận, rồi đọc CSDL khẳng định
    `xac_nhan_luc` khác NULL.
  - `don_sach_du_lieu_thu`: xoá sạch cảnh báo thử; số dòng trở lại như trước.

- **Counterexample:**
  - nếu quên xoá 6 dòng hoặc 4 thẻ cứng thì `khong_con_du_lieu_cung` FAIL.
  - nếu dòng render thiếu `data-type`/`data-severity` thì `bo_loc_van_chay` FAIL
    trong khi `so_dong_khop_api` vẫn PASS — đó là lý do cần cả hai.
  - nếu trang không tự làm mới thì `tu_lam_moi_khi_co_canh_bao_moi` FAIL.
  - nếu trang nuốt lỗi API và hiện bảng rỗng thì `trang_rong_khac_trang_loi` FAIL.
  - nếu nút xác nhận chỉ đổi màu mà không ghi CSDL thì `nut_xac_nhan_ghi_duoc`
    FAIL — đúng lỗi mà nút `ESTOP` cũ đang mắc.

- **Artifacts:** `specs/canh-bao-su-co/artifacts/trang-alarms.png` và
  `trang-alarms.json`; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-trang-alarms.mjs"`
Exit: 0
Base: 383ad696b1bf127f30367c194abd9d20c337eb0c
Head: cfec63a0d731e4d448de60e199a77163753c2ecc

```text
  PASS  deploy_khop_source  | 88ece4d6c57b
  PASS  khong_con_du_lieu_cung  | <tr tinh trong tbody: 0, khong con chuoi cung
  PASS  so_dong_khop_api  | trang 3 vs api 3
  PASS  bo_loc_van_chay  | da thu 3 muc: critical, high, low
  PASS  tu_lam_moi_khi_co_canh_bao_moi  | da tu hien, dau hieu cap nhat: "Cập nhật 00:00:14"
  PASS  nut_xac_nhan_ghi_duoc  | xac_nhan_boi="Nguoi Kiem Thu", xac_nhan_luc=co
  PASS  trang_rong_khac_trang_loi  | than bang: "Không đọc được danh sách cảnh báo: HTTP 503"
  PASS  don_sach_du_lieu_thu  | so dong 0 -> 0

8/8 PASS
EXIT=0
```

### Hai phép kiểm bắt hai lỗi khác nhau

`so_dong_khop_api` và `bo_loc_van_chay` nhìn giống nhau nhưng bắt hai thứ khác
hẳn. Nếu dòng render thiếu `data-severity`, số dòng vẫn đúng — chỉ tới khi người
vận hành **chọn một mức lọc** thì bảng mới trống. Phép kiểm thứ hai chọn từng
mức và đối chiếu với API, nên bắt được.

### Nhánh lỗi được ép thật

`trang_rong_khac_trang_loi` chặn `/Alarm/List` bằng `setRequestInterception` và
trả `503`, rồi khẳng định thân bảng hiện *"Không đọc được danh sách cảnh báo:
HTTP 503"* chứ không phải bảng trống. Đây là khác biệt giữa **"không có sự cố
nào"** và **"không đọc được sự cố"** — hai tình huống trước đây cho cùng một màn
hình trắng.

### Tự làm mới đã chạy

Cảnh báo được chèn **sau** khi trang tải xong và tự xuất hiện trong vòng một chu
kỳ 15 giây, kèm dấu hiệu `"Cập nhật 00:00:14"`. Không có nó thì mọi cảnh báo do
task 03 sinh ra ban đêm sẽ không ai thấy tới sáng.

### Hạn chế còn lại

1. **Chưa có cảnh báo thật nào để hiển thị.** Task 03 (sinh cảnh báo khi PLC mất
   kết nối) chưa làm, nên hiện trang luôn rỗng trên hệ thống thật. Phép kiểm
   dùng cảnh báo tự chèn rồi xoá.
2. **Nhịp làm mới 15 giây là con số tôi chọn**, chưa hiệu chỉnh theo nhu cầu vận
   hành. Mỗi lần làm mới là một lượt gọi API dựng lại toàn bộ thân bảng.
3. **Render lại toàn bộ bảng mỗi chu kỳ.** Với vài trăm dòng thì không sao; nếu
   sau này bỏ trần 200 dòng thì cách này phải xem lại.
4. **Tên người xác nhận lấy bằng `prompt`.** Đơn giản và đủ dùng, nhưng vẫn là
   tên tự khai — hệ thống chưa có đăng nhập.
