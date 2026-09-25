# Task 01 — Dashboard đọc cảnh báo thật, gỡ sáu nguồn bịa

Status: done

## Outcome

Bảng cảnh báo, ba bộ đếm và biểu ngữ trên Dashboard hiện đúng cảnh báo **chưa
xác nhận** trong bảng `canh_bao`; không còn nguồn nội dung bịa nào; không đọc
được máy chủ trông khác hẳn trạng thái sạch; nút xác nhận ghi được xuống CSDL;
rác giả trong `localStorage` được dọn mà bản ghi của trang khác vẫn còn.

## Scope

- **In:** gỡ sáu nguồn bịa (bảng dưới); `renderAlarmsTable`, `updateCounters`,
  `updateGlobalBanner` lấy dữ liệu từ `GET /Alarm/List`; tự làm mới **15 giây**;
  `ackAlarm` gọi `POST /Alarm/Ack`; dọn rác `localStorage` một lần; công thức cột
  "Nguồn phát"; sắp xếp theo `xay_ra_luc`.
- **Out:** xem `plan.md`. Phần dễ vi phạm nhất: **không đụng
  `OperationControl.cshtml`**, **không `removeItem` khoá `activeAlarms`**.

### Sáu nguồn phải gỡ

| Dòng | Nội dung | Có chứa chuỗi cấm không |
|---|---|---|
| `:987-1009` | Ba kho câu `*Pool` | có |
| `:1012-1020` | `getRandomItem`, `formatCurrentTime` | có (`getRandomItem`) |
| `:1023-1035` | `initAlarms()` đọc `localStorage` + **`return` sớm ở `:1031`** | **KHÔNG** |
| `:1060-1082` | Cảnh báo `MNT-DUE-100` suy từ `palletCycleData` | có (`MNT-DUE-`) |
| `:1085-1113` | Ba cảnh báo bịa khi `activeAlarms.length < 3` | có |
| `:1228-1295`, `:1298-1309` | `addRealtimeAlarm()` và `setInterval` 15 giây | có |

**Cột cuối là điểm mấu chốt.** Khối `:1023-1035` không chứa chuỗi cấm nào, nên
để lại nó thì mọi phép kiểm tĩnh vẫn xanh — trong khi `return` ở `:1031` làm
`fetch('/Alarm/List')` **không bao giờ chạy** trên máy đã có sẵn
`localStorage.activeAlarms`. Màn hình tường chắc chắn có, do chính bộ sinh 15
giây ghi suốt nhiều ngày.

Vì vậy phép kiểm phải **gieo rác vào `localStorage` trước khi tải trang**, không
đo trên hồ sơ trình duyệt sạch.

### `saveActiveAlarms` KHÔNG có trong file này

Đo được **0 lần** trong `Index.cshtml`. Hàm cùng tên ở
`OperationControl.cshtml:618` có bốn nơi gọi, trong đó `:1053` là đường ghi bản
ghi E-Stop mà `Safety.cshtml:256` dò tìm.

> **Không grep rồi xoá.** File đó nằm ngoài phạm vi. Trong `Index.cshtml` chỉ có
> ba câu `localStorage.setItem('activeAlarms', …)` trần, ở `:1115`, `:1219`,
> `:1276` — bỏ cả ba cùng với hàm chứa chúng.

### Một tập duy nhất: chưa xác nhận

Lọc `chua_xac_nhan === true` **một lần** ngay sau khi nhận phản hồi, rồi bảng,
ba bộ đếm và biểu ngữ dùng chung mảng đã lọc đó. Lý do ở `plan.md`.

Giá trị `nguon` lạ (không thuộc `operation`/`hardware`/`maintenance`) **không
được lặng lẽ rơi vào nhóm 2** — đếm riêng, không tính vào nhóm nào, để một giá
trị `ENUM` mới thêm sau này lộ ra thay vì bị giấu.

### Công thức cột "Nguồn phát"

Bảng có 6 cột (`:949-954`): Thời gian, Mã Lỗi, Phân loại, **Nguồn phát**, Nội
dung, Thao tác.

API không có trường ghép sẵn. Dựng từ ba trường, **bỏ qua phần thiếu**, nối bằng
` · `:

- `Z{zone_id}` — chỉ khi `zone_id` khác null
- `Block {block_no}` — **chỉ khi `block_no` khác null**
- `{thiet_bi}` — chỉ khi khác rỗng

`AlarmController.cs:165` đặt `BlockNo = null` cho `SU-CO-KHAN`, nên ghép thẳng
sẽ cho ra `Block null` trên màn hình vận hành. Nếu cả ba thiếu thì hiện `—`.

