# Task 01 — Tầng cảm biến đỗ thường đọc từ CCU

Status: done

## Outcome

`/PgsStatus/Index` trả số ô trống / có xe / lỗi / không lắp lấy từ CCU
`192.169.1.75:2000` theo đúng giao thức nhà cung cấp, tách riêng nhóm ZCU đang
kết nối với nhóm dữ liệu đóng băng. SCADA không còn mở socket nào tới 5 ZCU.

## Scope

**In:**

1. Luồng riêng cho CCU với đồng hồ `LIVE` độc lập, gửi `$CCU,01,LIVE*42#` mỗi
   5 giây, không phụ thuộc nhịp của vòng nền hiện có.
2. Tách khung `$...#`; **chỉ `$CCU,02` là gói dữ liệu**; `$CCU,01,OK` ghi nhận
   là nhịp sống của CCU và phơi thành `ccu_tra_loi_cach_day_giay`.
3. Tách trường bằng cả `,` và `*` (ID ZCU có thể dài 2 ký tự); so CRC không
   phân biệt hoa/thường; bảng ZCU đánh chỉ mục theo `X2` với sức chứa **16**.
4. Giữ `X3` từng ZCU; đồng hồ riêng mỗi ZCU; đánh dấu quá hạn khi không có gói
   mới quá 10 giây kể cả `X3 = 1`.
5. Endpoint trả hai nhóm số tách hẳn nhau: `dang_ket_noi` và `dong_bang`.
   Không có con số gộp duy nhất nào.
6. Cổng tự kiểm `POST /PgsStatus/TuKiem` **chỉ nhận từ loopback**, nhận chuỗi
   khung và trả kết quả giải mã. Không ghi gì, không chạm thiết bị.
7. Nhóm PGS trên dashboard hiện 1/1 chứ không hiện "0/0" màu xanh.

   **Sửa lúc thi công:** bản C2 viết mục này là "trỏ `pgs:hosts` về CCU thay vì
   xoá". Cách đó mâu thuẫn trực tiếp với mục 8: `pgs:hosts` còn trỏ `.75` thì
   `ProbeGroup` vẫn mở một socket tới CCU mỗi 15 giây khi hết bộ nhớ đệm — đúng
   cơ chế đã sinh 33 socket FinWait2 hồi tháng 9. Nên `pgs:hosts` bị bỏ hẳn và
   `DeviceProbeService` lấy 1/1 từ `PgsHost.Ccu`. Mục tiêu quan sát được của
   mục 7 giữ nguyên; chỉ cách đạt là khác.
8. `DeviceProbeService` lấy trạng thái PGS từ `CcuConnection`, không tự mở socket.
9. Gỡ `PgsFrame.cs` và `PgsConnection.cs` khỏi luồng chạy và khỏi csproj.
10. Đường CCU công bố ngay mọi gói CRC hợp lệ — `pgs:debounceMs` ra khỏi đường chạy.
11. Chỉ đóng kết nối CCU khi `Read` trả 0 hoặc quá 30 giây không có gói `$CCU,02`;
    hết thời gian chờ đọc đơn thuần không phải lỗi.

**Out:** bảng LED, view sức chứa trong CSDL, ánh xạ cảm biến về ô đỗ, chống nhiễu
theo từng cảm biến, mọi lệnh ghi cấu hình xuống thiết bị.

**Khoá cấu hình chốt cứng:** `pgs:ccuHost`, `pgs:ccuPort`, `pgs:liveIntervalMs`,
`pgs:zcuQuaHanMs`. Không tự đặt tên khác.

## Coverage

- CP-01, CP-02, CP-03, CP-04, CP-05

## Ownership

- Create: `TotalParking/Services/Pgs/CcuFrame.cs`
- Create: `TotalParking/Services/Pgs/CcuConnection.cs`
- Create: `specs/pgs-doc-ccu/verify-ccu.mjs`
- Create: `specs/pgs-doc-ccu/verify-qua-han.mjs`
- Modify: `TotalParking/Services/Pgs/PgsHost.cs`
- Modify: `TotalParking/Controllers/PgsStatusController.cs`
- Modify: `TotalParking/Services/DeviceProbeService.cs`
- Modify: `TotalParking/Web.config`
- Modify: `TotalParking/TotalParking.csproj`
- Delete: `TotalParking/Services/Pgs/PgsFrame.cs`
- Delete: `TotalParking/Services/Pgs/PgsConnection.cs`
- Read: `TotalParking/Global.asax.cs`, `TotalParking/Views/Home/Index.cshtml`

