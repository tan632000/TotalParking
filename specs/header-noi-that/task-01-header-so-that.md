# Task 01 — Header đọc số thật từ endpoint tóm tắt

Status: done

## Outcome

Header của mọi trang SCADA hiện đúng số cảnh báo chưa xác nhận, đúng màu theo
mức cao nhất đang mở, đồng hồ chạy theo giờ máy, và không còn danh tính giả.
Máy chủ không trả lời được trông khác hẳn trạng thái sạch.

## Scope

- **In:** `GET /Alarm/Summary` trả vài con số cố định; header đọc endpoint đó
  theo chu kỳ và dừng khi tab ẩn; chip đổi màu theo mức cao nhất; đồng hồ chạy;
  bỏ `supervisor01` khỏi header.
- **Out:** xem `plan.md` mục *Out of scope*. Tóm tắt phần dễ vi phạm nhất: không
  đụng `:291` (nhãn phiên bản thanh bên), không đụng `:305`
  (`lucide.createIcons()`), không đụng `Remote.cshtml`.

### Bốn chuỗi viết cứng cần diệt — số dòng đã kiểm lại

| Dòng | Chuỗi | Thực tế |
|---|---|---|
| `:102-104` | chip đỏ `Lỗi` | luôn đỏ, kể cả khi hệ thống bình thường |
| `:105-106` | `4 Alarm` kèm `animate-pulse` ở `:105` | đo được lúc lập kế hoạch: 8 cảnh báo đang mở, 16 chưa xác nhận |
| `:111` | `16:02:42` | đứng im |
| `:112` | `08/06/2026` | đứng im; hôm nay là 25/09/2026 |
| `:116` | `supervisor01` | hệ thống chưa có đăng nhập |

**Không sửa theo số dòng mà không đọc dòng đó trước.** `:110` và `:115` là hai
thẻ `<div>` **mở**, không phải nội dung. Xoá nhầm một trong hai sẽ để lại
`</div>` không khớp cặp; Razor vẫn biên dịch và trình duyệt tự vá cây DOM, nên
lỗi không lộ ra ở đâu cả — chỉ bố cục cụm giờ/người dùng/nút vỡ trên **cả 22
trang**, và không phép kiểm nào trong bộ này đọc bố cục.

### `08/06/2026` xuất hiện HAI lần trong file

- `:112` — trong header, **đúng mục tiêu**.
- `:291` — `v2.4.1 – 08/06/2026`, nằm trong `<aside id="scada-aside">`
  (`:129-293`), là nhãn phiên bản thanh bên, **ngoài phạm vi**.

Vì vậy phép kiểm chuỗi phải đọc **chỉ khối `<header>` `:77-125`**, không đọc cả
file. Đọc cả file thì task tự khoá: làm đúng vẫn FAIL, mà lối thoát duy nhất là
sửa `:291` — vi phạm Out of scope và đổi nhãn phiên bản mà không ai quyết định.

### Layout có HAI khối script, không phải một

- `:304-306` — `lucide.createIcons();`, **gọi trần, không có guard**.
- `:307-337` — nút thu gọn thanh bên; IIFE bắt đầu ở `:314`.

Hai ràng buộc rút ra:

1. **Script header phải là một khối `<script>` mới, tách rời.** Viết thêm vào
   khối `:304-306` thì khi `lucide` chưa nạp được, `ReferenceError` ở dòng đầu
   nuốt luôn phần còn lại — header đứng im vĩnh viễn ở trạng thái ban đầu, trong
   khi chip cũ đã bị xoá. Trên máy dev có Internet thì mọi phép kiểm vẫn PASS.
2. **Không dùng `<i data-lucide>` trong header.** `lucide.createIcons()` chỉ
   chạy một lần ở `:305`; biểu tượng thêm sau đó sẽ không bao giờ được dựng.

### Đừng ghi đè `innerHTML` của chip

Hai chip chứa `<svg>` nội tuyến (`:103`, `:106`). Viết đè `innerHTML` của cả chip
sẽ xoá mất SVG và để lại chip trống — lucide không dựng lại vì đây không phải
`<i data-lucide>`.

