# Task 03 — PLC mất kết nối sinh cảnh báo tự động

Status: done

## Outcome

PLC mất kết nối quá ngưỡng thì có một cảnh báo trong bảng, không phải người tự
để ý. PLC vẫn đang lỗi thì không sinh thêm dòng nào. Kết nối lại thì cảnh báo
được đánh dấu đã hết, dòng vẫn còn, và lần sự cố sau vẫn sinh được cảnh báo mới.

## Scope

- **In:** dịch vụ nền `CanhBaoPlcService` đọc `plc_device.is_connected` rồi sinh
  và đóng cảnh báo; nhịp chạy **30 giây**; ngưỡng cấu hình được, mặc định 5 phút;
  bắt lỗi trùng khoá; bỏ qua `is_connected IS NULL`.
- **Out:** không đụng `block.is_active` (việc của `CongVanHanhService` ở packet
  trước); không cảnh báo cho LED, PGS, camera; không gửi ra ngoài.

### Chống trùng là yêu cầu chính, không phải chi tiết

Vòng chạy 30 giây. Một PLC chết ba ngày mà mỗi vòng sinh một dòng thì sau ba ngày
bảng có **8.640 dòng cho một sự cố**, và trang Alarms thành vô dụng.

Cách chặn: cột `khoa_chong_trung` có ràng buộc `UNIQUE` (định nghĩa ở task 01),
giá trị dạng `PLC_MAT_KET_NOI:<block_no>` và **chỉ đặt khi cảnh báo đang mở**.
Khi đóng thì xoá khoá về `NULL`, để lần sự cố sau vẫn sinh được dòng mới. Dùng
ràng buộc CSDL chứ không kiểm trong mã: hai tiến trình cùng chạy thì
kiểm-rồi-ghi vẫn lọt.

**Phải bắt lỗi `1062` (trùng khoá) như no-op.** Nếu để nó thoát ra, lượt chạy đó
dừng trước bước đóng cảnh báo cho PLC đã nối lại — và `van_loi_khong_sinh_them`
vẫn PASS, che mất lỗi.

### `NULL` không phải là `0`

`PlcDeviceRepository.cs:167-169` ghi rõ: *"Thiết bị rời vòng poll thì không còn
quan sát được. NULL nghĩa là 'không biết', khác hẳn 0 nghĩa là 'biết chắc đang
chết'."*

Nếu dịch vụ coi `is_connected <> 1` là mất kết nối, thì mỗi lần thiết bị bị gỡ
khỏi vòng poll sẽ sinh cảnh báo giả. **Chỉ `is_connected = 0` mới sinh cảnh báo.**

### `plc_device` không có cột `block_no`

Khoá ngoài là `block_id`, còn `block_no` nằm ở bảng `block` và **hai giá trị khác
nhau**. Phải `JOIN block b ON b.block_id = d.block_id` rồi dùng `b.block_no`.
Dùng nhầm `block_id` làm `block_no` thì khoá chống trùng và cột `block_no` trong
cảnh báo cùng trỏ sai khối, mà cả hai phía cùng sai nên phép kiểm vẫn PASS.

### Ngưỡng 5 phút, và lớp chống rung đã có sẵn ở tầng dưới

`PlcTrangThaiWriter` yêu cầu **3 lần quan sát liên tiếp** cùng trạng thái, nhịp
5 giây (`PlcTrangThaiWriter.cs:37,41`) — tức khoảng 15 giây ổn định — trước khi
ghi `is_connected`. Một sự cố vài giây không bao giờ tới được cột đó.

Ngưỡng 5 phút của task này là **lớp thứ hai**. Ngắn hơn thì bảng đầy cảnh báo tự
tắt và người vận hành bắt đầu bỏ qua chúng — đó là cách một hệ thống cảnh báo
chết.

### Ngưỡng đọc từ đâu, và vì sao không dùng `Web.config` khi đo

App chạy từ bản deploy `C:\Users\Admin\Documents\Web\totalParking`, **không phải**
cây mã nguồn — sửa `Web.config` trong source là vô nghĩa. Và ghi `Web.config` bản
deploy **làm ứng dụng nạp lại**: hai lần ghi (hạ rồi khôi phục) là hai lần khởi
động lại ngay giữa phép đo.

Vì vậy script rút ngắn ngưỡng bằng cách ghi trực tiếp `connected_changed_at` lùi
về quá khứ cho khối thử, thay vì đổi cấu hình. Cách đó không chạm `Web.config`,
không gây nạp lại, và ép được đúng điều kiện ngưỡng.

## Coverage

- CP-03

## Ownership

