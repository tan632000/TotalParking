# Prompt phân tích và triển khai hệ thống Parking SCADA – Omron PLC

Bạn hãy đóng vai **Senior Solution Architect + PLC Integration Engineer + Backend Engineer**, hỗ trợ tôi phân tích và triển khai hệ thống **quản lý bãi đỗ xe tự động (Parking Management System)** có tích hợp **Web SCADA, Camera, RFID và PLC Omron**.

## 1. Bối cảnh hệ thống

Tôi đang xây dựng hệ thống quản lý bãi đỗ xe gồm:

- 1 hệ thống Web SCADA trung tâm.
- 6 Zone.
- 100 Block.
- Mỗi Block tương ứng với:
  - 1 tủ điện.
  - 1 HMI.
  - 1 PLC Omron.
- Mỗi PLC có địa chỉ IP riêng.
- Các PLC giao tiếp với hệ thống SCADA thông qua mạng LAN.
- Hệ thống SCADA có Database để lưu trạng thái xe, RFID, Zone, Block, Pallet và lịch sử gửi/lấy xe.

Kiến trúc logic:

```text
Camera
   |
   v
Parking SCADA
   |
   +---- Database
   |
   +---- PLC Communication Layer
   |
   +---- Block 1 PLC
   +---- Block 2 PLC
   +---- Block 3 PLC
   ...
   +---- Block 100 PLC
```

## 2. Luồng xe vào bãi

Hệ thống Camera hoạt động 24/7 để nhận diện xe khi xe đi vào bãi.

Khi Camera phát hiện một xe vào bãi:

```text
Camera
   ↓
SCADA nhận thông tin xe
   ↓
SCADA phân tích thông tin
   ↓
Xác định Zone phù hợp
   ↓
Xác định Block phù hợp
   ↓
Điều hướng xe đến Block
```

SCADA cần quản lý trạng thái của xe xuyên suốt quá trình:

```text
Xe vào bãi
→ Được phân Zone
→ Được phân Block
→ Được cấp/nhận RFID
→ Chọn pallet
→ Gửi xe thành công
→ Xe đang được lưu trong hệ thống
→ Khách tìm xe
→ Điều hướng đến Block
→ Lấy xe
→ Hoàn tất parking session
```

## 3. Cấu trúc Block và Pallet

Mỗi Zone có nhiều Block.

Mỗi Block có nhiều pallet để xe đỗ.

Ví dụ:

```text
Zone 1
 ├── Block 1
 │    ├── Pallet 1
 │    ├── Pallet 2
 │    ├── Pallet 3
 │    └── ...
 │
 ├── Block 2
 │    ├── Pallet 1
 │    ├── Pallet 2
 │    └── ...
 │
 └── ...
```

Khách hàng có thể lựa chọn pallet để đỗ xe.

## 4. Phân loại RFID theo tải trọng

RFID được phân loại theo loại xe/tải trọng:

### Loại 2200kg

- Có thể sử dụng pallet phía trên.
- Có thể sử dụng pallet phía dưới.

### Loại 2600kg

- Chỉ được phép sử dụng pallet phía dưới.

### Loại quá khổ

- Chỉ được phép sử dụng pallet phía dưới hoặc vị trí phù hợp với quy định hệ thống.

SCADA phải kiểm tra loại RFID trước khi cho phép chọn pallet.

Không được để xe thuộc loại 2600kg hoặc quá khổ vào vị trí phía trên.

## 5. Vấn đề quan trọng với HMI và PLC

Hiện tại HMI của từng Block **không tự phân biệt được trạng thái xe đang gửi ở Block nào**.

Ví dụ:

```text
Xe A
↓
Được SCADA điều hướng đến Block 1
↓
Khách quẹt RFID tại HMI Block 1
↓
Gửi xe thành công
```

Database/SCADA biết:

```text
RFID = ABC123
Vehicle = Xe A
Zone = Zone 1
Block = Block 1
Pallet = P01
Status = PARKED
```

