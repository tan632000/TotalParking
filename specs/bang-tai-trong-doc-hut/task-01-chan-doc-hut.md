# Task 01 — `D1004` không nhận băng tính từ giá trị `D106` nửa vời

Status: done

## Outcome

`UpdateWeightBandAsync()` bỏ qua vòng poll khi giá trị `D106` đọc được ở trạng
thái nửa vời, nên `D1004` không còn nhận băng tính từ một mã thẻ sai. Lượt quẹt
bình thường vẫn ghi băng đúng như trước, và nhật ký vẫn ghi lại được cả giá trị
nửa vời để còn quan sát được lỗi này về sau.

## Scope

- **In:** một điều kiện thoát sớm trong `UpdateWeightBandAsync()` — nếu **đúng
  một** trong hai word bằng 0 còn word kia khác 0 thì `return`, không tính băng,
  không ghi `D1004`. Giá trị `D1004` cũ giữ nguyên (quyết định C2).
- **Vị trí chèn — bắt buộc:** **sau** lệnh `PlcRegisterLog.Track` tại
  `TotalParking/Services/Plc/PlcConnection.cs:539-543`, **trước** nhánh tính băng
  tại `:554`. Chèn sớm hơn (ngay sau `ReadWordsAsync`) làm mất dòng nhật ký
  `D106` cho đúng trường hợp cần quan sát nhất — vi phạm AC-03.
- **Out:** không thêm trường trạng thái nào (bản vá **không có trạng thái**, nên
  không phát sinh điểm reset); không đụng `_lastSeenRaw` — trường đó thuộc luồng
  tìm xe, ghi đè nó sẽ phá chốt chống lặp tại
  `TotalParking/Services/Plc/PlcConnection.cs:452` và làm `D1000` bị ghi lại mỗi
  nhịp poll trên mọi block; không đổi `_lastBandWritten`; không đụng
  `WeightBandService`, luồng tìm xe, hay luồng gửi xe.

## Coverage

- CP-01, CP-02, CP-03

## Ownership

- Modify: `TotalParking/Services/Plc/PlcConnection.cs`
- Create: `tools/kiem_chung_doc_hut.py`
- Read: `TotalParking/Services/Plc/CardCodeDecoder.cs`,
  `TotalParking/Services/WeightBandService.cs`, `tools/plc_register.py`,
  `TotalParking/Services/Plc/PlcConnectionManager.cs`

## Acceptance

- **AC-01:** Sau khi `D1004` đang ở 1, đặt `D106 = 0x0000` / `D107 = 0xA0F3` và
  giữ hơn hai nhịp poll → `D1004` **vẫn là 1**, không chuyển sang 3.
- **AC-02:** Đặt `D106 = 0x7660` / `D107 = 0xA0F3` (thẻ `a0f37660`, 2200kg) và
  giữ hơn hai nhịp poll → `D1004` chuyển sang **1**.
- **AC-03:** Sau bước nửa vời, `plc_register_changes.log` có dòng `D106` ghi giá
  trị `0000 A0F3` cho block thử.

## Dependencies

- none

## Verification Plan

- **Command:** `python tools/kiem_chung_doc_hut.py --block <N>`

- **Tiền đề, kiểm trước khi chạy (probe tự kiểm, thoát khác 0 nếu hỏng):**
  1. Đã build bằng MSBuild VS2019 và **triển khai sang**
     `C:\Users\Admin\Documents\Web\totalParking` — thư mục repo không phải bản
     đang chạy.
  2. Khối `blocks` của `GET http://localhost:8080/PlcStatus` có block `<N>` với
     `online == true`, và `poll_running == true`.
  3. Không thẻ hợp lệ nào có nửa word bằng 0 (câu SQL ở `plan.md` mục Giới hạn 3
     trả về rỗng).
  4. Block `<N>` đang rảnh: không có dòng `D106` nào cho block đó trong
     `plc_register_changes.log` trong 60 giây gần nhất.