Cách làm: đặt `id` cho **phần tử bọc** và cho **`<span>` chữ**, rồi chỉ đổi
`textContent` của span và `className` của phần tử bọc. Không chạm vào SVG.

Lớp CSS cần dùng đã có sẵn trong `TotalParking/Content/scada.css`
(`bg-green-950/60`, `border-green-700/50`, `text-green-300`, `bg-slate-800/50`,
`text-slate-400`), nên không cần thêm CSS — `_ScadaLayout.cshtml:50-57` cảnh báo
rằng class Tailwind viết mới sẽ không có tác dụng.

### Mất kết nối là yêu cầu an toàn, và nó có BA dạng chứ không một

Đây là lỗi nguy hiểm nhất có thể mắc trong task này: khi không đọc được máy chủ
mà header vẫn hiện **"Bình thường"**, người vận hành tin hệ thống đang sạch
trong khi nó đang mù. Giữ số cũ cũng sai theo cách tương tự.

Ba dạng hỏng phải cho ra **cùng một** trạng thái "không đọc được":

| Dạng | Vì sao dễ lọt |
|---|---|
| HTTP `503` | `r.json()` ném → rơi vào `catch` → thường đã đúng |
| `200` + thân `{}` | `.json()` **thành công**; `d.chua_xac_nhan \|\| 0` cho ra `0` → **chip xanh "Bình thường"** |
| `200` + thân HTML | `.json()` ném; đúng nếu `catch` bọc cả bước phân tích |

Ràng buộc: **thiếu trường bắt buộc phải bị coi là không đọc được.** Không dùng
`|| 0` để lấp chỗ trống — đó chính là đường dẫn tới chip xanh giả.

### Dừng poll khi tab ẩn, và guard chống chồng interval

22 trang có thể cùng mở trên màn hình tường và các máy trạm.
`Index.cshtml:1489-1490` là khuôn phải làm theo, và điều kiện then chốt nằm ở
chính hai cái guard:

```js
function batDau() { if (!timer) { tick(); timer = setInterval(tick, POLL_MS); } }
function dungLai() { if (timer) { clearInterval(timer); timer = null; } }
```

Thiếu `if (!timer)` / `timer = null` thì mỗi lần trình duyệt bắn
`visibilitychange` với `hidden === false` mà không có lần `hidden === true`
tương ứng (khôi phục bfcache, đổi màn hình, khoá/mở máy) sẽ sinh thêm một
interval và **mất handle cũ**. Sau vài ngày một tab có hàng chục poller 15 giây
chạy song song, và header — thứ được biện minh là "nhẹ hơn `/Alarm/List`" —
thành nguồn tải lớn hơn chính cái nó thay thế. Không có gì trên giao diện cho
thấy điều đó.

Nhịp **15 giây**, khớp `Alarms.cshtml:135`. Trên trang Alarms và
OperationControl sẽ có hai bộ đếm chạy song song vào **hai endpoint khác nhau**;
đó là cố ý, vì header phải sống độc lập với trang.

### Vì sao endpoint riêng chứ không dùng `/Alarm/List`

Lý do và số đo nằm ở `plan.md`. Điều kiện then chốt cho task này: **byte thân
phản hồi** của `/Alarm/Summary` không được tăng theo số cảnh báo. Trả kèm một
mảng mẫu "vài dòng gần nhất" là vi phạm điều đó và sẽ bị AC-07 bắt.

## Coverage

- CP-01, CP-02, CP-03, CP-04, CP-05

## Ownership

- Modify: `TotalParking/Controllers/AlarmController.cs`
- Modify: `TotalParking/Views/Shared/_ScadaLayout.cshtml`
- Create: `specs/header-noi-that/verify-header.mjs`
- Modify: `TotalParking/Services/CanhBaoRepository.cs`
- Read: `TotalParking/Views/Home/Index.cshtml`, `TotalParking/Views/Home/Alarms.cshtml`, `TotalParking/Content/scada.css`

