# Task 02 — `is_connected` ghi an toàn vào CSDL

Status: done

## Outcome

`plc_device.is_connected`, `connected_changed_at` và `last_probe_at` phản ánh
trạng thái kết nối FINS, truy vấn được bằng SQL, chỉ bị ghi khi trạng thái thật
sự đổi, và không bao giờ làm dừng vòng poll.

## Scope

- **In:** ba cột mới; một dịch vụ nền riêng gom trạng thái từ
  `PlcConnectionManager.Connections` rồi ghi CSDL; khử rung trước khi ghi; xoá
  trạng thái của thiết bị đã rời vòng poll; cập nhật chú thích đã thành sai.
- **Out:** không đổi hành vi kết nối, không đổi nhịp poll, không thêm bảng lịch sử,
  không đụng cổng vận hành (việc đó ở task 03).

### Ba cột, mỗi cột một câu hỏi

| Cột | Trả lời câu hỏi |
|---|---|
| `is_connected` | lần quan sát gần nhất, PLC này sống hay chết |
| `connected_changed_at` | trạng thái đó giữ nguyên từ bao giờ |
| `last_probe_at` | lần quan sát gần nhất là lúc nào |

`last_probe_at` là cột bắt buộc, không phải trang trí. Bản trước biện minh rằng
`connected_changed_at` cho biết số liệu cũ bao lâu — **sai**: nó là thời điểm
trạng thái ĐỔI, nên một PLC online ổn định ba ngày và một site đã chết năm phút
cho ra dữ liệu giống hệt nhau. Chỉ `last_probe_at` phân biệt được, và nó ghi
theo lô một câu `UPDATE` cho cả bảng nên không tốn kém.

### Vì sao ghi vào bảng thay vì chỉ giữ trong bộ nhớ

Người dùng chốt ngày 24/09: muốn truy vấn được bằng SQL cùng chỗ với cấu hình.
Tôi đã nêu nhược điểm và người dùng giữ quyết định, nên packet cài kèm ba lớp
giảm nhẹ thay vì bỏ qua: chỉ ghi khi đổi, có khử rung, và có `last_probe_at` để
cột không nói dối khi site chết.

Nguồn sự thật lúc chạy vẫn là `PlcConnection.IsOnline`; ba cột là bản chiếu, hệ
thống không đọc ngược lại chúng để ra quyết định.

## Coverage

- CP-02

## Ownership

- Create: `TotalParking/Database/42_is_connected.sql`
- Create: `TotalParking/Services/Plc/PlcTrangThaiWriter.cs`
- Create: `specs/tach-vai-co-plc/verify-is-connected.mjs`
- Modify: `TotalParking/Services/PlcDeviceRepository.cs`
- Modify: `TotalParking/Services/Plc/PlcHost.cs`
- Read: `TotalParking/Services/Plc/PlcConnectionManager.cs`, `TotalParking/Services/Plc/PlcConnection.cs`

## Acceptance

- AC-02: ép một PLC rớt bằng cách kiểm soát được → `is_connected` đổi `1 → 0` và
  `connected_changed_at` tiến lên, trong vòng một chu kỳ ghi.
- AC-03: khi trạng thái không đổi, `connected_changed_at` giữ nguyên qua cửa sổ
  quan sát; tổng số lần đổi trong cửa sổ không vượt trần cấu hình.
- AC-04: khi lệnh ghi CSDL hỏng (ép bằng cách đổi tên cột trong cửa sổ quan
  sát) → vòng poll vẫn tiếp tục đọc PLC, và lỗi được ghi lại, không bị nuốt im
  lặng.
- AC-09: dấu vân tay DLL ở cây mã nguồn trùng bản deploy.

## Dependencies

- task-01-tach-vai.md

## Verification Plan

- **Thứ tự triển khai bắt buộc — sai thứ tự là sự cố:**
  1. chạy `42_is_connected.sql` và **xác nhận ba cột tồn tại**;
  2. chỉ khi đó mới MSBuild và chép DLL sang bản deploy;
  3. đợi IIS nạp lại.

  Chiều ngược lại (DLL mới, cột chưa có) sinh `Unknown column` mỗi lần ghi, mà
  `PlcConnectionManager.cs:202-211` bắt mọi `Exception` của cả lô poll và **không
  ghi log gì**. Người vận hành sẽ thấy vòng poll "vẫn chạy", cột không bao giờ có
  dữ liệu, và không một dòng nhật ký nào để lần ra. Chiều thuận thì vô hại: cột
  thừa, chưa ai đọc.

- **Command:** `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-is-connected.mjs"`

