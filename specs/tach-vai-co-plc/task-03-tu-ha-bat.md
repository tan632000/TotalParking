# Task 03 — Tự hạ và tự bật cổng vận hành theo kết nối

Status: done

## Outcome

Khối có PLC mất kết nối quá ngưỡng tự rời khỏi vận hành (`block.is_active = 0`),
nên thôi được xếp xe và thôi tính vào sức chứa. Khối có PLC kết nối lại ổn định
quá ngưỡng tự vào lại. Mọi lần đổi đều có nhật ký và có trần chống dao động.

## Vì sao task này tồn tại, và nó trái điều gì

Bản C1 nêu nguyên tắc **"cổng vận hành do người quyết"**, với lý do: dữ liệu ô đỗ
của một khối chưa sẵn sàng sẽ chảy vào sức chứa và thuật toán xếp xe.

Ở C2, người dùng chọn **tự động hạ và bật**. Tôi đã trình bày đánh đổi và người
dùng giữ quyết định. Task này thực hiện đúng quyết định đó, nhưng vì nó **tự
thay đổi sức chứa của một bãi đang phục vụ xe thật**, nó mang ba lớp an toàn mà
một tính năng thông thường không cần:

1. **Trễ bất đối xứng.** Hạ sau khi mất kết nối liên tục quá `plc:haSauPhut`
   (mặc định 15 phút). Bật lại sau khi kết nối liên tục quá `plc:batSauPhut`
   (mặc định 5 phút). Khối 89 đo được hôm 24/09 mất 3 giây rồi lên lại
   (`12/15` lần ping) — ngưỡng phút khiến loại chập chờn đó không chạm tới cờ.
2. **Trần số lần đổi mỗi ngày.** Vượt `plc:tranDoiCoMoiNgay` (mặc định 4) thì
   ngừng tự đổi khối đó và chờ người xử lý. Một khối dao động là dấu hiệu hỏng
   phần cứng, và để máy bật tắt sức chứa liên tục thì tài xế thấy số nhảy loạn.
3. **Chỉ đụng khối máy đã tự hạ.** Dịch vụ ghi nhớ khối nào do nó hạ
   (`tu_dong_ha_luc`); khối do người hạ tay thì **không** được tự bật lại. Nếu
   không, một khối bị người khoá để sửa chữa sẽ bị máy mở lại sau 5 phút.

Việc lấy xe ra **không** bị ảnh hưởng: `CarLocatorService` không lọc
`block.is_active`, và `BlockAllocator.cs:54` là chỗ duy nhất trong C# dùng cờ này.
Nên khối bị hạ vẫn trả xe bình thường, chỉ thôi nhận xe mới — đúng điều mong muốn.

## Scope

- **In:** cột `tu_dong_ha_luc` và `so_lan_doi_hom_nay` trên `block`; dịch vụ nền
  `CongVanHanhService` đọc `plc_device.is_connected` rồi hạ/bật `block.is_active`;
  nhật ký mỗi lần đổi; một view liệt kê khối đang lệch giữa hai tầng; endpoint
  phơi trạng thái để kiểm chứng.
- **Out:** không đổi `plc_device.is_active`; không đụng thuật toán xếp xe; không
  tự dọn thẻ rác; không gửi cảnh báo ra ngoài (email, tin nhắn).

## Coverage

- CP-03

## Ownership

- Create: `TotalParking/Database/43_tu_ha_bat.sql`
- Create: `TotalParking/Services/Plc/CongVanHanhService.cs`
- Create: `specs/tach-vai-co-plc/verify-tu-ha-bat.mjs`
- Modify: `TotalParking/Controllers/PlcStatusController.cs`
- Modify: `TotalParking/Services/Plc/PlcHost.cs`
- Read: `TotalParking/Services/BlockAllocator.cs`, `TotalParking/Services/LedPanelRepository.cs`

## Acceptance

- AC-05: PLC mất kết nối quá ngưỡng → `block.is_active` về `0`, có dòng nhật ký,
  và khối biến mất khỏi `v_zone_capacity`.
- AC-06: PLC đó kết nối lại ổn định quá ngưỡng → `block.is_active` về `1`.
- AC-07: khối vượt trần số lần đổi trong ngày → dịch vụ ngừng tự đổi khối đó, và
  điều này quan sát được qua endpoint.
- AC-09: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-02-is-connected.md

## Verification Plan

- **Thứ tự triển khai:** chạy `43_tu_ha_bat.sql` và xác nhận cột tồn tại TRƯỚC
  khi chép DLL, cùng lý do đã nêu ở task 02.