`CanhBaoRepository.cs` chuyen tu *Read* sang *Modify* sau vong ra soat: tom
tat bang cach goi `Doc()` roi dem trong C# se keo toan bo canh bao ve bo nho
moi luot — dung ganh nang ma endpoint nay sinh ra de tranh — con dat SQL
thang trong controller thi pha khuon kien truc dang co. Thay doi la bo sung
thuan, khong sua chu ky nao dang co.

`TotalParking.csproj` **không** cần sửa: không thêm file `.cs` hay `.cshtml` nào
mới, và `_ScadaLayout.cshtml` đã có ở `TotalParking.csproj:273`.

## Acceptance

- AC-01: số trên header khớp số cảnh báo chưa xác nhận, đo trên **hai trang khác
  nhau**.
- AC-02: không còn cảnh báo chưa xác nhận thì chip xanh "Bình thường".
- AC-03: có `critical` đang mở thì chip đỏ; chỉ còn mức thấp hơn thì không đỏ.
- AC-04: cả ba dạng hỏng ở bảng trên đều cho ra dấu mất kết nối — không giữ số
  cũ, không hiện "Bình thường".
- AC-05: đồng hồ đổi giá trị giữa hai lần đọc.
- AC-06: khối `<header>` `:77-125` không còn `4 Alarm`, `supervisor01`,
  `16:02:42`, `08/06/2026`.
- AC-07: byte thân `/Alarm/Summary` không tăng quá 50 khi số cảnh báo đi từ 0
  lên ≥40.
- AC-08: không lỗi JavaScript từ script header trên trang không liên quan;
  `lucide is not defined` ghi nhận riêng.
- AC-09: dấu vân tay DLL và `_ScadaLayout.cshtml` trùng **bản deploy**.
- AC-10: ẩn/hiện tab nhiều lần không làm số lượt gọi vượt số chu kỳ đã trôi qua.

## Dependencies

- none

## Verification Plan

- **Build và deploy trước khi đo.** Razor biên dịch lúc chạy nên `.cshtml` phải
  chép sang bản deploy **riêng**, ngoài `bin\*.dll`.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\header-noi-that\verify-header.mjs"`

- **Named probe:** mười phép kiểm có tên — `deploy_khop_source`,
  `khong_con_chuoi_cung`, `so_khop_tren_hai_trang`, `chip_do_khi_co_critical`,
  `chip_xanh_khi_sach`, `mat_ket_noi_khac_trang_thai_sach`, `dong_ho_chay`,
  `kich_thuoc_khong_tang_theo_so_canh_bao`, `khong_loi_js_tren_trang_khac`,
  `poll_khong_chong_nhau`.

- **Reachability:** site chạy ở `http://localhost:8080`; Chrome tại
  `C:/Program Files/Google/Chrome/Application/chrome.exe`; CSDL truy vấn được
  bằng `mysql.exe`; **bản deploy ở `C:\Users\Admin\Documents\Web\totalParking`**;
  khuôn đo theo `specs/canh-bao-su-co/verify-trang-alarms.mjs`.

