# Task 01 — Truy vấn poll thôi phụ thuộc cờ vận hành

Status: done

## Outcome

Truy vấn nạp danh sách thiết bị chỉ còn lọc theo `plc_device.is_active`. Hạ
`block.is_active = 0` không còn làm PLC rời vòng poll — hai cờ tách hẳn vai.

## Scope

- **In:** gỡ `AND b.is_active = 1` khỏi `SelectSql` trong `PlcDeviceRepository`.
- **Out:** không đổi dữ liệu, không bật thêm PLC nào (việc đó ở task 04), không
  đụng `BlockAllocator`, không đụng view nào.

## Coverage

- CP-01

## Ownership

- Modify: `TotalParking/Services/PlcDeviceRepository.cs`
- Create: `specs/tach-vai-co-plc/verify-tach-vai.mjs`
- Read: `TotalParking/Services/BlockAllocator.cs`, `TotalParking/Services/Plc/PlcConnectionManager.cs`

## Acceptance

- AC-01: `PlcDeviceRepository.cs` không còn chuỗi `b.is_active`; và trong một
  transaction hạ cờ thử, truy vấn mới giữ khối trong khi truy vấn cũ loại nó.
- AC-09: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- none

## Verification Plan

- **Build và deploy trước khi đo** (app phục vụ từ
  `C:\Users\Admin\Documents\Web\totalParking`, không phải cây mã nguồn):
  1. `& "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" TotalParking\TotalParking.sln /p:Configuration=Debug`
  2. chép `TotalParking\bin\*.dll` sang bản deploy
  3. đợi IIS nạp lại

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-tach-vai.mjs"`

- **Named probe:** bốn phép kiểm có tên — `deploy_khop_source`,
  `ma_khong_con_loc_co_van_hanh`, `transaction_phan_biet_hai_truy_van`,
  `khong_de_lai_co_nao_bi_ha`.

- **Reachability:** CSDL truy vấn được bằng `mysql.exe`; site chạy ở
  `http://localhost:8080` để kiểm dấu vân tay DLL.

- **Oracle:**
  - `ma_khong_con_loc_co_van_hanh`: đọc `PlcDeviceRepository.cs`, khẳng định
    `SelectSql` và nhánh `activeOnly` không còn chuỗi `b.is_active`. Phép kiểm
    tĩnh này là **bắt buộc**, không phải bổ sung: hiện không có block nào
    `is_active = 0` nên hai truy vấn trả cùng số dòng, và một phép đo runtime
    đơn thuần sẽ PASS kể cả khi chưa sửa gì.
  - `transaction_phan_biet_hai_truy_van`: trong **một transaction rollback**,
    chọn một khối đang trống, hạ `block.is_active = 0`, rồi chạy cả hai dạng
    truy vấn. Truy vấn mới phải giữ khối, truy vấn cũ phải loại. Hai số phải
    **khác nhau** — bằng nhau nghĩa là phép thử không phân biệt được gì.
  - Chọn khối trống thuần bằng CSDL, không đọc thanh ghi: không có dòng
    `plc_slot_state` nào còn `card_code`, không có `parking_session` đang mở, và
    không có `vehicle_routing` nào `outcome = ROUTED` trong 90 giây gần nhất
    (`BlockAllocator.cs:47-50`, cửa sổ ở `BlockAllocator.cs:28`).
  - `khong_de_lai_co_nao_bi_ha`: sau `ROLLBACK`, `SELECT SUM(is_active = 0) FROM block`
    phải bằng 0.

- **Vì sao dùng transaction thay vì hạ cờ thật:**
  - Hạ cờ thật rồi gọi `Reload` sẽ đóng phiên FINS của khối đó
    (`PlcConnectionManager.cs:138-150`), và mở lại có thể vướng lỗi hết khe
    `0x20` với thời gian chờ 5 phút (`PlcConnection.cs:52`), có tiền lệ kẹt hàng
    giờ. Một phép kiểm không được phép làm rớt PLC thật.
  - Transaction không lộ giá trị `0` ra ngoài, nên `LedPublisher` và tài xế
    không bao giờ thấy số sai.
  - `ROLLBACK` là nguyên tử, không phụ thuộc script còn sống hay bị kill — khác
    hẳn `try/finally` của Node, vốn không bắt được `SIGTERM` hay mất điện.