### Sắp xếp theo `xay_ra_luc`, không theo `HH:mm:ss`

Mã hiện tại sắp xếp bằng `b.time.localeCompare(a.time)` trên chuỗi `HH:mm:ss`
(`:1138`, `:1200`). Qua nửa đêm thì cảnh báo `23:58` hôm qua xếp trên `00:05` hôm
nay, và biểu ngữ quảng cáo một sự cố **cũ 24 giờ** là "mới nhất".

`xay_ra_luc` là chuỗi `yyyy-MM-dd HH:mm:ss` (`AlarmController.cs:50`) nên so sánh
chuỗi trực tiếp là an toàn.

### Trạng thái không đọc được phải có câu RIÊNG

`:1126-1135` hiện *"Không có cảnh báo hoạt động. Hệ thống vận hành an toàn."*
khi rỗng. Câu đó **chỉ được hiện khi máy chủ trả lời và trả về rỗng**.

Bốn dạng hỏng phải cho ra **cùng một câu báo lỗi cố định** — ví dụ
`Không đọc được cảnh báo từ máy chủ`:

| Dạng | Vì sao dễ lọt |
|---|---|
| HTTP lỗi | `.json()` ném → thường đã đúng |
| `200` + thân HTML | `.json()` ném |
| `200` + `{}` | `.json()` **thành công**; `d.canh_bao \|\| []` cho mảng rỗng → "an toàn" |
| `fetch` ném | mạng đứt |

**Không dùng `|| []`.** Và phép kiểm phải đòi **khẳng định dương** — có đúng câu
báo lỗi đó — chứ không chỉ "không chứa chữ an toàn": một cài đặt giữ đường đọc
`localStorage` làm cache dự phòng sẽ render dữ liệu chết, không chứa chữ nào
trong hai chữ đó, và qua được mọi khẳng định phủ định.

Biểu ngữ khi lỗi **phải hiện** và nói rõ là lỗi. Ẩn nghĩa là "không có gì".

### Dọn rác một lần, giữ bản ghi của trang khác

Khi trang tải, trên `localStorage.activeAlarms`:

- **loại** phần tử có `code` khớp `^(SYS-FLT|OP-ERR|MNT-DUE)-`
- **giữ** phần tử có `code === "SU-CO-KHAN"` hoặc `msg` chứa `"E-Stop"`
- ghi lại bằng `setItem`; **không** `removeItem`

Dọn quá tay là cắt đầu vào của ma trận an toàn `Safety`. Không dọn là để
`Diagnostics` nhấp nháy "sự cố đang chờ xử lý khẩn cấp" vĩnh viễn trên rác.

### Nhịp làm mới chốt 15 giây

Khớp `Alarms.cshtml:135`. Không chốt con số thì một cài đặt đặt 60 giây (hoặc
quên hẳn `setInterval`) vẫn qua được phép kiểm so số dòng — vì vậy phép kiểm
**đếm lượt gọi API**, không so số dòng.

Guard `if (!timer)` / `timer = null` như `Index.cshtml:1489-1490` để interval
không chồng nhau.

### Giữ tên `ackAlarm`

Dây `onclick="ackAlarm(...)"` ở `:1169` do chính `renderAlarmsTable` sinh ra.
Đổi tên hàm thì phải đổi cả hai chỗ cùng lúc.

## Coverage

- CP-01 … CP-07

## Ownership

- Modify: `TotalParking/Views/Home/Index.cshtml`
- Create: `specs/dashboard-canh-bao-that/verify-dashboard-canh-bao.mjs`
- Read: `TotalParking/Views/Home/Alarms.cshtml`, `TotalParking/Controllers/AlarmController.cs`

`TotalParking.csproj` không cần sửa: không thêm file nào mới.

## Acceptance

- AC-01 … AC-12 như `plan.md`.

## Dependencies

- none

## Verification Plan

- **Deploy trước khi đo.** Chép `.cshtml` sang bản deploy **riêng**; việc này
  không làm khởi động lại ứng dụng.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\dashboard-canh-bao-that\verify-dashboard-canh-bao.mjs"`

- **Named probe:** mười ba phép kiểm có tên — `deploy_khop_source`,
  `khong_con_nguon_bia`, `so_dong_khop_csdl`, `cot_nguon_phat_dung`,
  `localstorage_co_rac_van_doc_may_chu`, `ba_bo_dem_khop`, `bieu_ngu_khop`,
  `khong_doc_duoc_khac_trang_thai_sach`, `tu_lam_moi_that_su_chay`,
  `nut_xac_nhan_ghi_duoc`, `don_rac_gia_giu_lai_e_stop`,
  `bieu_ngu_an_khi_khong_con_canh_bao`, `don_sach_du_lieu_thu`.