## Acceptance

- AC-01: bốn con số nhóm `dang_ket_noi` khớp đúng dữ liệu script đọc trực tiếp
  từ CCU trong cùng một lần chạy.
- AC-02: mỗi ZCU có `X3 = 0` nằm ở nhóm `dong_bang`, có tuổi tính từ lần cuối
  `X3 = 1`, và không được cộng vào `dang_ket_noi`.
- AC-03: ZCU không có gói mới quá 10 giây bị đánh dấu quá hạn kể cả khi `X3 = 1`.
- AC-04: khung sai CRC và khung `$CCU,01,OK` đều bị loại khỏi phép đếm dữ liệu,
  mỗi loại có bộ đếm riêng, và số đang công bố không đổi vì chúng.
- AC-05: không còn kết nối TCP nào tới `.70`–`.74` cổng 2000; đúng một kết nối
  Established tới `.75`.
- AC-06: `bin\TotalParking.dll` ở cây mã nguồn trùng dấu vân tay với bản deploy.

## Dependencies

- none

## Verification Plan

- **Build và deploy trước khi đo** (app phục vụ từ `C:\Users\Admin\Documents\Web\totalParking`,
  không phải cây mã nguồn; sửa `.cs` mà chỉ chép `.cshtml` là vô nghĩa):
  1. `& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking.sln /p:Configuration=Debug`
  2. chép `TotalParking\bin\*.dll` và `Web.config` sang bản deploy
  3. đợi IIS nạp lại

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-ccu.mjs"`

- **Named probe:** `verify-ccu.mjs`, bảy phép kiểm có tên — `deploy_khop_source`,
  `so_dang_ket_noi_khop_ccu`, `nhom_dong_bang_tach_rieng`, `zcu_qua_han_bi_danh_dau`,
  `khung_hong_bi_loai`, `khong_con_socket_zcu`, `mot_socket_ccu`.

- **Lệnh thứ hai (thêm lúc thi công):**
  `& "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-qua-han.mjs"`

  Review độc lập chỉ ra rằng ngoài hiện trường lúc này cả 5 ZCU đều `X3 = 1` và
  gói đều tươi, nên trong `verify-ccu.mjs` hai phép kiểm mang tên AC-02 và AC-03
  chạy trên tập rỗng: chúng in PASS mà không chứng minh gì. Script thứ hai hạ
  `pgs:zcuQuaHanMs` xuống 1 giây để tạo ra tình huống quá hạn thật, rồi tự khôi
  phục cấu hình. Sáu phép kiểm có tên — `nguong_da_ha`, `zcu_bi_danh_dau_qua_han`,
  `qua_han_roi_vao_nhom_dong_bang`, `khong_dem_trung`, `so_hoc_tung_nhom_khop`,
  `khoi_phuc_cau_hinh`.

- **Reachability:**
  - site `http://localhost:8080`; CCU `192.169.1.75:2000` đã xác nhận đẩy dữ liệu
    sau lệnh `LIVE` (đo 11:52 ngày 24/09: 91 gói/40 s, CRC sai 0).
  - script được mở socket riêng tới CCU: **đã đo CCU phục vụ 2 client song song**
    (client A 33 gói, client B 61 gói), nên phép đo không đá văng site.
  - `Get-NetTCPConnection -RemotePort 2000` chạy được trên chính máy chủ.
  - `POST /PgsStatus/TuKiem` từ loopback — theo đúng khuôn `PlcStatus/Reload` đã có.

- **Oracle:**
  - `deploy_khop_source` chạy TRƯỚC mọi phép khác; lệch dấu vân tay thì dừng ngay,
    không chạy tiếp, để không ai đọc kết quả của bản cũ.
  - script tự nối CCU bằng cài đặt giao thức độc lập với mã C#, đọc trọn một vòng
    5 ZCU (~2,5 s mỗi ZCU nên chờ tối thiểu 8 s), rồi so với endpoint. Lệch bất kỳ
    con số nào của nhóm `dang_ket_noi` là FAIL.
  - `khung_hong_bi_loai` nạp qua `POST /PgsStatus/TuKiem` bốn chuỗi: một khung
    `$CCU,02` hợp lệ, một khung cùng nội dung nhưng CRC sai một ký tự, một khung
    `$CCU,01,OK*50#`, và một khung `$CCU,02` cụt trường. Mã C# phải nhận đúng
    khung đầu và loại ba khung sau, mỗi loại vào đúng bộ đếm.