- **Oracle:**
  - `deploy_khop_source`: so SHA-256 của **hai cặp đường dẫn tuyệt đối cố định**
    — `TotalParking\Views\Shared\_ScadaLayout.cshtml` ↔
    `<deploy>\Views\Shared\_ScadaLayout.cshtml`, và `TotalParking\bin\TotalParking.dll`
    ↔ `<deploy>\bin\TotalParking.dll`. **Không dùng glob**: tồn tại bản sao cũ
    `TotalParking/obj/Release/Package/PackageTmp/Views/Shared/_ScadaLayout.cshtml`
    chứa `4 Alarm`; so nhầm vào đó thì hai bản cùng cũ khớp nhau hoàn hảo và
    probe PASS giả, khiến mọi số đo sau đó đo trên mã cũ. Lệch thì in ra cả hai
    đường dẫn đã so rồi dừng.
  - `khong_con_chuoi_cung`: cắt lấy **chỉ khối `<header>`** của
    `_ScadaLayout.cshtml` (từ `<header` tới `</header>`) rồi khẳng định bốn chuỗi
    không còn trong đoạn đó. `08/06/2026` ở `:291` nằm ngoài đoạn cắt và **không
    được tính**.
  - `so_khop_tren_hai_trang`: chèn số cảnh báo thử đã biết, mở **`Home/Settings`**
    (không có lời gọi `fetch` nào) và **`Home/Alarms`**, đọc số trên header của
    cả hai và so với CSDL. Đo một trang chỉ chứng minh một trang, chưa chứng minh
    layout dùng chung.
  - `chip_do_khi_co_critical` và `chip_xanh_khi_sach`: ép **cả ba** trạng thái —
    có `critical`, chỉ còn `low`, và không còn dòng chưa xác nhận nào — rồi đọc
    **cả chữ lẫn lớp CSS** của phần tử bọc. Thiếu trạng thái giữa thì một cài đặt
    "hễ có cảnh báo là đỏ" vẫn PASS.
  - `mat_ket_noi_khac_trang_thai_sach`: dùng `page.setRequestInterception` ba
    lượt riêng cho `/Alarm/Summary` — `503`; `200` + `{}` (JSON hợp lệ, thiếu
    trường); `200` + `<html>Server Error</html>`. Với cả ba, khẳng định header
    **không** hiện "Bình thường" **và không** giữ số cũ. Kỹ thuật chặn đã chứng
    minh chạy được ở `specs/canh-bao-su-co/verify-trang-alarms.mjs:163-165`.
  - `dong_ho_chay`: đọc đồng hồ, chờ quá 1 giây, đọc lại; hai giá trị phải khác
    nhau. So chuỗi chứ không so định dạng.
  - `kich_thuoc_khong_tang_theo_so_canh_bao`: đo **byte thân phản hồi**
    (`(await res.text()).length` tính theo UTF-8, hoặc `res.buffer().length`),
    **không** đọc `Content-Length`. Khẳng định phép đo `> 0` trước khi so sánh —
    một phép đo rỗng phải FAIL rõ ràng chứ không PASS im lặng qua `0 - 0 <= 50`.
    Đo với 0 cảnh báo, rồi chèn **≥40** cảnh báo và đo lại; trần chênh lệch **50
    byte**. Phép kiểm này bắt đúng lỗi "tiện tay trả kèm danh sách" — lỗi mà mọi
    phép kiểm khác vẫn PASS.
  - `khong_loi_js_tren_trang_khac`: mở `Home/Settings` và `Home/Backup`, bắt
    `pageerror`, rồi **lọc theo nguồn**: lỗi khớp `/lucide is not defined/` là nợ
    có sẵn (`_ScadaLayout.cshtml:10,305` — CDN `unpkg` không ghim, gọi trần), ghi
    vào artifact và **không** tính FAIL; mọi lỗi khác FAIL. Probe cũng ghi
    `typeof lucide` để phân biệt hai nguyên nhân. Không lọc thì phép kiểm FAIL
    oan ở mạng OT cô lập và đẩy người thực thi vào việc sửa ngoài phạm vi.
  - `poll_khong_chong_nhau`: đếm request tới `/Alarm/Summary` bằng
    `page.on("request")`; bắn ẩn/hiện ít nhất **5 lần** rồi quan sát **45 giây**;
    số request phải **≤ 4**. Nếu không ép được `document.hidden` qua CDP
    (`Emulation.setPageVisibilityOverride` đã bị gỡ ở Chrome mới) thì dùng
    `page.evaluateOnNewDocument` định nghĩa lại `document.hidden` kèm
    `document.dispatchEvent(new Event("visibilitychange"))`, và **ghi rõ trong
    artifact đã dùng cách nào**.