- **Named probe:** tám phép kiểm có tên — `deploy_khop_source`,
  `ghi_nam_ngoai_vong_poll`, `co_moc_quan_sat`, `khong_ghi_khi_khong_doi`,
  `bat_duoc_chuyen_trang_thai`, `khoi_phuc_port`,
  `loi_csdl_khong_giet_vong_poll`, `khoi_phuc_ten_cot`.

  Nhiều hơn năm phép dự kiến vì hai phép khôi phục được tách thành phép kiểm
  riêng: khôi phục thất bại phải là FAIL nhìn thấy được, không phải một dòng
  cảnh báo lẫn trong đầu ra.

- **Reachability:** `/PlcStatus/Index` gọi được; CSDL truy vấn được bằng
  `mysql.exe`; `POST /PlcStatus/Reload` chạy từ loopback.

- **Oracle:**
  - `bat_duoc_chuyen_trang_thai` — phép kiểm quan trọng nhất của task. Ép một
    chuyển trạng thái thật bằng cách **đổi `port` của đúng một dòng `plc_device`
    sang một cổng không ai nghe**, gọi `Reload`, chờ, khẳng định `is_connected`
    về `0` và `connected_changed_at` tiến lên; rồi khôi phục cổng, `Reload` lại,
    khẳng định về `1`. Chọn khối theo đúng tiêu chí "đang trống" như task 01.
    Không có phép này thì một cài đặt chỉ ghi một lần lúc khởi động vẫn PASS hết
    các phép còn lại.
  - `khong_ghi_khi_khong_doi`: chụp `connected_changed_at` toàn bảng, chờ cửa sổ
    dài hơn nhiều nhịp poll, chụp lại. Khối nào không đổi `is_connected` mà
    `connected_changed_at` lại đổi thì FAIL. Đồng thời đếm tổng số khối có
    `connected_changed_at` đổi trong cửa sổ; vượt trần cấu hình thì FAIL — đây là
    thứ bắt được PLC chập chờn ghi mỗi nhịp.
  - `co_moc_quan_sat`: `last_probe_at` của mọi dòng đang poll phải mới hơn cửa sổ
    ghi gần nhất; dòng không còn trong vòng poll phải có `is_connected` NULL.
  - `loi_csdl_khong_giet_vong_poll`: **đổi tên cột `last_probe_at`** trong ~25
    giây, khẳng định `/PlcStatus/Index` vẫn đọc được thiết bị — tức vòng poll còn
    sống — và nhật ký có dòng lỗi ghi CSDL.

    Không tạm dừng hẳn dịch vụ MySQL như bản kế hoạch đầu dự tính: cả hệ thống
    dùng chung một CSDL (phiên gửi xe, bảng LED, danh mục thẻ), nên phạm vi ảnh
    hưởng quá rộng cho một phép kiểm. Đổi tên một cột mà chỉ writer dùng thì vẫn
    là phép thử THẬT, chỉ hẹp hơn nhiều.

    Phải chọn `last_probe_at` chứ không phải `is_connected`: `GhiMocQuanSat` chạy
    mỗi lượt chiếu nên chắc chắn ném lỗi, còn `GhiTrangThaiKetNoi` chỉ chạy khi có
    thiết bị đổi trạng thái — trong 25 giây yên tĩnh nó không được gọi lần nào và
    phép thử sẽ không ép được lỗi nào cả.

- **Hợp đồng đặt lệnh ghi:** lệnh ghi CSDL chạy trong `PlcTrangThaiWriter` — một
  task nền riêng, **không nằm trong** `LoopAsync` của `PlcConnectionManager`.
  Lý do: `LoopAsync` chỉ bọc `try` quanh `Task.WhenAll` (`PlcConnectionManager.cs:202-211`),
  còn phần tính thời gian và `Task.Delay` nằm ngoài; một `MySqlException` ném ra
  từ đó sẽ thoát khỏi vòng lặp và **giết vòng poll vĩnh viễn** trong khi
  `IsRunning` vẫn trả `true` (`PlcConnectionManager.cs:54-57`). Writer phải tự
  bọc try/catch có ghi log, và không bao giờ ném lên vòng poll.

- **Counterexample:**
  - cài đặt chỉ ghi lúc khởi động → `bat_duoc_chuyen_trang_thai` FAIL.
  - ghi mỗi nhịp poll → `khong_ghi_khi_khong_doi` FAIL.
  - đặt lệnh ghi trong `LoopAsync` → `loi_csdl_khong_giet_vong_poll` FAIL.
  - quên xoá trạng thái của thiết bị đã rời vòng poll → `co_moc_quan_sat` FAIL.