Nhưng nếu sau đó khách đi sang Block 2 và tiếp tục quẹt RFID:

```text
RFID ABC123
↓
HMI Block 2
↓
Nhấn gửi xe
```

thì PLC/HMI hiện tại có thể vẫn cho phép thực hiện thao tác.

Điều này gây ra lỗi:

```text
Một RFID
→ Gửi xe ở Block 1
→ Nhưng tiếp tục có thể gửi xe ở Block 2
```

Trong khi yêu cầu nghiệp vụ phải là:

> **Một xe/RFID đã gửi xe thành công thì không được phép gửi xe lần thứ hai ở bất kỳ Block nào khác.**

## 6. Yêu cầu điều hướng mới

### Trường hợp 1 – Xe chưa gửi

Nếu RFID chưa có parking session đang ACTIVE:

```text
RFID
 ↓
SCADA kiểm tra Database
 ↓
Không tìm thấy xe đang PARKED
 ↓
Cho phép gửi xe
 ↓
Xác định Zone
 ↓
Xác định Block
 ↓
Xác định pallet hợp lệ
 ↓
Thực hiện gửi xe
```

### Trường hợp 2 – Xe đã gửi

Nếu RFID đã có trạng thái:

```text
PARKED
```

thì khi quẹt RFID tại **bất kỳ HMI nào**:

```text
RFID
 ↓
HMI Block bất kỳ
 ↓
PLC gửi thông tin RFID về SCADA
 ↓
SCADA kiểm tra Database
 ↓
RFID đã PARKED
 ↓
Không cho phép gửi xe mới
 ↓
Trả về thông tin xe đang ở:
    Zone X
    Block Y
    Pallet Z
```

HMI phải hiển thị dạng:

```text
Xe đã được gửi.

Vị trí xe:
Zone: 1
Block: 12
Pallet: P03

Vui lòng đến Block 12 để thực hiện thao tác tiếp theo.
```

Điều quan trọng:

**Không được chỉ dựa vào PLC/HMI của từng Block để quyết định xe đã gửi hay chưa.**

Trạng thái nghiệp vụ phải được quản lý tập trung bởi SCADA/Database.

## 7. Giao tiếp với PLC Omron

PLC sử dụng:

```text
Omron
```

Mỗi PLC có IP riêng.

Ví dụ:

```text
Block 1  → 192.168.1.101
Block 2  → 192.168.1.102
Block 3  → 192.168.1.103
...
Block 100 → 192.168.1.200
```

Đây chỉ là ví dụ minh họa, **không được giả định đây là IP thật**.

PLC có thanh ghi:

```text
D100
```

D100 là thanh ghi quan trọng để SCADA đọc dữ liệu từ PLC.

Khi người dùng quẹt RFID và thực hiện gửi xe, hệ thống cần đọc dữ liệu từ D100 để xác định/nhận biết dữ liệu mà PLC/HMI gửi lên.

Tôi muốn xây dựng một **PLC Connection Layer** để quản lý việc giao tiếp với 100 PLC.

Hãy phân tích cách thiết kế:

```text
SCADA
 ↓
PLC Connection Manager
 ↓
PLC Connection
 ↓
Omron PLC
 ↓
D100
```

Không được tạo 100 đoạn code kết nối PLC riêng biệt.

Hãy thiết kế theo mô hình:

```text
PLC Device Configuration
        ↓
PLC Connection Manager
        ↓
PLC Driver / Omron FINS Communication
        ↓
Read / Write Register
```

Ví dụ cấu hình nên có:

```text
BlockId
ZoneId
PLC IP
Port
PLC Node
Memory Area
Register
Connection Status
Last Communication Time
```

## 8. Phân tích D100

Hãy giải thích rõ:

1. D100 trong PLC Omron là gì.
2. SCADA cần đọc D100 như thế nào.
3. D100 đang chứa dữ liệu gì thì cần xác định dựa trên chương trình PLC thực tế, không được tự giả định.
4. Nếu D100 chứa RFID hoặc mã sự kiện thì nên encode/decode như thế nào.
5. Nếu D100 chỉ là một Word/Integer thì cần xác định cơ chế truyền dữ liệu nhiều byte như thế nào.
6. Cần xử lý trường hợp:
   - PLC mất kết nối.
   - D100 không thay đổi.
   - D100 thay đổi liên tục.
   - D100 có dữ liệu cũ.
   - SCADA đọc trùng dữ liệu.
   - PLC restart.
   - SCADA restart.
7. Đề xuất cơ chế handshake giữa SCADA và PLC nếu cần.

Nếu chưa đủ thông tin để xác định chính xác format của D100, hãy **chỉ rõ những thông tin tôi cần cung cấp**, không được tự bịa.

## 9. Thiết kế cơ chế chống gửi xe trùng

Đây là phần rất quan trọng.

Hãy thiết kế cơ chế đảm bảo:

```text
1 RFID
=
1 active parking session
```

Ví dụ:

```text
RFID ABC123

Lần 1:
Block 1
→ PARKED
→ Thành công

Lần 2:
Block 2
→ SCADA kiểm tra
→ RFID ABC123 đã PARKED
→ TỪ CHỐI

Lần 3:
Block 3
→ SCADA kiểm tra
→ RFID ABC123 đã PARKED
→ TỪ CHỐI
```

Không được để logic:

```text
Block 1 PLC tự quyết định
Block 2 PLC tự quyết định
Block 3 PLC tự quyết định
```

mà phải có:

```text
HMI
 ↓
PLC
 ↓
SCADA
 ↓
Parking Session State
 ↓
Database
```

## 10. Transaction / Concurrency

Hãy đặc biệt phân tích race condition.

Ví dụ:

```text
09:00:00
RFID ABC123 quẹt ở Block 1

09:00:00
RFID ABC123 đồng thời quẹt ở Block 2
```

Nếu hai request cùng kiểm tra:

```sql
SELECT ... WHERE RFID = ABC123 AND status = PARKED
```

thì cả hai có thể đều nhận:

```text
Không tồn tại
```

và cùng tạo parking session.

Hãy đề xuất cách chống vấn đề này bằng:

- Database transaction.
- Unique constraint/index.
- Row locking hoặc cơ chế tương đương.
- Idempotency.
- State machine.

Mục tiêu:

```text
Không bao giờ có 2 active parking session
cho cùng một RFID.
```

## 11. State Machine

Hãy thiết kế state machine cho Parking Session.

Ví dụ:

```text
NEW
 ↓
ASSIGNED
 ↓
ENTERING
 ↓
PARKING
 ↓
PARKED
 ↓
RETRIEVING
 ↓
COMPLETED
```

và các trạng thái lỗi:

```text
CANCELLED
ERROR
TIMEOUT
```

Hãy xác định:

- Trạng thái nào được phép chuyển sang trạng thái nào.
- Ai chịu trách nhiệm thay đổi trạng thái:
  - Camera
  - SCADA
  - HMI
  - PLC
  - Backend
- Khi nào Database được update.
- Khi nào PLC được update.
- Khi nào HMI được update.

## 12. Kiến trúc phần mềm đề xuất

Hãy đề xuất kiến trúc tổng thể:

```text
Camera Layer
     ↓
Parking Business Logic
     ↓
Parking Session Service
     ↓
PLC Communication Layer
     ↓
Omron PLC
```

và:

```text
                    ┌── PLC Block 1
                    ├── PLC Block 2
SCADA Backend ──────┼── PLC Block 3
                    ├── ...
                    └── PLC Block 100
```

Tôi muốn hệ thống có thể:

- Theo dõi trạng thái kết nối 100 PLC.
- Tự reconnect khi mất kết nối.
- Timeout.
- Retry.
- Logging.
- Monitoring.
- Không làm một PLC bị lỗi khiến toàn bộ hệ thống SCADA bị block.
- Có thể đọc/ghi nhiều PLC song song.
- Có giới hạn connection/thread phù hợp.
- Có queue/event nếu cần.

