# Task 04 — Nút ghi nhận sự cố khẩn thay cho ESTOP giả

Status: done

## Outcome

Nút khẩn trên trang Điều khiển vận hành ghi sự cố vào CSDL, **trình duyệt khác
đọc được**, và nhãn của nó nói đúng việc nó làm — không hứa dừng máy.

## Vì sao task này tồn tại

`Views/Home/OperationControl.cshtml:345` có nút nhãn **`ESTOP`** gọi
`setBlockMode('Emergency')`. Hàm đó (`:946-967`) làm ba việc:

```javascript
if (blockStates[selectedBlockId].status === "Disabled" && mode !== "Maintenance") {
    alert(...); return;                       // guard bao tri — PHAI GIU
}
blockStates[selectedBlockId].mode = mode;
saveBlockStates();                            // ghi localStorage cua CHINH trinh duyet do
if (mode === "Emergency") {
    blockStates[selectedBlockId].status = "Warning";
    saveBlockStates();
}
refreshOCCView();
```

Không gửi gì xuống PLC. Không ghi CSDL. Máy khác không thấy. Mở trình duyệt khác
thì nút coi như chưa từng được bấm.

**Đây là rủi ro an toàn, không phải thiếu tính năng.** Người vận hành trong tình
huống khẩn bấm nút mang nhãn dừng khẩn cấp, thấy ô chuyển đỏ, và tin rằng đã xử
lý — trong khi không có gì xảy ra và không ai khác biết.

### Ô màu đỏ cục bộ cũng phải đi, không chỉ nhãn

Bản kế hoạch đầu định "giữ phần tô màu ô". Đó là **tự mâu thuẫn**: chính ô đỏ
per-browser là thứ tạo cảm giác "đã xử lý" mà task này tuyên bố muốn diệt. Giữ
nó lại thì sau packet, máy A thấy ô đỏ còn máy B không — thêm một dòng CSDL chỉ
làm cảm giác sai đó có vẻ chính đáng hơn.

Nên trạng thái hiển thị của nút khẩn phải suy ra từ **kết quả API**, không từ
`occBlockStates`.

### `Safety.cshtml` là reader không ai khai báo

`CLAUDE.md` liệt kê `occBlockStates` chỉ do `OperationControl` đọc. Sai:

- `Safety.cshtml:255` đọc `occBlockStates`
- `Safety.cshtml:256` đọc `activeAlarms`
- `Safety.cshtml:259` — `alarms.some(a => ... a.message.includes("E-Stop"))`
- `Safety.cshtml:270-273` — biến kết quả đó thành dòng khoá liên động
  `FAIL` *"E-Stop khẩn cấp tại Block đang kích hoạt!"*

Tức là **ma trận an toàn của trang `Safety` đang dò chuỗi `"E-Stop"`**. Task này
đổi nhãn và đổi hành vi nút, nên phải biết mình đang chạm vào đó.

Lưu ý: `Safety.cshtml` hiện **đã hỏng sẵn** — nó dùng `a.message` trong khi nơi
ghi dùng `a.msg`, nên `renderMatrix()` ném `TypeError` mỗi khi có cảnh báo trong
localStorage. Sửa nó nằm ngoài packet này (xem `plan.md` mục Out of scope), nhưng
task này **không được làm tình hình tệ hơn**: nếu bỏ hẳn việc ghi `activeAlarms`
thì khi `Safety` được sửa sau này, ma trận E-Stop sẽ mãi báo `OK`. Vì vậy task
giữ nguyên việc ghi `activeAlarms` với chuỗi chứa `"E-Stop"`, và ghi rõ lý do
trong mã để người sửa `Safety` sau này không gỡ nhầm.

## Scope

- **In:** nút gọi `POST /Alarm/SuCoKhan` ghi cảnh báo mức `critical` kèm khối và
  thời điểm; đổi nhãn và chú thích; trạng thái hiển thị suy từ API; giữ nguyên
  guard bảo trì và việc ghi `activeAlarms` chứa chuỗi `"E-Stop"`.
- **Out:** không ghi lệnh dừng xuống PLC; không đụng Start, Stop, Pause, Lock,
  Bypass, Override; không sửa `Safety.cshtml`.