- **Reachability:** site chạy ở `http://localhost:8080`; Chrome tại
  `C:/Program Files/Google/Chrome/Application/chrome.exe`; CSDL truy vấn được
  bằng `mysql.exe`; bản deploy ở `C:\Users\Admin\Documents\Web\totalParking`.

- **Oracle:**
  - `khong_con_nguon_bia`: đọc `Index.cshtml`, khẳng định không còn `SYS-FLT-`,
    `OP-ERR-`, `MNT-DUE-`, `addRealtimeAlarm`, `operatorErrorsPool`,
    `systemFaultsPool`, `maintenanceDuePool`, `getRandomItem`; và
    `getItem('activeAlarms')` xuất hiện **đúng một lần**, nằm trong hàm dọn rác.
    Hàm dọn rác **bắt buộc** phải đọc khoá đó (AC-10), nên không thể cấm tuyệt
    đối — nhưng hai lần trở lên nghĩa là đường dựng bảng vẫn còn đọc nó, và đó
    chính là nhánh `return` sớm. Bằng chứng thật cho nhánh này là phép kiểm
    sống `localstorage_co_rac_van_doc_may_chu`, không phải phép đếm chuỗi. Đồng thời khẳng định `OperationControl.cshtml` **vẫn**
    có `function saveActiveAlarms` và 4 lời gọi — chốt chặn cho việc xoá nhầm
    file ngoài phạm vi.
  - `localstorage_co_rac_van_doc_may_chu`: **gieo** một mảng giả vào
    `localStorage.activeAlarms` bằng `page.evaluateOnNewDocument` **trước khi**
    tải trang, rồi khẳng định bảng vẫn hiện đúng dữ liệu máy chủ. Không có bước
    gieo này thì nhánh `return` sớm ở `:1031` không bao giờ bị chạm.
  - `so_dong_khop_csdl`: chèn ba cảnh báo thử mang ba `nguon` khác nhau, đối
    chiếu **tập mã lỗi** hiển thị với `GET /Alarm/List` đã lọc `chua_xac_nhan`.
  - `cot_nguon_phat_dung`: một trong ba cảnh báo thử có `block_no = NULL`;
    khẳng định cột Nguồn phát của nó **không chứa** chuỗi `null`, và cảnh báo có
    đủ ba trường thì hiện đủ ba phần.
  - `ba_bo_dem_khop`: đối chiếu với
    `SELECT nguon, COUNT(*) FROM canh_bao WHERE xac_nhan_luc IS NULL GROUP BY nguon`,
    **và** khẳng định tổng ba bộ đếm bằng số `<tr>` trong `#alarms-live-tbody`.
  - `bieu_ngu_khop`: `#global-alarm-banner-count` chứa đúng số chưa xác nhận.
    Nếu số đó bằng 0 thì khẳng định biểu ngữ có class `hidden`; nếu khác 0 thì
    ghi rõ đã bỏ qua nhánh ẩn, **không** im lặng PASS.
  - `khong_doc_duoc_khac_trang_thai_sach`: ép **ba** dạng bằng
    `page.setRequestInterception` — `503`; `200` + `<html>`; `200` + `{}`. Với cả
    ba: thân bảng **phải chứa** câu báo lỗi cố định (khẳng định dương), **không**
    chứa "Hệ thống vận hành an toàn", và biểu ngữ **không** ẩn.
  - `tu_lam_moi_that_su_chay`: đếm số lời gọi tới `/Alarm/List` trong cửa sổ
    **42 giây**; phải **≥ 2**. Đếm lượt gọi chứ không so số dòng: `CanhBaoPlcService`
    ghi cảnh báo thật mỗi 30 giây nên số dòng có thể đổi hợp lệ giữa chừng.
  - `nut_xac_nhan_ghi_duoc`: **bấm nút thật trong DOM** (`#alarms-live-tbody
    button`), trả lời hộp hỏi tên, rồi đọc CSDL khẳng định `xac_nhan_luc` khác
    NULL. Gọi hàm qua `window` thì dây `onclick` ở `:1169` chưa bao giờ chạy.
  - `don_rac_gia_giu_lai_e_stop`: gieo **hai** phần tử — một `code: "SYS-FLT-11"`
    và một `msg` chứa `"E-Stop"` — tải trang, đọc lại khoá: chỉ còn phần tử
    `"E-Stop"`, và khoá **vẫn tồn tại** (`getItem` khác `null`).
  - `don_sach_du_lieu_thu`: xoá cảnh báo thử; số dòng trở lại như trước.