## 13. Database

Hãy đề xuất Database Schema phục vụ bài toán.

Ít nhất cần phân tích các nhóm:

```text
zones
blocks
pallets
plcs
rfid_cards
vehicles
parking_sessions
parking_events
```

Có thể bổ sung các bảng khác nếu thực sự cần.

Tôi muốn Database quản lý được:

```text
RFID
Vehicle
Zone
Block
Pallet
PLC
Parking Session
Parking Status
Camera Event
PLC Event
Created At
Updated At
```

Hãy chỉ ra:

- Primary Key.
- Foreign Key.
- Unique Index.
- Index phục vụ truy vấn.
- Constraint để chống duplicate parking session.
- Quan hệ giữa các bảng.

## 14. API / Backend

Hãy đề xuất API cho hệ thống.

Ví dụ:

```text
POST /api/parking/rfid/scan
POST /api/parking/assign
POST /api/parking/park
POST /api/parking/retrieve
GET  /api/parking/rfid/{rfid}
GET  /api/parking/vehicle/{vehicle}
GET  /api/blocks/{blockId}/status
GET  /api/plcs/{plcId}/status
```

Không bắt buộc phải sử dụng đúng các API trên.

Hãy thiết kế API phù hợp với nghiệp vụ thực tế.

Đặc biệt:

```text
POST RFID scan
```

phải trả về được:

### Nếu RFID chưa gửi:

```json
{
  "status": "ALLOW_PARKING",
  "zone": 1,
  "block": 12,
  "pallet": "P03"
}
```

### Nếu RFID đã gửi:

```json
{
  "status": "ALREADY_PARKED",
  "zone": 1,
  "block": 12,
  "pallet": "P03"
}
```

### Nếu RFID không hợp lệ:

```json
{
  "status": "INVALID_RFID"
}
```

## 15. Phân tích các tình huống lỗi

Bắt buộc phân tích:

### Case A

Xe chưa gửi → quẹt Block 1 → thành công.

### Case B

Xe đã gửi Block 1 → quẹt Block 2 → phải từ chối.

### Case C

Xe đã gửi Block 1 → quẹt lại Block 1 → phải trả về vị trí xe.

### Case D

Hai HMI cùng quẹt một RFID gần như đồng thời.

### Case E

SCADA mất kết nối Database.

### Case F

SCADA mất kết nối PLC.

### Case G

PLC mất kết nối trong lúc gửi xe.

### Case H

PLC restart.

### Case I

SCADA restart.

### Case J

D100 chứa dữ liệu cũ.

### Case K

D100 thay đổi nhưng SCADA đọc trùng event.

### Case L

Camera nhận diện cùng một xe nhiều lần.

### Case M

Khách quẹt nhầm RFID.

### Case N

RFID đã PARKED nhưng khách không nhớ Block.

Khi đó HMI của bất kỳ Block nào cũng phải có khả năng trả về:

```text
Zone
Block
Pallet
```

để điều hướng khách đến đúng vị trí.

## 16. Yêu cầu về code

Sau khi hoàn thành phân tích kiến trúc, hãy đề xuất cấu trúc source code.

Tôi muốn code có khả năng mở rộng từ:

```text
1 PLC
```

lên:

```text
100 PLC
```

mà không phải copy/paste code.

Hãy ưu tiên:

- Clean Architecture.
- SOLID.
- Dependency Injection.
- Repository/Service nếu phù hợp.
- Interface cho PLC driver.
- Configuration-driven PLC connection.
- Async/background worker nếu cần.
- Logging.
- Retry policy.
- Timeout.
- Circuit breaker nếu phù hợp.
- Event-driven architecture nếu thực sự cần.

## 17. Không được tự giả định

Nếu có thông tin chưa đủ, hãy đánh dấu:

```text
[NEED INFORMATION]
```

và liệt kê chính xác tôi cần cung cấp gì.

Đặc biệt không được tự giả định:

- PLC model.
- FINS node.
- IP thật.
- Port thật.
- D100 data format.
- Số lượng pallet mỗi Block.
- Cấu trúc bộ nhớ PLC.
- Mapping giữa D100 và RFID.
- Protocol Camera.
- Protocol HMI.

Nếu cần thông tin PLC để triển khai chính xác, hãy yêu cầu tôi cung cấp:

```text
PLC Model
PLC Network Configuration
FINS Configuration
HMI Model
PLC Program / Ladder
Memory Map
D100 specification
Sample D100 values
Read/Write requirement
```

## 18. Output tôi muốn nhận

Hãy thực hiện theo thứ tự:

### Phase 1 – Phân tích nghiệp vụ

Mô tả đầy đủ flow:

```text
Camera
→ SCADA
→ Zone
→ Block
→ RFID
→ HMI
→ PLC
→ D100
→ Database
→ Parking Session
```

### Phase 2 – Phân tích vấn đề hiện tại

Chỉ ra:

- Vấn đề HMI không phân biệt Block.
- Vấn đề duplicate parking.
- Vấn đề race condition.
- Vấn đề PLC state vs SCADA state.
- Vấn đề mất kết nối.
- Vấn đề đồng bộ dữ liệu.

### Phase 3 – Kiến trúc hệ thống

Đưa ra sơ đồ architecture dạng text/ASCII.

### Phase 4 – Parking State Machine

Đưa ra state machine và transition rules.

### Phase 5 – PLC Communication

Thiết kế:

```text
PLC Manager
PLC Connection
Omron Driver
Read D100
Write Register
Retry
Timeout
Reconnect
Handshake
```

### Phase 6 – Database

Thiết kế ERD logic và SQL migration/schema.

### Phase 7 – API

Thiết kế API request/response.

### Phase 8 – Code Structure

Đề xuất cấu trúc source code.

### Phase 9 – Code

Sau khi architecture được xác định, bắt đầu viết code theo từng module.

**Không viết toàn bộ code một lần.**

Hãy chia thành từng module:

```text
1. PLC Connection Manager
2. Omron FINS Driver
3. D100 Reader
4. Parking Session Service
5. RFID Scan Service
6. Parking Assignment Service
7. Block/Pallet Service
8. Database
9. API
10. Logging & Monitoring
```

Mỗi module phải có:

- Mục đích.
- Input.
- Output.
- Interface.
- Class.
- Flow.
- Code.
- Error handling.
- Test case.

### Phase 10 – Test Scenario

Cuối cùng hãy tạo test case cho toàn bộ flow, đặc biệt tập trung vào:

```text
Một RFID
→ Không thể có
→ 2 parking session ACTIVE
```

và:

```text
RFID đã PARKED tại Block A
→ Quẹt HMI tại Block B
→ Block B không được phép gửi xe
→ HMI Block B phải hiển thị vị trí Block A
```

## Mục tiêu cuối cùng

Tôi muốn xây dựng một hệ thống trong đó:

```text
                    CAMERA
                       ↓
                 ┌──────────┐
                 │  SCADA   │
                 └────┬─────┘
                      ↓
             Parking Session
                      ↓
                 DATABASE
                      ↓
          ┌───────────┴───────────┐
          ↓                       ↓
      Block 1                  Block 100
          ↓                       ↓
       HMI + PLC               HMI + PLC
          ↓                       ↓
        D100                    D100
```

Và quy tắc nghiệp vụ cốt lõi là:

> **SCADA/Database là nguồn sự thật trung tâm (Single Source of Truth) về trạng thái gửi xe. PLC/HMI của từng Block chỉ thực hiện thao tác tại Block và giao tiếp trạng thái về SCADA.**

Nếu RFID đã có một parking session ACTIVE/PARKED thì **bất kỳ HMI/Block nào cũng phải nhận biết được RFID đó đã được gửi và chỉ được phép hiển thị/điều hướng về đúng Zone → Block → Pallet, tuyệt đối không tạo parking session thứ hai.**