### Nhãn là một yêu cầu an toàn

Nút này **không dừng được máy**. Nhãn phải phản ánh đúng, nếu không thì việc ghi
được CSDL còn làm tệ hơn: nó tạo cảm giác hệ thống đã "nhận lệnh". Dừng khẩn thật
là việc của nút cơ khí ngoài hiện trường.

## Coverage

- CP-04

## Ownership

- Modify: `TotalParking/Views/Home/OperationControl.cshtml`
- Modify: `TotalParking/Controllers/AlarmController.cs`
- Create: `specs/canh-bao-su-co/verify-nut-su-co.mjs`
- Read: `TotalParking/Services/CanhBaoRepository.cs`, `TotalParking/Views/Home/Safety.cshtml`

## Acceptance

- AC-10: bấm nút ghi một cảnh báo mức `critical` kèm khối và thời điểm; một
  trình duyệt **khác** đọc được nó.
- AC-11: `Views/Home/OperationControl.cshtml` không còn chuỗi `ESTOP` /
  `dừng khẩn` / `emergency stop`, và có chú thích nói rõ nút chỉ ghi nhận.
- AC-12: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-01-bang-va-api.md

## Verification Plan

- **Build và deploy trước khi đo**, và chép `.cshtml` riêng ngoài `bin\*.dll`.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-nut-su-co.mjs"`

- **Named probe:** bảy phép kiểm có tên — `deploy_khop_source`,
  `nhan_khong_hua_dung_may`, `guard_bao_tri_con_nguyen`,
  `bam_nut_ghi_duoc_csdl`, `trinh_duyet_khac_doc_duoc`,
  `mau_khong_phu_thuoc_trinh_duyet`, `don_sach_du_lieu_thu`.

- **Reachability:** site chạy ở `http://localhost:8080`; Chrome dùng được; CSDL
  truy vấn được bằng `mysql.exe`.

- **Oracle:**
  - `nhan_khong_hua_dung_may`: đọc **chỉ** `Views/Home/OperationControl.cshtml`,
    khẳng định không còn `ESTOP` / `dừng khẩn` / `emergency stop` (không phân
    biệt hoa thường), và có chú thích chứa chữ "ghi nhận".

    Phạm vi phải đúng một file. Grep cả `Views/` sẽ FAIL oan ở năm vị trí khác,
    trong đó `Safety.cshtml:122` (*"E-Stop không bị nhấn"*) là hạng mục checklist
    an toàn **phải giữ**. Trong riêng file này, chuỗi `ESTOP` hiện xuất hiện đúng
    một lần nên phép kiểm chặt và không bắt nhầm.
  - `guard_bao_tri_con_nguyen`: khẳng định nhánh chặn khi `status === "Disabled"`
    vẫn còn. Nếu viết đè mất guard này thì khối đang khoá bảo trì lại nhận được
    thao tác.
  - `bam_nut_ghi_duoc_csdl`: mở trang bằng puppeteer, chọn một khối, bấm nút, đọc
    CSDL khẳng định có cảnh báo mới đúng khối đó, mức `critical`.
  - `trinh_duyet_khac_doc_duoc`: gọi `GET /Alarm/List` bằng `fetch` **ngoài**
    trình duyệt vừa bấm — chứng minh dữ liệu nằm ở máy chủ, không ở
    `localStorage`. Đây là điểm khác biệt cốt lõi so với nút cũ.
  - `mau_khong_phu_thuoc_trinh_duyet`: mở **context thứ hai** của puppeteer
    (không dùng chung `localStorage`), tải trang, khẳng định khối vừa báo sự cố
    hiển thị đúng trạng thái. Không có probe này thì ô đỏ cục bộ vẫn sống sót.
  - `don_sach_du_lieu_thu`: xoá dòng do script tạo; số dòng trở lại như trước.

- **Counterexample:**
  - nút vẫn chỉ ghi `localStorage` → `trinh_duyet_khac_doc_duoc` FAIL.
  - giữ nhãn `ESTOP` → `nhan_khong_hua_dung_may` FAIL.
  - giữ ô đỏ per-browser → `mau_khong_phu_thuoc_trinh_duyet` FAIL.
  - viết đè mất guard bảo trì → `guard_bao_tri_con_nguyen` FAIL.