- **Counterexample:**
  - nếu lớp C# vẫn giải mã theo mẫu nền nhị phân cũ thì `co_xe` bằng 0 trong khi
    script đọc được 20 → `so_dang_ket_noi_khop_ccu` FAIL.
  - nếu mã C# bỏ qua CRC hoàn toàn thì khung CRC sai được nhận → `khung_hong_bi_loai`
    FAIL. Đây là lý do phép kiểm này phải chạy qua mã C#, không phải qua hàm CRC
    của chính script.
  - nếu chỉ để trống `pgs:hosts` mà không gỡ vòng đọc thì `PgsHost.Initialize`
    thoát sớm và không mở socket nào — `khong_con_socket_zcu` sẽ PASS giả. Nên
    phép kiểm này còn xét thêm: `TotalParking.csproj` không còn chuỗi `PgsFrame.cs`
    và `PgsConnection.cs`, và hai file đó không còn tồn tại.
  - nếu tuổi dữ liệu tính từ gói cuối thay vì từ lần cuối `X3 = 1` thì mọi ZCU
    đều có tuổi 0 → `nhom_dong_bang_tach_rieng` FAIL vì nhóm đóng băng thiếu tuổi.

- **Artifacts:**
  - `specs/pgs-doc-ccu/artifacts/ccu-goi.txt` — gói thô đọc được trong lần chạy.
  - `specs/pgs-doc-ccu/artifacts/tcp-2000.txt` — bảng kết nối TCP lúc chạy.
  - Cả hai ghi đè mỗi lần chạy; đối chiếu bằng cách đọc trực tiếp, không băm.

## Đối chiếu 15 phát hiện C2 đã áp vào đâu

| # | Phát hiện | Áp vào |
|---|---|---|
| F1 | `PgsHost.cs` chỉ được Read nhưng phải sửa | Ownership → Modify |
| F2 | Thiếu build + deploy | Verification Plan, AC-06, `deploy_khop_source` |
| F3 | Nhịp LIVE chết trong vòng nền tuần tự | Scope 1 — luồng riêng |
| F4 | Số gộp trộn sống với đóng băng | Scope 5, AC-01, AC-02 |
| F5 | Khung `$CCU,01,OK` chỉ 3 trường | Scope 2, AC-04 |
| F6 | Oracle không kiểm được CRC của mã C# | Scope 6, `khung_hong_bi_loai` |
| F7 | `DeviceProbeService` tự mở socket | Scope 8 |
| F8 | Tuổi dữ liệu luôn ≈ 0 | Scope 4, AC-02, AC-03 |
| F9 | `debounceMs` thành bộ đóng băng | Scope 10 |
| F10 | Timeout đọc bị coi là thiết bị chết | Scope 11 |
| F11 | `plan.md` thiếu csproj | Ownership + bảng Tasks |
| F12 | Chưa chốt tên khoá cấu hình | Mục "Khoá cấu hình chốt cứng" |
| F13 | Dashboard hiện "PGS 0/0" xanh | Scope 7 |
| F14 | Tách trường, CRC hoa/thường, 16 ZCU | Scope 3 |
| F15 | Ranh giới task lệch | Gộp còn một task |

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-ccu.mjs"`
Exit: 0
Base: e9fcc9ebef34b8e87dae8b712cefaf82f9435f5a
Head: 17ef5541c7e404655bea0697c29576eb50b7b746

```text
  PASS  deploy_khop_source  | 0de0e12e5441
  PASS  so_dang_ket_noi_khop_ccu  | api {trong:44 xe:31 loi:4 chuaLap:241} vs ccu {trong:44 xe:31 loi:4 chuaLap:241} (nguong qua han 10s)
  PASS  nhom_dong_bang_tach_rieng  | 0 zcu mat ket noi, dong_bang=[], dang_ket_noi=[0,1,2,3,4], tuoi rieng co, so gop da bo
  PASS  zcu_qua_han_bi_danh_dau  | nguong 10s, 5 zcu, qua han 0
  PASS  khung_hong_bi_loai  | nhan / loai:SaiCrc / loai:NhipSong / loai:ThieuTruong / nhan | X3=0 doc thanh dang_ket_noi=false, zcu_id=12
  PASS  khong_con_socket_zcu  | socket song toi .70-.74: 0, tong 0 -> 0 (khong tang), csproj da go, file da xoa
  PASS  mot_socket_ccu  | toi .75: 4 muc, Established 1

7/7 PASS
EXIT=0
```