- **Counterexample:**
  - nếu quên sửa mã thì `ma_khong_con_loc_co_van_hanh` FAIL ngay, không cần chạy
    tiếp.
  - nếu gỡ nhầm cả `p.is_active = 1` thì trong transaction hai truy vấn sẽ cho
    số bằng nhau ở phía trên (không phân biệt) → `transaction_phan_biet_hai_truy_van`
    FAIL.

- **Artifacts:** `specs/tach-vai-co-plc/artifacts/tach-vai.json` — hai số đếm
  trong transaction và trạng thái sau rollback; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-tach-vai.mjs"`
Exit: 0
Base: e9fcc9ebef34b8e87dae8b712cefaf82f9435f5a
Head: 9b39e13ce88db0a39c4e7a3515017008c6a9abfa

```text
  PASS  deploy_khop_source  | c46527045307
  PASS  ma_khong_con_loc_co_van_hanh  | loc b.is_active: da go, loc p.is_active: con
  PASS  transaction_phan_biet_hai_truy_van  | block thu 2: truy van moi giu 108, truy van cu con 107
  PASS  khong_de_lai_co_nao_bi_ha  | so block is_active = 0: 0

4/4 PASS
EXIT=0
```

### Phép kiểm phân biệt được, không PASS rỗng

`transaction_phan_biet_hai_truy_van` cho **108 so với 107** — hai truy vấn cho
kết quả khác nhau, nên phép kiểm thật sự đo được điều nó tuyên bố. Đây là chỗ
bản packet đầu sai: khi không có block nào `is_active = 0`, hai truy vấn trả
cùng số dòng và phép đo runtime đơn thuần sẽ PASS kể cả khi chưa sửa gì.

Sau `ROLLBACK`, `SUM(is_active = 0)` bằng 0 — không để lại dấu vết nào trên hệ
thống đang chạy, và bảng LED không bao giờ thấy giá trị tạm.

### Blast radius đã xác nhận

Ba nơi gọi `GetAll`, kiểm tại thời điểm đóng task:

| Nơi gọi | Ảnh hưởng |
|---|---|
| `PlcConnectionManager.cs:68` (`Load`), `:102` (`Reload`) | đích nhắm của thay đổi |
| `DeviceProbeService.cs:156` | sẽ thăm dò thêm PLC của khối đã tắt vận hành — socket ngắn, không phải FINS |
| `PlcReachabilityScanner.cs:135` | dùng `GetAll(false)`, không đi qua nhánh `activeOnly`, không bị ảnh hưởng |

Không nơi nào dựa vào ngữ nghĩa cũ "khối tắt thì PLC ngừng poll".

### Hạn chế còn lại

1. **Chưa quan sát được hiệu lực thật.** Hiện không có block nào `is_active = 0`,
   nên thay đổi này chưa đổi hành vi nào ngoài hiện trường. Nó chỉ có tác dụng
   khi task 03 bắt đầu tự hạ cờ, hoặc khi có người hạ tay.
2. **`v_zone_coverage` vẫn không lọc cờ nào**, nên độ phủ tiếp tục đếm cả khối đã
   tắt vận hành — đã ghi trong Out of scope.
3. Phép kiểm chọn khối trống bằng CSDL (`plc_slot_state`, `parking_session`,
   `vehicle_routing`), không đọc thanh ghi PLC. Nếu ba nguồn đó lệch thực tế thì
   khối được chọn có thể không thật sự trống — nhưng trong transaction thì điều
   đó cũng không gây hại vì không có gì lộ ra ngoài.
