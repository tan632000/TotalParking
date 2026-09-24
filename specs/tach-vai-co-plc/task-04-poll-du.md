# Task 04 — Đưa mọi PLC đã khai báo vào vòng poll

Status: done

## Outcome

Mọi PLC có trong `plc_device` đều nằm trong vòng poll, và số khối trong vòng poll
bằng đúng số dòng `plc_device.is_active = 1`. Không còn PLC nào được khai báo mà
không ai giám sát.

## Vì sao task này tách riêng và đứng cuối

Đây là task **duy nhất thay đổi tập thiết bị mà SCADA thực sự kết nối tới**. Ba
task trước chỉ đổi mã và cấu trúc bảng; task này đổi thứ đang chạy ngoài hiện
trường. Tách riêng để khi có sự cố còn biết do mã hay do dữ liệu — revert DLL
không tắt lại PLC đã bật, nên rollback của hai loại thay đổi này không đối xứng.

### Tiền đề bắt buộc — không phải thủ tục

**Bật một khối vào vòng poll là GHI xuống PLC của nó.**
`PlcConnection.cs:67` khởi tạo `_lastBandWritten = -1`, nên nhịp poll đầu tiên
của mỗi kết nối mới ghi `D1004` một lần (`PlcConnection.cs:558-564`). Đo trên
nhật ký thật: 2285/3083 dòng `WRITE D1004` là ghi `0` khi `D106` rỗng. Nếu
`D1002` còn rác thì nhịp đầu còn ghi `D1000` rồi xoá `D1002`
(`PlcConnection.cs:450`, `:489-492`).

Vì vậy **trước khi chạy migration, phải có người xác nhận không có thợ đang làm
việc trên các khối sắp bật**. Người dùng đã chấp nhận đánh đổi này ở C2 với đúng
điều kiện đó.

Ghi chú thêm, chưa xử lý trong packet: `Reload()` không gọi
`ClearStaleAnswersAsync` (`PlcConnectionManager.cs:100-157` so với
`PlcHost.cs:91`), nên khối vừa vào poll giữ nguyên `D1000` rác cho tới lượt quẹt
đầu tiên. Tài xế tìm xe đầu tiên ở khối đó có thể đọc phải số của đợt test.

## Scope

- **In:** migration bật `plc_device.is_active = 1` cho mọi dòng đang `0`; gọi
  `POST /PlcStatus/Reload`; kiểm số khối trong vòng poll khớp CSDL.
- **Out:** không đụng `block.is_active`; không sửa mã; không dọn `D1000`/`D1002`.

## Coverage

- CP-04

## Ownership

- Create: `TotalParking/Database/44_poll_du.sql`
- Create: `specs/tach-vai-co-plc/verify-poll-du.mjs`
- Read: `TotalParking/Services/Plc/PlcConnectionManager.cs`, `TotalParking/Controllers/PlcStatusController.cs`

## Acceptance

- AC-08: sau migration và `Reload`, số khối trong `/PlcStatus/Index` bằng đúng
  `SELECT COUNT(*) FROM plc_device WHERE is_active = 1`.
- AC-09: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-03-tu-ha-bat.md

## Verification Plan

- **Tiền đề người thực hiện phải xác nhận trước khi chạy:** không có thợ đang
  làm việc trên các khối sắp được bật. Ghi lại xác nhận đó vào Receipt.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-poll-du.mjs"`

- **Named probe:** bốn phép kiểm có tên — `deploy_khop_source`,
  `khong_con_plc_bi_bo_quen`, `poll_khop_csdl`, `khong_mat_ket_noi_hang_loat`.

- **Reachability:** CSDL truy vấn được; `POST /PlcStatus/Reload` chạy từ loopback;
  `/PlcStatus/Index` gọi được.

- **Oracle:**
  - `khong_con_plc_bi_bo_quen`: `SELECT COUNT(*) FROM plc_device WHERE is_active = 0`
    bằng 0.
  - `poll_khop_csdl`: số phần tử `blocks` trong `/PlcStatus/Index` bằng đúng số
    dòng `is_active = 1`. Phép kiểm này bắt đúng tình trạng đo được lúc 17:56
    ngày 24/09: CSDL 107 mà vòng poll chỉ giữ 87, vì có người `UPDATE` mà chưa
    gọi `Reload`.
  - `khong_mat_ket_noi_hang_loat`: sau `Reload`, chờ quá một chu kỳ poll rồi đếm
    số khối `online`. Số này phải **không thấp hơn** mốc đo trước migration. Mở
    thêm kết nối FINS có thể đụng giới hạn khe của PLC (`0x20`); nếu số online
    tụt thì migration đã gây hại và phải dừng lại.