- **Counterexample:**
  - để lại `:1023-1035` → `localstorage_co_rac_van_doc_may_chu` FAIL; mọi phép
    kiểm tĩnh khác vẫn PASS.
  - để lại `:1060-1082` → `khong_con_nguon_bia` FAIL ở chuỗi `MNT-DUE-`.
  - giữ đường đọc làm cache dự phòng → `khong_doc_duoc_khac_trang_thai_sach` FAIL
    ở khẳng định dương.
  - dùng `|| []` → cùng phép kiểm FAIL ở dạng `200` + `{}`.
  - đếm trên cả tập trả về → `ba_bo_dem_khop` FAIL ở khẳng định chéo tổng-vs-dòng.
  - ghép `Block ${block_no}` không kiểm null → `cot_nguon_phat_dung` FAIL.
  - nhịp 60 giây hoặc quên `setInterval` → `tu_lam_moi_that_su_chay` FAIL.
  - `removeItem` khoá → `don_rac_gia_giu_lai_e_stop` FAIL.
  - dọn quá tay, xoá cả phần tử E-Stop → cùng phép kiểm FAIL.

- **Artifacts:** `specs/dashboard-canh-bao-that/artifacts/dashboard-canh-bao.png`
  và `.json` — số dòng, ba bộ đếm, nội dung biểu ngữ, số lượt gọi API, nội dung
  `localStorage` trước/sau; ghi đè mỗi lần chạy. Dữ liệu thử mang tiền tố riêng
  và phải được xoá ở `finally`; dọn thất bại là FAIL nhìn thấy được kèm câu lệnh
  sửa tay.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\dashboard-canh-bao-that\verify-dashboard-canh-bao.mjs"`
Exit: 0
Base: 8245bc264d5c06167d425b1e10c1d78a5bf3fae4
Head: 29762ef1d8b78f92deca9f86a93d363ab2a4f230

```text
  PASS  deploy_khop_source  | cdfb0d51b836
  PASS  khong_con_nguon_bia  | sach 8 chuoi cam; getItem('activeAlarms') 1 lan (mong 1); OperationControl con nguyen: true
  PASS  localstorage_co_rac_van_doc_may_chu  | rac gia hien tren bang: false; so dong 19 vs api 19
  PASS  so_dong_khop_csdl  | 19 dong vs api 19; 4 ma tren bang vs 4 tu api
  PASS  cot_nguon_phat_dung  | thieu block_no -> "Bo phan thu" (khong chua null: true); du ba phan -> "Z2 · Block 21 · PLC 21"
  PASS  ba_bo_dem_khop  | op 1/1, sys 17/17, mnt 1/1; tong ba bo dem 19 vs so dong 19
  PASS  bieu_ngu_khop  | "19 canh bao dang hoat dong:" (mong 19), an=false
  PASS  don_rac_gia_giu_lai_e_stop  | khoa con: true; con 3 phan tu (mong 3); ma: ["SU-CO-KHAN","(khong co code)","(khong co code)"]
  PASS  tu_lam_moi_that_su_chay  | 4 luot goi /Alarm/List trong 65 giay (mong >= 3); NHIP_CANH_BAO_MS = 15000 trong ma nguon: true
  PASS  nut_xac_nhan_ghi_duoc  | xac_nhan_boi="Nguoi Kiem Thu", xac_nhan_luc=co
  PASS  bieu_ngu_an_khi_khong_con_canh_bao  | bieu ngu an: true, so dong: 0, than bang: "Khong co canh bao hoat dong. He thong van hanh an "
  PASS  khong_doc_duoc_khac_trang_thai_sach  | ca 3 dang deu hien cau bao loi co dinh, khong dang nao noi an toan
  PASS  don_sach_du_lieu_thu  | so dong 16 -> 16

13/13 PASS
EXIT=0
```

### Phép kiểm bắt được nhánh `return` sớm

`localstorage_co_rac_van_doc_may_chu` **gieo bốn phần tử giả vào
`localStorage.activeAlarms` trước khi tải trang**, rồi khẳng định rác không hiện
trên bảng. Đây là phép kiểm duy nhất chạm tới nhánh `return` ở dòng `:1031` của
bản cũ — nhánh đó không chứa chuỗi cấm nào, nên đo trên hồ sơ trình duyệt sạch
thì mọi phép kiểm tĩnh đều xanh trong khi trang vẫn render mảng giả.