- **Counterexample:**
  - phép kiểm chuỗi đọc cả file → `khong_con_chuoi_cung` FAIL vì `:291`, dù làm
    đúng.
  - so vân tay với bản `obj/` → `deploy_khop_source` PASS giả trên mã cũ.
  - `|| 0` khi thiếu trường → dạng `200` + `{}` cho chip xanh →
    `mat_ket_noi_khac_trang_thai_sach` FAIL.
  - thiếu guard `if (!timer)` → `poll_khong_chong_nhau` FAIL.
  - "hễ có cảnh báo là đỏ" → trạng thái "chỉ còn `low`" FAIL.
  - trả kèm mảng cảnh báo trong `/Alarm/Summary` →
    `kich_thuoc_khong_tang_theo_so_canh_bao` FAIL.
  - viết script header vào khối `:304-306` → không probe nào bắt trực tiếp; đây
    là lý do ràng buộc "khối `<script>` mới, tách rời" được ghi thành yêu cầu
    trong Scope chứ không chỉ là lời khuyên.
  - chỉ đo trên trang Alarms → `so_khop_tren_hai_trang` FAIL vì trang thứ hai
    không có số.

- **Artifacts:** `specs/header-noi-that/artifacts/header.png` và `header.json`
  (trạng thái từng bước, số byte đo được, `typeof lucide`, cách ép
  `document.hidden`, danh sách `pageerror` đã bỏ qua); ghi đè mỗi lần chạy. Dữ
  liệu cảnh báo thử mang tiền tố riêng và **phải được xoá ở `finally`**; khôi
  phục thất bại là FAIL nhìn thấy được kèm câu lệnh sửa tay.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\header-noi-that\verify-header.mjs"`
Exit: 0
Base: 55a100cb5029948b7ff8477ac53fee73f5a615c1
Head: b85da1f366e89a8cd9bc59f4b95845eba8394026

```text
  PASS  deploy_khop_source  | _ScadaLayout.cshtml=bafc1cce5a10, TotalParking.dll=ea9a66ac2c45
  PASS  khong_con_chuoi_cung  | khoi <header> 5784 ky tu, sach ca 4 chuoi
  PASS  so_khop_tren_hai_trang  | csdl 19 Alarm; Settings="19 Alarm", Alarms="19 Alarm"
  PASS  chip_do_khi_co_critical  | may chu bao muc_cao_nhat="critical"; chip="Su co", lop do: tinh_trang=true so=true
  PASS  chip_xanh_khi_sach  | sach: chu="Binh thuong" xanh=true; chi-con-low: chu="Canh bao" do=false (phai la false)
  PASS  mat_ket_noi_khac_trang_thai_sach  | ca 3 dang deu bao mat ket noi, khong dang nao noi "Binh thuong" hay giu so cu
  PASS  dong_ho_chay  | "10:53:43" -> "10:53:45", ngay="25/09/2026"
  PASS  kich_thuoc_khong_tang_theo_so_canh_bao  | 20 -> 60 canh bao: 97 -> 98 byte (chenh 1, tran 50)
  PASS  khong_loi_js_tren_trang_khac  | 2 trang sach; typeof lucide="object", da bo qua 0 loi lucide co san
  PASS  poll_khong_chong_nhau  | sau 5 lan an/hien, 45s co 3 luot goi /Alarm/Summary (tran 4)
  PASS  don_sach_du_lieu_thu  | so dong 16 -> 16

11/11 PASS
EXIT=0
```

### Kiểm chứng ngược: probe chống chồng poll thật sự bắt được lỗi

Một phép kiểm chưa bao giờ đỏ thì chưa chứng minh được gì. Vòng rà soát chỉ ra
phiên bản đầu của `poll_khong_chong_nhau` **không** tái hiện được lỗi: nó chỉ bắn
cặp ẩn/hiện, mà `dungLai()` xoá timer trước mỗi lần hiện nên ngay cả bản thiếu
guard cũng chỉ còn một interval.

Sau khi sửa (bắn ba lần *hiện* liên tiếp không xen *ẩn*), đo trên bản deploy:

```text
A. ban DUNG (co guard)      ->  3 luot goi trong 45s (tran 4) -> PASS
B. go guard o ban deploy    -> 12 luot goi trong 45s (tran 4) -> FAIL
C. khoi phuc tu nguon       -> fe3b206649ef76b1 == fe3b206649ef76b1
```

### Chạy lại lần cuối trên đúng bản đang phục vụ