- Create: `TotalParking/Services/Plc/CanhBaoPlcService.cs`
- Create: `specs/canh-bao-su-co/verify-canh-bao-plc.mjs`
- Modify: `TotalParking/Services/Plc/PlcHost.cs`
- Modify: `TotalParking/Services/CanhBaoRepository.cs`
- Modify: `TotalParking/TotalParking.csproj`
- Modify: `TotalParking/Web.config`
- Read: `TotalParking/Services/Plc/CongVanHanhService.cs`, `TotalParking/Services/PlcDeviceRepository.cs`

`TotalParking.csproj` là bắt buộc: csproj kiểu cũ liệt kê từng file `.cs`, nên
file mới không có trong đó sẽ không được biên dịch và `PlcHost` gọi nó sẽ lỗi
`CS0103`.

## Acceptance

- AC-07: PLC mất kết nối quá ngưỡng sinh đúng **một** cảnh báo; vẫn lỗi thì
  không sinh thêm qua nhiều vòng chạy.
- AC-08: PLC nối lại thì cảnh báo được đánh dấu hết, dòng vẫn còn, và lần sự cố
  tiếp theo sinh được cảnh báo mới.
- AC-09: `is_connected` là `NULL` thì không sinh cảnh báo.
- AC-12: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-01-bang-va-api.md

## Verification Plan

- **Thứ tự triển khai:** bảng của task 01 phải có trước khi chép DLL.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-canh-bao-plc.mjs"`

- **Named probe:** tám phép kiểm có tên — `deploy_khop_source`,
  `mat_ket_noi_sinh_canh_bao`, `van_loi_khong_sinh_them`,
  `noi_lai_thi_danh_dau_het`, `dong_cu_van_con_trong_bang`,
  `su_co_lan_hai_sinh_duoc_canh_bao_moi`, `null_khong_sinh_canh_bao`,
  `khoi_phuc_nguyen_trang`.

- **Reachability:** CSDL truy vấn được; `POST /PlcStatus/Reload` chạy từ
  loopback; `GET /Alarm/List` gọi được.

- **Oracle:**
  - Ép mất kết nối bằng cách đổi `port` của đúng một dòng `plc_device` sang cổng
    không ai nghe rồi `Reload` — cùng cách đã dùng ở
    `specs/tach-vai-co-plc/verify-is-connected.mjs`, đã chứng minh chạy được.
  - Chọn khối **đang trống** theo đúng tiêu chí packet trước: không có
    `plc_slot_state` còn thẻ, không `parking_session` đang mở, không
    `vehicle_routing` nào `ROUTED` trong 90 giây.
  - Rút ngắn ngưỡng bằng cách đặt `connected_changed_at` lùi về quá khứ cho khối
    thử, **không** sửa `Web.config` (xem mục trên).
  - `van_loi_khong_sinh_them`: sau khi có cảnh báo đầu, chờ **quá 90 giây** —
    tức hơn ba vòng chạy 30 giây — rồi đếm lại; số cảnh báo cho khối đó phải vẫn
    là 1. Ghi bằng giây tuyệt đối, không nói "ba vòng".
  - `su_co_lan_hai_sinh_duoc_canh_bao_moi`: chạy trọn chu trình **hai lần**. Nếu
    quên xoá khoá lúc đóng thì lần hai không sinh được cảnh báo và probe này FAIL
    — đây là lỗi mà một lần chạy không bao giờ bắt được.
  - `null_khong_sinh_canh_bao`: đặt `is_connected = NULL` cho khối thử, chờ quá
    ngưỡng, khẳng định không có cảnh báo mới nào cho khối đó.
  - Script ghi `block_no`, `port` gốc và `connected_changed_at` gốc ra artifact
    **trước** khi đổi gì, và khôi phục ở `finally`. Khôi phục thất bại là FAIL
    nhìn thấy được, kèm câu lệnh sửa tay.

- **Counterexample:**
  - thiếu khoá chống trùng → `van_loi_khong_sinh_them` FAIL.
  - đóng cảnh báo bằng cách xoá dòng → `dong_cu_van_con_trong_bang` FAIL.
  - quên xoá khoá khi đóng → `su_co_lan_hai_sinh_duoc_canh_bao_moi` FAIL.
  - coi `NULL` là mất kết nối → `null_khong_sinh_canh_bao` FAIL.
  - để lỗi `1062` thoát ra vòng chạy → `noi_lai_thi_danh_dau_het` FAIL, vì lượt
    đó dừng trước bước đóng cảnh báo.

- **Artifacts:** `specs/canh-bao-su-co/artifacts/canh-bao-plc.json` — khối đã
  dùng, `port` và `connected_changed_at` gốc, chuỗi trạng thái theo thời gian;
  ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\canh-bao-su-co\verify-canh-bao-plc.mjs"`
Exit: 0
Base: 383ad696b1bf127f30367c194abd9d20c337eb0c
Head: f679a82b595c9f14a9b93d8f371f6b8406156529