- **Rút ngắn ngưỡng khi đo:** script đặt tạm `plc:haSauPhut` và `plc:batSauPhut`
  xuống mức giây để quan sát được trong một lần chạy, rồi khôi phục. Không rút
  ngắn thì phép kiểm phải chờ 15 phút và sẽ bị bỏ qua trong thực tế.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-tu-ha-bat.mjs"`

- **Named probe:** tám phép kiểm có tên — `deploy_khop_source`,
  `bat_duoc_tinh_nang`, `mat_ket_noi_thi_tu_ha`, `khoi_bi_ha_roi_khoi_suc_chua`,
  `tran_doi_co_chan_dao_dong`, `khong_dung_khoi_nguoi_ha_tay`,
  `noi_lai_thi_tu_bat`, `khoi_phuc_nguyen_trang`.

  Nhiều hơn sáu phép dự kiến vì thêm `bat_duoc_tinh_nang` (xác nhận công tắc
  tổng thật sự có hiệu lực) và `khoi_phuc_nguyen_trang` (khôi phục thất bại phải
  là FAIL nhìn thấy được, không phải một dòng cảnh báo lẫn trong đầu ra).

- **Reachability:** CSDL truy vấn được; `POST /PlcStatus/Reload` chạy từ
  loopback; endpoint trạng thái cổng vận hành gọi được.

- **Oracle:**
  - Ép mất kết nối giống task 02: đổi `port` của đúng một dòng `plc_device` sang
    cổng không ai nghe, `Reload`, chờ quá ngưỡng đã rút ngắn.
  - `mat_ket_noi_thi_tu_ha`: `block.is_active` của khối đó về `0`, và
    `tu_dong_ha_luc` khác NULL.
  - `khoi_bi_ha_roi_khoi_suc_chua`: `v_zone_capacity.total_mech` của zone chứa
    khối giảm đúng bằng `slot_count` của nó.
  - `noi_lai_thi_tu_bat`: khôi phục `port`, `Reload`, chờ quá ngưỡng bật →
    `block.is_active` về `1` và `tu_dong_ha_luc` về NULL.
  - `tran_doi_co_chan_dao_dong`: lặp chu trình đổi port đủ số lần vượt trần →
    khối phải ngừng bị tự đổi, và endpoint phải nói rõ lý do.
  - `khong_dung_khoi_nguoi_ha_tay`: hạ `block.is_active = 0` cho một khối **mà
    không** qua dịch vụ (không đặt `tu_dong_ha_luc`), chờ quá ngưỡng bật →
    khối phải **vẫn** ở `0`. Đây là phép kiểm bảo vệ khối đang được người khoá
    để sửa chữa.
  - Chọn khối theo đúng tiêu chí "đang trống" như task 01, và script ghi
    `block_no` cùng `port` gốc ra artifact **trước** khi đổi bất cứ thứ gì.

- **Khôi phục:** script khôi phục `port`, `block.is_active`, `tu_dong_ha_luc` và
  hai khoá cấu hình ở khối `finally`, đồng thời bắt `SIGINT`/`SIGTERM`. Vì
  `try/finally` không chịu được kill cứng, Verification Plan ghi sẵn câu khôi
  phục thủ công để người vận hành chạy nếu script chết giữa chừng:

  ```sql
  UPDATE block SET is_active = 1, tu_dong_ha_luc = NULL WHERE is_active = 0;
  -- doi lai port theo artifacts/tu-ha-bat.json
  ```

- **Counterexample:**
  - nếu ngưỡng hạ bị đặt quá ngắn thì khối chập chờn sẽ bị hạ ngay →
    `tran_doi_co_chan_dao_dong` FAIL.
  - nếu dịch vụ bật lại mọi khối `is_active = 0` chứ không chỉ khối nó tự hạ →
    `khong_dung_khoi_nguoi_ha_tay` FAIL.
  - nếu hạ cờ mà sức chứa không đổi thì `block.is_active` không thật sự là cổng
    vận hành → `khoi_bi_ha_roi_khoi_suc_chua` FAIL.

- **Artifacts:** `specs/tach-vai-co-plc/artifacts/tu-ha-bat.json` — khối đã dùng,
  `port` gốc, hai khoá cấu hình gốc, và chuỗi trạng thái quan sát được theo thời
  gian; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-tu-ha-bat.mjs"`
Exit: 0
Base: e9fcc9ebef34b8e87dae8b712cefaf82f9435f5a
Head: b796898ce7a11bd5ad7dc75df436816ba7aa6d93