### Bằng chứng bổ sung cho AC-02 và AC-03

Lệnh: `& "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-qua-han.mjs"` · Exit: 0

Cần lệnh thứ hai vì trong lần chạy trên, cả 5 ZCU đều `X3 = 1` và gói đều tươi
(`dong_bang=[]`, `qua han 0`), nên hai phép kiểm mang tên AC-02 và AC-03 chạy
trên tập rỗng — chúng in PASS mà chưa chứng minh gì.

```text
  PASS  nguong_da_ha  | nguong_qua_han_giay = 1
  PASS  zcu_bi_danh_dau_qua_han  | 1/5 zcu co tuoi > 1s, tat ca deu co co qua_han
  PASS  qua_han_roi_vao_nhom_dong_bang  | dang_ket_noi=[0,1,3,4] dong_bang=[2]
  PASS  khong_dem_trung  | trung=[], 4+1=5 vs 5 zcu
  PASS  so_hoc_tung_nhom_khop  | song co_xe=25/25, dong_bang co_xe=6/6, tong 31/31
  PASS  khoi_phuc_cau_hinh  | nguong ve 10s

6/6 PASS
EXIT=0
```

### Build và deploy

```text
MSBuild.exe TotalParking\TotalParking.sln /p:Configuration=Debug
  TotalParking -> C:\Users\Admin\source\repos\TotalParking\TotalParking\bin\TotalParking.dll
```

DLL đã chép sang `C:\Users\Admin\Documents\Web\totalParking\bin\`; `deploy_khop_source`
xác nhận dấu vân tay hai bên trùng nhau (`0de0e12e5441`). `Web.config` bản deploy
được sao lưu trước tại `Web.config.truoc-ccu`.

### Kết quả review độc lập

Reviewer ngữ cảnh mới trả `FAIL` với bốn việc phải làm trước khi đóng. Đã xử lý
toàn bộ:

| Phát hiện | Xử lý |
|---|---|
| H1 — sai lệch Scope 7 chưa ghi nhận | Ghi rõ vào mục Scope 7 kèm lý do vì sao cách cũ mâu thuẫn Scope 8 |
| M1 — đếm trùng ZCU ở mốc ngưỡng, trái AC-02 | Chốt mốc thời gian một lần trong `Index()`; `khong_dem_trung` chứng minh `4+1=5` |
| M2 — mọi `IOException` bị coi là khe rỗng | Chỉ `SocketError.TimedOut` mới bỏ qua, còn lại `Drop` ngay |
| M3 — dùng `_stream` sau khi `Drop` đặt null | Thêm `if (_stream == null) return;` |
| M4 — `KhongHieu` đếm rồi vứt | Nối lên tới endpoint, có ở cả hai nhóm và từng ZCU |
| M5 — AC-02/AC-03 chứng minh rỗng | Thêm `verify-qua-han.mjs`, kết quả ở trên |
| L2 — đọc rách `CcuTraLoiUtc` | Chép ra biến cục bộ trước khi dùng |
| L3 — `TuKiem` không giới hạn đầu vào | `Take(200)` |
| L6 — script không in dòng FAIL của `do_that` | Đổi sang gọi `ghi()` |

### Hạn chế còn lại

1. **L1 — đua giữa `Stop` và `Run`**: `_loop.Join(2000)` có thể hết giờ khi
   `Poll` đang chặn trong `Read`, để lại một socket không ai đóng cho tới khi
   AppDomain sập. Cửa sổ hẹp, chỉ lúc IIS recycle. Chưa sửa.
2. **L4 — chú thích ở `Global.asax.cs:33-37` đã sai** ("CHỈ ĐỌC — không gửi byte
   nào" và "lọc nhiễu"). File đó chỉ nằm trong quyền Read của task nên không sửa.
3. **Lỗ "0/0 màu xanh" chưa đóng hết**: khi `pgs:enabled = false` hoặc
   `pgs:ccuHost` rỗng thì `Health = "unmonitored"`, mà `Index.cshtml:1437-1440`
   chỉ ánh xạ `down`/`partial` nên rơi vào xanh. Lỗi có sẵn của view, ngoài
   quyền sở hữu của task.
4. **`KhongHieu` chưa từng khác 0** trên dữ liệu thật, nên đường cảnh báo đó mới
   được nối chứ chưa được chứng minh bằng dữ liệu hiện trường.
5. Số liệu **chưa nối vào bảng LED** — đúng quyết định C1, nằm trong Out of scope.