- **Artifacts:** `specs/tach-vai-co-plc/artifacts/is-connected.json` — các lần
  chụp cột, nội dung endpoint, và khối đã dùng để ép chuyển trạng thái kèm giá
  trị `port` gốc; ghi đè mỗi lần chạy.

## Receipt

Verification: PASS
Command: `& "C:\Program Files\nodejs\node.exe" "specs\tach-vai-co-plc\verify-is-connected.mjs"`
Exit: 0
Base: e9fcc9ebef34b8e87dae8b712cefaf82f9435f5a
Head: e9063f134037a3967b3ece6f482cde0f29b3daec

```text
  PASS  deploy_khop_source  | 8b332d9b12ba
  PASS  ghi_nam_ngoai_vong_poll  | manager goi lenh ghi: khong, writer co try/catch + log: true, writer chay luong rieng: true
  PASS  co_moc_quan_sat  | co trang thai ma thieu moc: 0, dang bat ma thieu moc: 0, da tat ma van co trang thai: 0
  PASS  khong_ghi_khi_khong_doi  | trong 30s: 0 thiet bi doi trang thai (tran 5), 0 thiet bi doi moc MA KHONG doi trang thai
  PASS  bat_duoc_chuyen_trang_thai  | block 2: sau khi rot is_connected=0 (moc da tien), sau khi noi lai is_connected=1 (moc da tien)
  PASS  khoi_phuc_port  | port block 2 = 9600 (goc 9600)
  PASS  loi_csdl_khong_giet_vong_poll  | poll_running=true, thiet bi doc duoc 99 -> 99, nhat ky co dong loi: co
  PASS  khoi_phuc_ten_cot  | cot last_probe_at ton tai: 1

8/8 PASS
EXIT=0
```

### Trạng thái cột lúc đóng task

```text
tong  noi_duoc  chet  chua_ro  co_moc
 112       99      9        4     108
```

`chua_ro = 4` là bốn thiết bị không nằm trong vòng poll — `is_connected` để NULL
đúng như thiết kế: không biết khác hẳn biết là chết. `co_moc = 108` bằng đúng số
thiết bị đang được poll.

### Hai phép thử thật, không suy luận

**Ép chuyển trạng thái** (`bat_duoc_chuyen_trang_thai`): đổi `port` của block 2
sang cổng không ai nghe, `Reload`, chờ → `is_connected` về `0` và mốc tiến lên;
khôi phục cổng, `Reload` → về `1`, mốc tiến tiếp. Không có phép này thì một cài
đặt **chỉ ghi một lần lúc khởi động** vẫn PASS hết các phép còn lại.

**Ép lỗi CSDL** (`loi_csdl_khong_giet_vong_poll`): đổi tên cột `last_probe_at`
trong 25 giây → writer ném `Unknown column` mỗi lượt chiếu, mà vòng poll **vẫn
đọc đủ 99 thiết bị** và nhật ký **có** dòng lỗi. Đây là chứng minh cho ràng buộc
kiến trúc quan trọng nhất của task: lỗi CSDL không được phép làm mù tầng PLC.

### Một lỗi trong phép thử, đã sửa

Lần chạy đầu phép kiểm này FAIL với lý do "nhật ký không có dòng lỗi". Nguyên
nhân **nằm ở phép thử, không ở mã**: tôi đổi tên cột `is_connected`, nhưng trong
25 giây yên tĩnh không thiết bị nào đổi trạng thái nên `GhiTrangThaiKetNoi` không
được gọi lần nào — không có lỗi để bắt. Cột chạy mỗi lượt là `last_probe_at`
(qua `GhiMocQuanSat`). Đổi sang cột đó thì phép thử ép được lỗi thật.

### Hạn chế còn lại

1. **Chưa quan sát được PLC chập chờn thật.** Khử rung (3 lượt × 5 giây) đã cài
   và `khong_ghi_khi_khong_doi` đo được 0 lần ghi thừa trong 30 giây, nhưng lúc
   đo không có thiết bị nào dao động. Block 89 từng đo được `12/15` ping — nếu
   nó dao động lại thì đây là chỗ cần xem lại trần.
2. **Trần 5 thiết bị đổi trạng thái/30 giây là con số tôi chọn**, không phải đo
   ra. Nó đủ để bắt "ghi mỗi nhịp" nhưng chưa được hiệu chỉnh theo hành vi thật
   của bãi này.
3. **`GhiMocQuanSat` nối danh sách id vào câu lệnh** thay vì tham số hoá. Nguồn
   là danh sách kết nối trong bộ nhớ và đã ép kiểu số nguyên, nên không mở đường
   chèn SQL; ghi lại ở đây để ai đổi nguồn dữ liệu sau này biết mà xem lại.