- **Named probe:** `tools/kiem_chung_doc_hut.py`. Nhịp poll **không** lấy từ
  `poll_ms` của block — `TotalParking/Services/Plc/PlcConnectionManager.cs:75,153`
  cho thấy nhịp thật là `Math.Max(100, devices.Min(d => d.PollMs))`, tức nhỏ
  nhất trên toàn bộ thiết bị đang bật. Probe **đo tại chỗ** bằng khoảng cách hai
  dòng `D106` liên tiếp trong nhật ký (đã đo ≈ 512 ms), rồi chờ `3 ×` giá trị đó.

  | Bước | Ghi `D106`/`D107` | `D1004` mong đợi | Phủ |
  |---|---|---|---|
  | 1 | `0x7660` / `0xA0F3` — thẻ đủ | **1** | AC-02 |
  | 2 | `0x0000` / `0xA0F3` — nửa vời | **vẫn 1**, không phải 3 | AC-01 |
  | 3 | — (đọc nhật ký) | có dòng `D106 -> 0000 A0F3` | AC-03 |
  | 4 | `0x0000` / `0x0000` — dọn | **0** | dọn dẹp |

  Thứ tự này dựng lại **đúng** chuỗi hỏng đã quan sát (`D1004 1 -> 3` ở cả 5 lần
  trong `plan.md`), nên bước 2 là phép thử thật chứ không phải kiểm tra hình thức.

- **Reachability:** đã biết. Vòng poll gọi `UpdateWeightBandAsync()` mỗi nhịp tại
  `TotalParking/Services/Plc/PlcConnection.cs:387`; đã xác nhận
  `poll_running = True` qua `GET http://localhost:8080/PlcStatus`.

- **Điều kiện hợp lệ của phép thử (bắt buộc):** packet cố ý đi ngược cảnh báo ở
  `tools/plc_register.py:38-40` — công cụ ghi rõ không nên chạy vào block đang
  nằm trong vòng poll vì hai kết nối FINS có thể làm lệch khung tin của nhau. Ở
  đây **không tránh được**: phải có vòng poll chạy thì `D1004` mới được ghi.
  Bù lại bằng điều kiện hợp lệ: kết quả mỗi bước **chỉ được tính** nếu
  `C:\Users\Admin\Documents\Web\totalParking\App_Data\plc_audit.log` **không** có
  dòng lỗi FINS nào cho IP của block thử trong cửa sổ chạy. Có lỗi → huỷ lượt
  đo, chạy lại; không được diễn giải kết quả.

- **Oracle:** giá trị `D1004` đọc thẳng từ PLC sau mỗi bước, khớp cột "mong đợi".

- **Counterexample:** ba chiều sai đều làm phép thử trượt.
  - Thiếu chốt chặn → bước 2 cho `D1004 = 3` (đúng lỗi đã quan sát).
  - Chốt chặn quá tay, chặn cả mã thẻ hợp lệ → bước 1 cho `D1004 = 0`.
  - Chốt chặn đặt sai vị trí, trước `PlcRegisterLog.Track` → bước 3 không tìm
    thấy dòng nhật ký.

- **Artifacts:** `specs/bang-tai-trong-doc-hut/artifacts/kiem-chung.txt` — toàn
  bộ đầu ra của lệnh, kèm trích đoạn
  `C:\Users\Admin\Documents\Web\totalParking\App_Data\plc_register_changes.log`
  và `plc_audit.log` trong cửa sổ chạy. So sánh bằng đọc trực tiếp.

### Điều kiện an toàn trước khi chạy

`D106` **không** thuộc vùng ô đỗ (`D200-D211`, `D300-D311`, `D400-D411` —
`tools/plc_register.py:70`) nên không có nguy cơ ladder xếp chồng xe. Nhưng ghi
vào `D106` là **giả lập một lượt quẹt**, nên phải chọn block đang rảnh và báo
trước bên vận hành. Ghi `D106` cần cờ `--force` (`tools/plc_register.py:60,353`).
Bước 4 phải chạy kể cả khi các bước trước trượt.

## Receipt

