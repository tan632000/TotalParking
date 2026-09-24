# Task 02 — Bảng LED đầu hầm hiện số chỗ đỗ thường thật

Status: done

## Outcome

Bảng LED đầu hầm (`192.169.1.50`, cổng `TOTAL`) hiện số chỗ đỗ thường lấy từ
cảm biến PGS thay vì con số tĩnh của CSDL. 21 cổng `ZONES` còn lại không đổi.

## Vì sao task này nằm ngoài C1 ban đầu

C1 chốt KEEP và đưa bảng LED vào Out of scope, với lý do "lấy số đúng trước đã,
đối chiếu thực địa vài ngày rồi mới cho điều khiển LED thật". Sau khi task 01
xong, người dùng xem số liệu, xác nhận đúng thực tế, rồi chỉ đạo trực tiếp:
*"triển khai việc 1 trước"* — tức nối riêng cổng `TOTAL`.

Đây là mở rộng phạm vi **theo quyết định của người dùng**, không qua vòng C1/C2
riêng. Ghi lại ở đây thay vì sửa lịch sử C1, để sau này đọc packet không tưởng
rằng bảng LED vẫn đang nằm ngoài phạm vi.

Hai lựa chọn hành vi do người dùng chốt ngày 24/09:

| Quyết định | Chọn | Lý do |
|---|---|---|
| Cách tính số trống | chỉ đếm cảm biến báo trống | ô lỗi và ô thiếu cảm biến không tính là trống; tài xế đi theo bảng thì chắc có chỗ |
| Khi mất kết nối | giữ số cuối đọc được | bãi đỗ thường biến động chậm; quay về số CSDL là quay lại nói dối |

## Scope

- **In:** cổng `TOTAL` lấy `FreeStandard` từ cảm biến; `TotalStandard` giữ theo
  CSDL vì đó là sức chứa thật của bãi (80 ô) chứ không phải số cảm biến đã lắp (79).
- **Out:** 21 cổng `ZONES` — chưa biết cảm biến nào thuộc zone nào (5 ZCU cho 6
  zone; số cảm biến mỗi ZCU `21/17/18/16/7` không khớp số ô mỗi zone
  `13/18/2/17/11/19`). Đoán rồi đẩy lên mũi tên chỉ hướng là chỉ sai đường.

## Coverage

- CP-06

## Ownership

- Create: `TotalParking/Services/Led/StandardFreeSource.cs`
- Create: `specs/pgs-doc-ccu/verify-led-total.mjs`
- Modify: `TotalParking/Services/LedPanelRepository.cs`
- Modify: `TotalParking/Services/Led/LedFrameBuilder.cs`
- Modify: `TotalParking/TotalParking.csproj`
- Read: `TotalParking/Services/Led/LedPublisher.cs`, `TotalParking/Controllers/LedStatusController.cs`

## Acceptance

- AC-07: `LedStatus/Index → capacity.free_standard` bằng tổng số cảm biến báo
  trống, và khác `total_standard` khi có xe đang đỗ.
- AC-08: `total_standard` giữ nguyên 80 theo CSDL.
- AC-09: đường tính theo zone không gọi tới nguồn cảm biến — toàn repo chỉ có
  đúng một chỗ gọi, nằm trong `GetCapacity()`.

## Dependencies

- task-01-doc-ccu.md

## Verification Plan

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-led-total.mjs"`
- **Named probe:** sáu phép kiểm — `deploy_khop_source`, `so_toan_bai_theo_cam_bien`,
  `khong_con_la_so_tinh_cua_csdl`, `total_van_theo_csdl`, `cong_zones_khong_doi`,
  `so_trong_hop_ly`.
- **Reachability:** site `http://localhost:8080`; `LedStatus/Index` và
  `PgsStatus/Index` đều gọi được; bảng `192.169.1.50` đang online và trả ACK.
- **Oracle:** `free_standard` ở endpoint LED phải bằng tổng `khong_co_xe` của
  mọi ZCU ở endpoint PGS, đọc trong cùng một lần chạy.
- **Counterexample:**
  - nếu vẫn lấy số CSDL thì `free_standard` bằng `total_standard` (80) →
    `khong_con_la_so_tinh_cua_csdl` FAIL.
  - nếu số cảm biến rò rỉ sang đường zone thì `cong_zones_khong_doi` FAIL. Phép
    này kiểm **tĩnh trên mã nguồn**, không qua HTTP, vì `/LedStatus/Index` không
    phơi `free_standard` theo từng zone — so qua endpoint chỉ cho `0 === 0`,
    PASS mà không chứng minh gì.
- **Artifacts:** `specs/pgs-doc-ccu/artifacts/led-total.json` — số của cả hai
  endpoint lúc chạy; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\pgs-doc-ccu\verify-led-total.mjs"`
Exit: 0
Base: e9fcc9ebef34b8e87dae8b712cefaf82f9435f5a
Head: 22107444bb0bfdf53f1145f2e06181a543e5589b

```text
  PASS  deploy_khop_source  | b558033d2984
  PASS  so_toan_bai_theo_cam_bien  | LED free_standard=47 vs tong cam bien bao trong=47
  PASS  khong_con_la_so_tinh_cua_csdl  | free=47, total=80, cam bien thay 28 xe dang do
  PASS  total_van_theo_csdl  | total_standard=80
  PASS  cong_zones_khong_doi  | GetCapacityByZone khong goi cam bien, SumZones khong goi, toan repo goi 1 lan (dung trong GetCapacity)
  PASS  so_trong_hop_ly  | 0 <= 47 <= 80

6/6 PASS
EXIT=0
```

### Bảng vật lý đã nhận số mới

Đọc `LedStatus/Index`, mục bảng `50`:

```text
  code         50
  name         Bang led dau ham
  online       True
  last_frame   $PORT,0,0.2.0,751.2,751.2,47.2*28#
  last_ack     $PORT,0,OK*2D#
  ports        [{'index': 0, 'scope': 'TOTAL', 'zone_list': None, 'active': True, 'skipped': False}]
```

Trường thứ ba của khung — `47.2` — là số chỗ đỗ thường kèm mã màu xanh. Bảng
trả `OK`, nghĩa là nó đã nhận và hiển thị. Trước thay đổi, chỗ này là `80.2`.

### Hạn chế còn lại

1. **21 cổng `ZONES` vẫn hiện số cũ của CSDL.** Đây là phần lớn bảng trong bãi.
   Cần ánh xạ ZCU → zone, chỉ có được bằng phép thử vật lý (đỗ xe vào ô đã biết
   của từng zone rồi xem ZCU nào đổi số).
2. **Ô lỗi và ô thiếu cảm biến không được tính là trống** theo quyết định của
   người dùng, nên số trên bảng thấp hơn sức chứa thật vài ô. Hiện là 4 ô lỗi
   và 1 ô không có cảm biến.
3. **Chưa kiểm được hành vi khi CCU mất kết nối hoàn toàn.** Lúc đo, CCU online
   và cả 5 ZCU đều gửi dữ liệu. Đường "giữ số cuối cùng" đã cài (`SoOTrong()`
   trả `null` khi chưa có dữ liệu, và khi đó `FreeStandard` giữ số CSDL) nhưng
   nhánh đó chưa chạy trên dữ liệu thật.
4. **Người dùng chưa xác nhận tại chỗ.** Bằng chứng ở đây là khung lệnh và ACK
   từ bảng, chưa phải mắt người nhìn vào mặt bảng đầu hầm.