Giữa lúc chốt, DLL được build lại (dấu thời gian 10:51:26) ngoài phiên làm việc
này, nên vân tay trong lần chạy trước không còn khớp cây mã. Ngoài ra chú thích
JavaScript của chính khối script mới có chứa nguyên văn bốn chuỗi cũ — không
phải dữ liệu được render, nhưng vẫn gửi chúng xuống cả 22 trang và làm nhiễu mọi
lần grep sau này, nên đã viết lại.

Số liệu trong khối trên là của lần chạy **sau** hai thay đổi đó, với vân tay
`_ScadaLayout.cshtml=bafc1cce5a10` và `TotalParking.dll=ea9a66ac2c45` — đúng bản
đang phục vụ tại `http://localhost:8080`.

Chuỗi `08/06/2026` vẫn xuất hiện một lần trong HTML trả về: đó là nhãn phiên bản
`v2.4.1 – 08/06/2026` của thanh bên, nằm ngoài phạm vi theo `plan.md`.

### Số đo chính

`/Alarm/Summary` là **97 byte**; `/Alarm/List` cùng lúc là **8.022 byte** — nhẹ
hơn 82 lần. Thêm 40 cảnh báo làm nó tăng đúng **1 byte** (19 → 59 dòng, 97 → 98).

### Lệch Ownership đã ghi nhận

`CanhBaoRepository.cs` được chuyển từ *Read* sang *Modify* giữa chừng. Lý do đầy
đủ nằm ở mục Ownership. Thay đổi là bổ sung thuần (`TomTatCanhBao`, `TomTat()`,
`TenMuc()`), không sửa chữ ký nào đang có, và người gọi duy nhất là
`AlarmController.Summary()`.

## Hạn chế còn lại

1. **`chip_xanh_khi_sach` là phép kiểm phía trình duyệt, không phải end-to-end.**
   Hệ thống đang có cảnh báo thật đang mở; ép trạng thái "sạch" thật đòi phải xoá
   chúng. Probe dùng thân phản hồi viết tay đúng khuôn máy chủ, nên nó chứng minh
   **ánh xạ số liệu → màu chip**, không chứng minh máy chủ có bao giờ phát
   `dang_mo: 0` hay không.
2. **Xếp hạng `high`/`medium`/`low` phía máy chủ chưa được chứng minh.** Mọi lần
   đo đều có `critical` đang mở, nên chỉ nhánh `critical` của `FIELD/MIN` và
   `TenMuc()` được xác nhận đầu-cuối. Ba nhánh còn lại mới chỉ đúng theo suy luận
   từ mã.
3. **AC-07 đo từ mức nền hiện tại lên +40, không phải từ 0.** Bảng không thể rỗng
   vì có cảnh báo thật. Tính chất được đo (kích thước độc lập với số dòng) không
   phụ thuộc điểm xuất phát, nhưng con số "0" trong tiêu chí thì chưa chạm tới.
4. **Chip xanh đòi HAI điều kiện**: `dang_mo = 0` **và** `chua_xac_nhan = 0`.
   AC-02 (xanh khi hết chưa-xác-nhận) và AC-03 (đỏ khi còn critical mở) mâu thuẫn
   nhau ở trường hợp "đã xác nhận hết nhưng thiết bị còn hỏng"; tôi chọn hướng bảo
   thủ — **không bao giờ xanh khi còn thứ gì đang mở**.
5. **Trường hợp `dang_mo > 0` mà `chua_xac_nhan = 0` cho ra chip đỏ mang chữ
   "0 Alarm".** Đúng ý đồ nhưng đọc lên mâu thuẫn; chưa xử lý.
6. **`lucide` vẫn nạp từ CDN `unpkg` không ghim** (ngoài phạm vi). Phép kiểm chạy
   trên máy có Internet (`typeof lucide = "object"`, 0 lỗi bị bỏ qua), nên **hành
   vi ở mạng OT cô lập chưa được đo**. Bộ lọc `pageerror` đã sẵn sàng cho tình
   huống đó nhưng chưa từng kích hoạt.
7. **`Remote.cshtml:106,186` vẫn trưng `supervisor01`** như tài khoản đang trực
   tuyến (ngoài phạm vi, đã ghi ở `plan.md`). Danh tính giả mới biến khỏi header,
   chưa biến khỏi hệ thống.