```text
  PASS  deploy_khop_source  | 5505c825f854
  khoi thu: block 2 (plc_id 136), nguong 5 phut

  PASS  mat_ket_noi_sinh_canh_bao  | id=230, nguon=hardware, muc_do=critical, mo_ta="PLC khoi 2 (192.169.1.102) mat ket noi 7 phut."
  PASS  van_loi_khong_sinh_them  | sau 95 giay PLC van chet: 1 canh bao cho block 2 (mong doi 1)
  PASS  noi_lai_thi_danh_dau_het  | canh bao 230 da co het_luc
  PASS  dong_cu_van_con_trong_bang  | dong 230 van con, khoa_chong_trung = NULL (dung)
  PASS  su_co_lan_hai_sinh_duoc_canh_bao_moi  | lan 1 id=230, lan 2 id=269 — khoa da duoc xoa dung luc dong
  PASS  null_khong_sinh_canh_bao  | is_connected=NULL suot phep thu, canh bao moi cho block 2: 0 (mong doi 0)
  PASS  khoi_phuc_nguyen_trang  | port 9600 (goc 9600), is_connected 1, da xoa 2 canh bao thu

8/8 PASS
EXIT=0
```

### Bảy cảnh báo thật, không phải dữ liệu thử

Ngay khi dịch vụ lên, bảng có đúng **7 dòng** cho 7 PLC đang mất kết nối thật
(khối 12, 21, 22, 33, 52, 55, 69) — mỗi khối một dòng, không dòng nào trùng.
Đây là sự cố có thật do bên khách rút nguồn theo line để kiểm thử, không phải
dữ liệu script tự chèn.

### Chống trùng được chứng minh bằng thời gian tuyệt đối

`van_loi_khong_sinh_them` chờ **95 giây** — hơn ba lượt của vòng 30 giây — với
PLC vẫn đang chết, và số cảnh báo cho khối đó vẫn là 1. Không có khoá chống
trùng thì con số ở đây là 4.

### Lỗi mà một chu trình không bắt được

`su_co_lan_hai_sinh_duoc_canh_bao_moi` chạy trọn chu trình lần hai: rớt → sinh
cảnh báo → nối lại → đóng → rớt lại. Lần hai sinh được `id=269` khác `id=230`,
tức khoá đã thực sự được xoá lúc đóng. Nếu quên xoá, bảy phép kiểm kia vẫn PASS
và chỉ phép này FAIL.

### `NULL` không bị coi là mất kết nối

`null_khong_sinh_canh_bao` đặt `is_connected = NULL` kèm `connected_changed_at`
lùi quá ngưỡng — đúng điều kiện sẽ sinh cảnh báo nếu dịch vụ coi `is_connected
<> 1` là chết. Sau 75 giây: 0 cảnh báo mới.

### Ngưỡng được rút bằng dữ liệu, không bằng cấu hình

Script lùi `connected_changed_at` thay vì sửa `Web.config` bản deploy. Ghi
`Web.config` làm ứng dụng nạp lại, tức hai lần khởi động lại ngay giữa phép đo.

## Hạn chế còn lại

1. **Mỗi lượt chạy vẫn thử INSERT cho khối đã có cảnh báo mở.** 7 PLC chết thì
   cứ 30 giây có 7 lệnh INSERT bị ràng buộc `UNIQUE` từ chối. Không sinh dòng
   thừa (bảng vẫn đúng 7 dòng), nhưng mỗi lần bị từ chối vẫn đốt một số
   `AUTO_INCREMENT` — đó là lý do `id` nhảy từ 230 lên 269 trong phép đo. Cột là
   `BIGINT UNSIGNED` nên không có nguy cơ cạn; lọc trước bằng `NOT EXISTS` sẽ
   gọn hơn nhưng chưa làm.
2. **Ngưỡng 5 phút là con số tôi chọn**, chưa hiệu chỉnh theo thực tế vận hành.
   Đổi bằng `plc:canhBaoMatKetNoiSauPhut` trong `Web.config` bản deploy (đổi
   xong ứng dụng tự nạp lại).
3. **Chỉ cảnh báo cho PLC.** LED, CCU, camera chưa có cảnh báo nào — nằm ngoài
   phạm vi packet này.
4. **Cảnh báo chỉ đóng khi PLC nối lại.** Không có cơ chế người vận hành đóng
   tay một cảnh báo PLC; họ chỉ xác nhận được nó trên trang Alarms.
5. **Dịch vụ chỉ chạy khi `plc:enabled = true`.** Tắt vòng poll thì `is_connected`
   không được cập nhật nữa, nên sinh cảnh báo từ cột đó sẽ là cảnh báo từ dữ
   liệu cũ.