- Command: `python tools/kiem_chung_doc_hut.py --block 20`
- Exit: 0
- Verification: PASS
- Base: `1a1bd1f19088fac0943579dccdd41e11bd7976f0`
- Head: `1a1bd1f19088fac0943579dccdd41e11bd7976f0`
- **Chạy lúc:** 2026-09-25 14:59:34, block 20 (`192.169.1.120`)
- **Tiền đề đã đạt:** DLL triển khai mới hơn bản build; `poll_running = true`;
  block 20 `online = true`; không lượt quẹt nào 60 giây trước.
- **Điều kiện hợp lệ:** không dòng lỗi FINS nào cho `192.169.1.120` trong cửa sổ
  chạy → lượt đo hợp lệ (xử lý rủi ro nêu ở `tools/plc_register.py:38-40`).
- **Artifacts:** `specs/bang-tai-trong-doc-hut/artifacts/kiem-chung.txt`

```
=== KIEM CHUNG CHAN DOC HUT D106 -> D1004 ===
Block 20 | 2026-09-25 14:59:34

[Tien de]
    Tat ca dat: da trien khai, poll dang chay, block online va rang

[Buoc 1] AC-02 — the day du phai duoc ghi bang binh thuong
    D106 = 7660 A0F3 (a0f37660, 2200kg)            D1004 = 1  DAT

[Buoc 2] AC-01 — nua voi KHONG duoc lam D1004 nhay sang 3
    D106 = 0000 A0F3 (nua voi)                     D1004 = 1  DAT (giu nguyen 1)

[Buoc 3] AC-03 — nhat ky van phai ghi duoc gia tri nua voi
    DAT: 14:59:35.557  7660 A0F3   -> 0000 A0F3  | ma the: a0f30000

[Buoc 4] Don dep — luon chay ke ca khi buoc tren truot
    D106 = 0000 0000 (sach)                        D1004 = 0  DAT

[Dieu kien hop le] Loi FINS cho 192.169.1.120 trong cua so chay
    Khong co. Luot do hop le.

=== KET QUA ===
  AC-01 : PASS
  AC-02 : PASS
  AC-03 : PASS

  Verification: PASS
EXIT: 0
```

### Bằng chứng độc lập — nhật ký, không qua lời của probe

```
14:59:35.055  block 20  D106   0000 0000 -> 7660 A0F3  | ma the: a0f37660
14:59:35.058  block 20  D1004  0 -> 1
14:59:35.557  block 20  D106   7660 A0F3 -> 0000 A0F3  | ma the: a0f30000
              (KHONG co dong D1004 nao theo sau)
14:59:40.157  block 20  D106   0000 A0F3 -> 0000 0000  | khong co the
14:59:40.157  block 20  D1004  1 -> 0
```

Số dòng `D1004 -> 3` cho block 20 trong cửa sổ chạy: **0**.

Đối chiếu cùng chuyển trạng thái `D106` trước khi vá (block 103, 25/09):

```
12:54:59.897  block 103  D106   7660 A0F3 -> 0000 A0F3  | ma the: a0f30000
12:54:59.899  block 103  D1004  1 -> 3
```

Cùng `7660 A0F3 -> 0000 A0F3`, kết cục `D1004` ngược nhau — đây là cặp
trước/sau trên cùng một giá trị thanh ghi, cùng một thẻ, cùng một định dạng
nhật ký.

### Ghi chú thi hành

Lượt chạy đầu bị cửa tiền đề chặn nhầm: nó đếm dòng `[dau tien]` (ảnh chụp lúc
app khởi động) là lượt quẹt, mà mỗi lần deploy sinh ra một dòng như vậy cho từng
block — đo được **217 dòng cùng mốc 14:58:30**. Đã sửa `tools/kiem_chung_doc_hut.py`
để bỏ qua dòng `[dau tien]`, rồi chạy lại. Đây là lỗi của trình kiểm chứng, không
phải của bản vá, và nó nghiêng về phía an toàn (từ chối đo, không cho PASS giả).