### Vòng rà soát trả về FAIL, sáu phát hiện đã sửa

| Phát hiện | Sửa |
|---|---|
| `bieu_ngu_khop` so **chuỗi con** (`"13…".includes("3")` là `true`) và nhánh ẩn là **mã chết theo cấu trúc** — script tự chèn ba cảnh báo trước khi mở trình duyệt | So bằng `parseInt`; thêm probe riêng `bieu_ngu_an_khi_khong_con_canh_bao` ép phản hồi rỗng |
| `so_dong_khop_csdl` so **tập** mã lỗi: 19 dòng nhưng chỉ 4 mã, nên một cài đặt khử trùng lặp chỉ vẽ 4 dòng vẫn PASS | Thêm khẳng định số dòng |
| Cửa sổ 42 giây với nhịp 15 giây cho **đúng 2** lượt — biên bằng 0: nhịp 20 giây cũng PASS, một tick bị throttle là FAIL giả | Nới 65 giây, đòi ≥3, **và** khẳng định tĩnh `NHIP_CANH_BAO_MS = 15000` |
| `don_rac_gia_giu_lai_e_stop` gieo một phần tử mang **cả hai** luật giữ nên không tách được; `Safety` dò theo **chuỗi**, không theo `code` | Gieo bốn phần tử, khẳng định còn đúng ba |
| Cột Thời gian cắt còn `HH:mm:ss` — một sự cố ba hôm trước hiện `08:15:02` và trực ban đọc thành "sáng nay" | Hiện đủ `yyyy-MM-dd HH:mm:ss`, giống `Alarms.cshtml:167` |
| Khối style `.alarm-row-new` thành mã chết; chú thích IIFE vẫn ghi "Alarm vẫn là dữ liệu mô phỏng" | Xoá khối, sửa chú thích |

### Ràng buộc "không được đụng" được biến thành phép kiểm chạy được

`khong_con_nguon_bia` không chỉ kiểm `Index.cshtml` sạch — nó còn khẳng định
`OperationControl.cshtml` **vẫn có** `function saveActiveAlarms` và ≥4 lời gọi.
Không có chốt này thì một người đọc chỉ dẫn sai của bản packet đầu sẽ grep, tìm
ra hàm đó ở file ngoài phạm vi, xoá nó, và cắt đứt đường ghi E-Stop nuôi ma trận
an toàn của `Safety` — mà mọi phép kiểm khác vẫn xanh.

## Hạn chế còn lại

1. **`Diagnostics` và `Safety` vẫn đọc `localStorage.activeAlarms`.** Sau packet
   này khoá đó chỉ còn nhận bản ghi do `OperationControl` ghi, nên hai trang ấy
   gần như luôn đọc được mảng rỗng. Hành vi này **thay đổi nhìn thấy được** so
   với trước, và nằm ngoài phạm vi theo quyết định C1.
2. **Nhánh ẩn biểu ngữ chỉ chứng minh được bằng phản hồi giả lập.** CSDL đang có
   16 cảnh báo thật chưa xác nhận; ép trạng thái "0 cảnh báo" thật đòi phải xác
   nhận hết chúng. Probe chứng minh **ánh xạ phía trình duyệt**, không chứng minh
   máy chủ có bao giờ trả mảng rỗng.
3. **Bảng không có trần số dòng.** Nhánh chưa xác nhận của `/Alarm/List` không có
   `LIMIT`; 300 cảnh báo tồn đọng cuối tuần sẽ dựng 300 `<tr>` mỗi 15 giây trên
   màn hình tường. Chưa xử lý.
4. **Nhánh `nguon` lạ chưa từng chạy.** Mã hiện nguyên văn giá trị thay vì giấu
   vào nhóm 2, nhưng `ENUM` hiện chỉ có ba giá trị nên nhánh đó là phòng thủ
   chiều sâu, chưa có bằng chứng.
5. **Hai lớp CSS trong nhánh đó không có trong `scada.css`**
   (`hover:bg-slate-800/40`, `border-slate-700`) — vô hại vì nhánh không chạy,
   nhưng sẽ hiện nhợt nhạt nếu có ngày nó chạy.
6. **Phần lớn lớp CSS của ba nhãn phân loại vốn đã không có trong `scada.css`**
   (`bg-amber-500/10`, `bg-red-500/15`, cả họ `cyan-*`). Đây là **nợ có sẵn**;
   packet giữ nguyên bộ lớp cũ, chỉ sửa hai lớp rác `bg-amber-955/5` và
   `bg-red-955/5` (số `955` không tồn tại trong Tailwind).