```text
  PASS  deploy_khop_source  | 801ad5e219ef
  (khoi thu: A=2 ep mat ket noi, B=3 dat kich tran, C=4 nguoi ha tay)
  PASS  bat_duoc_tinh_nang  | bat_tinh_nang=true, ha_sau_phut=1
  (cho 150s de dich vu ha...)
  PASS  mat_ket_noi_thi_tu_ha  | block 2: is_active=0, tu_dong_ha_luc=co
  PASS  khoi_bi_ha_roi_khoi_suc_chua  | zone 4: total_mech 122 -> 117, tong slot_count khoi con bat = 117 (khop), khoi 2 con duoc tinh: 0
  PASS  tran_doi_co_chan_dao_dong  | block 3 da kich tran (so_lan_doi=99): is_active=1 (phai van la 1)
  PASS  khong_dung_khoi_nguoi_ha_tay  | block 4 nguoi ha tay: is_active=0 (phai van la 0), tu_dong_ha_luc=trong
  (cho 150s de dich vu bat lai...)
  PASS  noi_lai_thi_tu_bat  | block 2: is_active=1, tu_dong_ha_luc=trong
  PASS  khoi_phuc_nguyen_trang  | block tat: 0 -> 0 (khop), tinh nang da tat lai

8/8 PASS
EXIT=0
```

### Nguyên trạng sau phép thử

```text
block_tat  con_dau_tu_dong  port_la
        0                0        0
plc:tuDongCongVanHanh = "false"
```

Phép thử bật tính năng, rút ngưỡng xuống 1 phút, ép ba tình huống trên hệ thống
đang chạy — và không để lại thay đổi nào.

### Cả bốn lớp an toàn đều được chứng minh bằng phép thử thật

| Lớp | Phép kiểm | Kết quả |
|---|---|---|
| Công tắc tổng mặc định tắt | `bat_duoc_tinh_nang` | phải bật tường minh mới chạy |
| Trễ theo ngưỡng | `mat_ket_noi_thi_tu_ha`, `noi_lai_thi_tu_bat` | hạ rồi bật lại đúng chiều |
| Trần số lần đổi | `tran_doi_co_chan_dao_dong` | khối kịch trần **không** bị đổi |
| Chỉ đụng khối máy tự hạ | `khong_dung_khoi_nguoi_ha_tay` | khối người khoá **không** bị mở lại |

Lớp thứ tư là lớp quan trọng nhất về an toàn con người: nếu thiếu, một khối bị
khoá để thợ làm việc sẽ bị máy mở lại sau vài phút và hệ thống xếp xe vào đó.

### Ba lỗi trong phép thử của tôi, đã sửa

Không lỗi nào nằm ở mã; cả ba đều là phép thử đo sai thứ mình tuyên bố:

1. **Đọc cột rỗng thành `undefined`.** `out.trim()` nuốt tab cuối dòng, nên
   `COALESCE(x,'')` trả về `undefined` chứ không phải `""`. Hai phép kiểm FAIL
   trong khi kết quả thực tế đúng. Sửa: để SQL trả nhãn `'co'`/`'trong'`.
2. **Giả định "chỉ khối A bị hạ" là sai.** Bật tính năng làm dịch vụ hạ **mọi**
   khối có PLC chết quá ngưỡng — zone 4 giảm 30 ô chứ không phải 5. Sửa phép
   kiểm sang thứ thật sự cần chứng minh: `total_mech` của zone bằng đúng tổng
   `slot_count` của các khối còn bật, và khối A không còn trong tập đó.
3. **Script không chạy lại được.** Mỗi lần chạy làm khối A đổi cờ hai lần, nên
   đến lần thứ ba nó chạm trần 4 và dịch vụ từ chối đổi tiếp — phép kiểm FAIL
   với lý do "không tự hạ" trong khi mã hoàn toàn đúng. Đó chính là lớp an toàn
   thứ ba đang làm việc. Sửa: script tự đặt lại bộ đếm cho khối thử.

### Hạn chế còn lại

1. **Tính năng đang TẮT.** Packet chỉ chứng minh nó chạy đúng khi bật; chưa ai
   bật nó trong vận hành thật. Bật bằng `plc:tuDongCongVanHanh = true`.
2. **Bật lên sẽ hạ ngay 9 khối** đang có PLC chết quá 15 phút — đó là hành vi
   đúng, nhưng sức chứa trên bảng LED sẽ giảm thấy rõ ngay lúc bật. Nên bật vào
   lúc có người theo dõi.
3. **Ngưỡng nhỏ nhất là 1 phút** vì truy vấn dùng `INTERVAL ... MINUTE`. Phép
   kiểm phải chờ 150 giây mỗi chiều, nên chạy trọn bộ mất khoảng 6 phút.
4. **Chưa quan sát được khối dao động thật.** Trần 4 lần/ngày được chứng minh
   bằng cách đặt thẳng bộ đếm, không phải bằng một PLC chập chờn thật.
5. `v_zone_coverage` vẫn không lọc cờ nào — độ phủ tiếp tục đếm cả khối đã tắt
   vận hành. Đã ghi trong Out of scope.