- **Artifacts:** `specs/canh-bao-su-co/artifacts/nut-su-co.png` và
  `nut-su-co.json`; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-nut-su-co.mjs"`
Exit: 0
Base: 383ad696b1bf127f30367c194abd9d20c337eb0c
Head: db3ceefe0969d6ba29054c424e658cf19d913028

```text
  PASS  deploy_khop_source  | 5f68f1aecfa5
  PASS  nhan_khong_hua_dung_may  | khong con chuoi hua dung may, co chu thich "ghi nhan": true
  PASS  guard_bao_tri_con_nguyen  | nhanh chan khi status === "Disabled" van con
  PASS  bam_nut_ghi_duoc_csdl  | id=363, muc_do=critical, nguon=operation, thiet_bi="Block A-01"
  PASS  trinh_duyet_khac_doc_duoc  | GET /Alarm/List (ngoai trinh duyet) co su co cua "Block A-01"
  PASS  mau_khong_phu_thuoc_trinh_duyet  | context thu hai: dau su co co hien, localStorage rong (dung la context rieng)
  PASS  don_sach_du_lieu_thu  | so dong 7 -> 7

7/7 PASS
EXIT=0
```

### Điểm khác biệt cốt lõi được đo bằng hai phép riêng

`trinh_duyet_khac_doc_duoc` gọi `GET /Alarm/List` **từ Node**, không qua trình
duyệt vừa bấm — chứng minh dữ liệu nằm ở máy chủ. `mau_khong_phu_thuoc_trinh_duyet`
mở một context riêng (localStorage rỗng, script khẳng định điều đó) và vẫn thấy
dấu sự cố trên đúng thẻ khối. Nút cũ sẽ trượt cả hai.

### Phát hiện trong lúc làm: khối trên trang OCC là tên giả

`zoneBlocksMap` (`OperationControl.cshtml:532`) là danh sách **viết cứng** 18 tên
kiểu `"Block A-01"`, không phải `block_no` thật của 112 khối. Ánh xạ tên đó sang
một con số là bịa, nên cảnh báo ghi `block_no = NULL` và lưu **đúng nhãn người
bấm nhìn thấy** vào cột `thiet_bi`.

### Một lỗi của phép kiểm, không phải của sản phẩm

Lần chạy đầu ba phép FAIL vì script đọc `window.selectedBlockId` — biến khai báo
bằng `let` ở phạm vi script nên không gắn vào `window` và trả `undefined`. Dữ
liệu ghi xuống CSDL lúc đó đã đúng (`thiet_bi="Block A-01"`). Đã đổi sang đọc
`#occ-block-select` và chạy lại.

## Hạn chế còn lại

1. **Cảnh báo sự cố khẩn không tự đóng.** Khác cảnh báo PLC (nối lại thì đóng),
   dòng này chỉ mất dấu trên trang OCC khi có người **xác nhận** nó ở trang
   Alarms. Chưa có nút "đóng sự cố" cho người vận hành.
2. **Mỗi lần bấm là một dòng.** Không dùng khoá chống trùng (có chủ ý: mỗi lần
   báo là một sự kiện), nên bấm ba lần sinh ba dòng.
3. **`block_no` để trống** vì lý do ở trên. Muốn có số khối thật thì phải thay
   `zoneBlocksMap` bằng danh sách khối từ CSDL — ngoài phạm vi packet này.
4. **`Safety.cshtml` không được sửa** (ngoài phạm vi). Nút vẫn ghi `activeAlarms`
   với chuỗi chứa `"E-Stop"` để ma trận an toàn của trang đó hoạt động được sau
   khi ai đó sửa nó; có chú thích trong mã dặn đừng gỡ.
5. **Nút không dừng máy, và sẽ không bao giờ.** Dừng khẩn cấp thật là nút cơ khí
   tại chỗ; phần mềm chỉ ghi nhận.
6. **Nhịp làm mới 15 giây**: một máy khác thấy sự cố chậm nhất sau 15 giây.