- **Counterexample:**
  - nếu chỉ chạy `UPDATE` mà quên `Reload` thì `poll_khop_csdl` FAIL — đây chính
    là lỗi đang tồn tại trên hệ thống lúc viết packet.
  - nếu việc bật thêm PLC làm các khối khác rớt vì hết khe kết nối thì
    `khong_mat_ket_noi_hang_loat` FAIL.

- **Rollback:** artifact ghi danh sách `block_no` được bật trong lần chạy. Câu
  khôi phục:

  ```sql
  -- doi lai theo danh sach trong artifacts/poll-du.json
  UPDATE plc_device d JOIN block b ON b.block_id = d.block_id
     SET d.is_active = 0 WHERE b.block_no IN (...);
  ```
  rồi gọi lại `POST /PlcStatus/Reload`.

- **Artifacts:** `specs/tach-vai-co-plc/artifacts/poll-du.json` — danh sách khối
  được bật, số online trước và sau, và xác nhận hiện trường của người thực hiện;
  ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-poll-du.mjs"`
Exit: 0
Base: e9fcc9ebef34b8e87dae8b712cefaf82f9435f5a
Head: dcf794cebe2749e32aae68064dd9e791c9df8ee8

```text
  PASS  deploy_khop_source  | 801ad5e219ef
  (moc truoc: 101 online / 108 trong poll, se bat them 4 khoi: 14 20 28 57)
  PASS  khong_con_plc_bi_bo_quen  | so PLC is_active = 0: 0
  PASS  poll_khop_csdl  | vong poll 112 vs CSDL 112
  PASS  khong_mat_ket_noi_hang_loat  | online 101 -> 105
  (khoi vua bat: 14=online, 20=online, 28=online, 57=online)

4/4 PASS
EXIT=0
```

### Xác nhận hiện trường

Tiền đề bắt buộc của task này là có người xác nhận không có thợ đang làm việc
trên các khối sắp bật, vì bật poll là ghi `D1004 = 0` xuống PLC.

Người dùng xác nhận ngày 24/09: *"Sẵn sàng, không có thợ đang làm — bật đi"*,
trong bối cảnh đang chuẩn bị luồng test để bàn giao. Xác nhận này cũng được ghi
vào `artifacts/poll-du.json`.

### Kết quả

```text
trong_poll  bo_quen  khoi_tat  khoi_lech
       112        0         0          7
```

Cả 4 khối `14 20 28 57` lên `online` ngay sau `Reload`, và tổng số khối đọc được
**tăng** từ 101 lên 105 — không khối nào rớt vì hết khe kết nối. Đây là rủi ro
chính của task (mở thêm kết nối FINS có thể đụng giới hạn khe và gây lỗi `0x20`
cho khối khác), và nó đã không xảy ra.

`khoi_lech = 7` là bảy khối có PLC chết mà vẫn đang nhận xe — nhìn thấy được qua
view `v_plc_lech_tang`. Trước packet này không có cách nào thấy chúng ngoài việc
quét mạng bằng tay.

### Hạn chế còn lại

1. **`Reload()` không dọn `D1000` rác.** Khối vừa vào vòng poll giữ nguyên giá
   trị cũ ở `D1000` cho tới lượt quẹt thẻ đầu tiên
   (`PlcConnectionManager.cs:100-157` so với `PlcHost.cs:91` — chỉ đường khởi
   động mới gọi `ClearStaleAnswersAsync`). Tài xế tìm xe đầu tiên ở một trong
   bốn khối vừa bật có thể đọc phải số block của đợt test trước. Đã ghi trong
   Scope là ngoài phạm vi; nên xử lý ở một packet riêng.
2. **Không quan sát được `D1004` đã ghi gì.** Việc ghi xảy ra ở nhịp poll đầu và
   không có phép kiểm nào trong script này đọc lại giá trị đó — chỉ biết qua
   nhật ký `plc_audit.log`.
3. **Bốn khối vừa bật chưa từng chạy luồng thật.** Chúng online, nhưng chưa ai
   quẹt thẻ trên chúng để xác nhận toàn bộ luồng hoạt động.
