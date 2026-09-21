# Công cụ đọc/ghi thanh ghi PLC — `tools/plc_register.py`

Đọc và ghi thanh ghi DM của PLC Omron qua FINS/TCP, chạy thẳng từ máy SCADA.

> **Đây là công cụ TEST, không phải một đường vận hành.**
>
> Luồng thật vẫn là: camera/thẻ quét → site TotalParking → vòng poll
> (`PlcHost` + `PlcConnectionManager`) → ghi `D1000`. Công cụ này không thay thế,
> không bổ sung, và không được nối vào luồng đó. Không có gì gọi nó; nó chỉ chạy
> khi có người gõ tay.
>
> Nếu thấy mình phải dùng công cụ này lặp đi lặp lại để chữa cháy một sự cố, đó là
> dấu hiệu luồng thật đang hỏng và cần sửa ở đó, không phải ở đây.

## Dùng khi nào

- Xem PLC đang thật sự giữ giá trị gì, khi số trên SCADA và trên HMI không khớp.
- Nghiệm thu HMI: cưỡng bức một giá trị rồi xem màn hình khối đỗ phản ứng thế nào.
- Thao tác với block đang `is_active = 0` — những block này **không nằm trong vòng
  poll**, nên `POST /PlcStatus/Write` sẽ trả 404, còn công cụ này vẫn vào được.
- Thay cho việc ra tận HMI xoá thanh ghi bằng tay (xem `xoa_thanh_ghi_test.txt`) —
  nhưng chỉ sau khi đã đọc kỹ phần cảnh báo bên dưới.

## Chuẩn bị

Cần Python 3 trên máy chạy SCADA. **Không phải cài thêm thư viện nào** — công cụ chỉ
dùng thư viện chuẩn. Thông tin PLC (IP, port, node) được tra tự động từ DB, mật khẩu
DB đọc từ `Web.config` nên không phải nhập gì.

Chạy từ thư mục gốc của repo.

## Các lệnh

```bash
# Đọc
python tools/plc_register.py --block 95 --read D1000
python tools/plc_register.py --block 95 --read D100 --count 4

# Ghi (tự đọc trước → ghi → đọc lại xác nhận)
python tools/plc_register.py --block 95 --write D1000 --value 103

# PLC chập chờn thì nới thời gian chờ
python tools/plc_register.py --block 95 --read D1000 --timeout-ms 6000 --retries 5
```

Chỉ cần nhập **số block**. Nếu muốn bỏ qua DB thì dùng `--ip` thay cho `--block`:

```bash
python tools/plc_register.py --ip 192.169.1.195 --read D1000
```

### Tham số

| Tham số | Ý nghĩa | Mặc định |
|---|---|---|
| `--block N` | Số block, tự tra IP/port/node từ DB | — |
| `--ip A.B.C.D` | Dùng IP trực tiếp, không qua DB | — |
| `--read D1000` | Đọc thanh ghi | — |
| `--write D1000` | Ghi thanh ghi, đi kèm `--value` | — |
| `--value N` | Giá trị ghi xuống, 0…65535 | — |
| `--count N` | Số word đọc liên tiếp, 1…64 | 1 |
| `--timeout-ms N` | Thời gian chờ mỗi thao tác | 3000 (hoặc theo DB) |
| `--retries N` | Số lần thử kết nối lại | 4 |
| `--force` | Cho ghi thanh ghi ngoài danh sách an toàn | tắt |
| `--xac-nhan-o-trong` | Đã có người xác nhận tận nơi ô đỗ trống | tắt |

Ghi luôn theo trình tự **đọc trước → ghi → đọc lại**. Nếu đọc lại không ra đúng giá
trị vừa ghi, công cụ báo `LỆCH!` và thoát với mã lỗi — thường là do ladder ghi đè
ngay sau đó.

## Bản đồ thanh ghi

Cả 112 PLC dùng chung một bố cục:

| Thanh ghi | Nội dung | Ghi được? |
|---|---|---|
| `D100`–`D101` | Mã thẻ vừa quẹt (32 bit, 2 word) | cần `--force` |
| `D200`–`D211` | Mã thẻ các ô đỗ 2…5 (`D202 D204 D206 D208`) | **chặn** |
| `D300`–`D311` | Mã thẻ các ô đỗ 6…10 (`D300`…`D308`) | **chặn** |
| `D400`–`D411` | Mã thẻ ô đỗ 1 (`D400`); `D402` là word phân loại | **chặn** |
| `D1000` | Số block trả lời tìm xe (SCADA → PLC) | **ghi thẳng** |
| `D1002`–`D1003` | Mã thẻ cần tìm xe (PLC → SCADA) | cần `--force` |

## Ba tầng hàng rào an toàn

Cả ba đều chặn **trước khi** kết nối tới PLC, nên lệnh sai không hề chạm vào thiết bị.

1. **`D1000`** — ghi thẳng, không cần cờ gì.
2. **Vùng ô đỗ `D200`–`D211`, `D300`–`D311`, `D400`–`D411`** — chặn hẳn, `--force`
   không mở được. Phải dùng riêng `--xac-nhan-o-trong`.
3. **Mọi thanh ghi còn lại** — cần `--force`.

> **Vì sao vùng ô đỗ bị chặn riêng.** Đó là thanh ghi mã thẻ của từng ô đỗ, do ladder
> sở hữu. Ghi `0` xuống làm ô đó trông như đã trống, ladder có thể xếp xe khác vào
> → **va chạm xe thật**. Chỉ ghi sau khi có người ra tận nơi nhìn thấy ô đỗ trống.
>
> Mỗi ô chiếm **2 word liên tiếp** — xoá `D206` thì phải xoá cả `D207`.

Lưu ý: `D402` (word phân loại) nằm trong dải `D400`–`D411` nên cũng bị chặn theo. Đây
là cố ý — thà chặn dư còn hơn sót.

## Nhật ký

Mọi lần ghi được ghi vào `TotalParking/App_Data/plc_manual_write.log`:

```
2026-09-21 17:28:43  192.169.1.195  block=95  D1000  0 -> 103  OK  ghi tay bang tools/plc_register.py
```

File này **tách riêng** khỏi `plc_audit.log` của ứng dụng, để hai tiến trình không
tranh nhau ghi cùng một file.

## PLC chập chờn — không phải lỗi công cụ

PLC Omron chỉ nhận **một số ít kết nối FINS/TCP cùng lúc**. Khe của lần trước cần vài
giây mới được giải phóng, nên `TimeoutError` khi kết nối **không có nghĩa là PLC chết**.

Đo thực tế trên block 95: 6 lần liên tiếp thì 4 lần nối được (có lần 0.00 s), 2 lần
timeout hẳn 6 giây. Gặp trường hợp này thì nới ra:

```bash
python tools/plc_register.py --block 95 --read D1000 --timeout-ms 6000 --retries 5
```

Giá trị `timeout_ms` trong DB được chỉnh cho vòng poll 500 ms nên khá chặt; tham số
`--timeout-ms` trên dòng lệnh luôn được ưu tiên hơn.

## Không được làm

- **Không chạy trong vòng lặp tự động.** Một khung FINS là cặp ghi-rồi-đọc không thể
  xen kẽ. Với block **đang** nằm trong vòng poll, mở thêm kết nối là ăn mất khe của site.
- **Không nối vào job, scheduler, hay bất kỳ luồng nào.**
- **Không dùng `--force` khi chưa biết thanh ghi đó làm gì.**

## Đối chiếu độ tin cậy

Khung tin được chuyển thể từ `TotalParking/Services/Plc/OmronFinsClient.cs` — bản đã
chạy thật trên toàn bộ 112 PLC. Đã đối chiếu chéo với code production trên dữ liệu
khác 0 (`D400`–`D403` của block 103), hai bên khớp byte-per-byte.
