# Câu hỏi gửi bên lập trình PLC — lỗi FINS `0x00000020`

**Ngày:** 24/09/2026 · **Người gửi:** đội SCADA TotalParking

Hiện có **56/86 PLC** mà SCADA không kết nối được. Chúng tôi đã đo và loại trừ
được phần lớn nguyên nhân từ phía SCADA, nhưng còn **một câu hỏi duy nhất**
chỉ bên PLC trả lời được. Tài liệu này nêu bằng chứng đã có và câu hỏi cần hỏi.

---

## 1. Hiện tượng

SCADA mở TCP tới cổng 9600 thành công, nhưng bước bắt tay FINS bị PLC từ chối:

```
PLC tu choi bat tay FINS/TCP, ma loi 0x00000020
```

Tài liệu Omron ghi mã này là **"All connections are in use"** — PLC hết khe
kết nối để cấp.

## 2. Số liệu lúc 00:28 ngày 24/09

| | Số lượng |
|---|---|
| PLC trong vòng quét của SCADA | 86 |
| **SCADA đọc được** | **29** |
| **Lỗi `0x20`** | **56** |
| Mất hẳn khỏi mạng (block 83) | 1 |

**56 block báo `0x20`:**

```
 1   2  12  16  18  19  21  22  23  25  27  29  30  31
36  37  38  39  40  41  42  43  51  52  53  55  56  58
60  62  63  64  65  70  71  72  73  74  75  76  77  78
79  80  81  82  84  85  86  87  88  89  94  95  96 103
```

**29 block đang hoạt động bình thường:**

```
 3   4   5   6   7   8   9  10  17  48  50  54  59  61
91  92  93  97  98  99 100 101 102 104 105 108 109 110 112
```

Cả 56 block lỗi đều **ping được và mở cổng 9600** — thiết bị sống, mạng thông.

## 3. Những gì chúng tôi đã đo

### 3.1 Mỗi PLC chỉ có **3 khe** FINS/TCP

Mở lần lượt nhiều kết nối tới cùng một PLC, bắt tay thật mỗi lần:

```
kết nối 1 : OK, PLC cấp node 251
kết nối 2 : OK, PLC cấp node 252
kết nối 3 : OK, PLC cấp node 253
kết nối 4 : TỪ CHỐI — 0x00000020  (từ chối tức thì, 0.00 giây)
```

Lặp lại trên 4 PLC khác nhau (block 11, 13, 14, 15) — **kết quả giống hệt:
đúng 3 khe, node 251/252/253**.

### 3.2 Đóng kết nối đúng cách trả khe lại **ngay lập tức**

```
chiếm hết 3 khe      -> OK
đóng sạch cả 3       -> OK
mở lại sau 0 giây    -> ĐƯỢC NGAY
```

Không cần chờ. Nghĩa là cơ chế giải phóng khe của PLC hoạt động bình thường
khi client đóng đàng hoàng.

### 3.3 Máy chủ SCADA **không giữ kết nối nào** tới 56 block đó

Kiểm bằng `Get-NetTCPConnection` trên máy chủ SCADA:

```
Kết nối tới cổng 9600 : 306
  ESTABLISHED : 28   <- đúng bằng số block SCADA đọc được
  TIME_WAIT   : 278  <- socket đã đóng, đang chờ hết hạn
```

Với từng block trong nhóm `0x20` (đã kiểm block 70–80 và 83): **không có kết
nối ESTABLISHED nào** từ máy chủ SCADA.

### 3.4 Một PLC đã tự khỏi mà không ai can thiệp

Block 93 kẹt `0x20` từ **12:28**, kiểm lại lúc 20:25 vẫn kẹt, nhưng đến
**23:19 tự hoạt động lại** — khoảng **11 tiếng**, không ai cấp nguồn lại hay
thao tác gì.

Nghĩa là khe **có** được giải phóng, chỉ là rất chậm.

## 4. Kết luận từ phía SCADA

Ba khe của 56 PLC đó **đang bị một thứ khác chiếm**, không phải SCADA:

- SCADA không có kết nối nào tới chúng (mục 3.3)
- Khi SCADA đóng thì khe được trả ngay (mục 3.2)
- Khe tự trống sau nhiều giờ (mục 3.4) — đúng kiểu phiên bị bỏ rơi chờ hết hạn

Chúng tôi **không nhìn được** phía bên kia: khe bị chiếm nằm trong bộ nhớ PLC,
chỉ máy đang chiếm mới thấy.

---

## 5. Câu hỏi cần trả lời

### Câu hỏi chính

> **Có máy nào khác đang kết nối FINS/TCP vào các PLC trong danh sách ở mục 2
> không?**

Cụ thể xin xác nhận:

1. **CX-Programmer / CX-One** có đang mở online tới dải PLC này không? Máy nào,
   có bao nhiêu phiên?
2. **Phần mềm giám sát riêng** của bên PLC (nếu có) có kết nối thường trực
   không? Mở bao nhiêu kết nối mỗi PLC?
3. **HMI** tại từng tủ có dùng FINS/TCP qua Ethernet không, hay nối bằng đường
   khác (serial, bus riêng)? Nếu dùng FINS/TCP thì mỗi HMI chiếm mấy khe?
4. Có **bản SCADA hoặc hệ thu thập dữ liệu nào khác** đang chạy song song
   không?

### Câu hỏi phụ

5. **CP-series có tham số timeout cho phiên FINS/TCP không?** Hiện phiên bị bỏ
   rơi mất khoảng 11 tiếng mới được dọn. Nếu chỉnh xuống vài phút được thì sự
   cố này sẽ tự hết.
6. **Số khe FINS/TCP có tăng được không?** 3 khe là rất ít: SCADA 1, một phiên
   bỏ rơi 1, một máy kỹ thuật 1 — là hết.
7. Con số **3 khe** và **node 251/252/253** có đúng như cấu hình các anh đặt
   không, hay là mặc định của thiết bị?

---

## 6. Phép thử đề xuất — 5 phút là xong

Nếu nghi ngờ có máy khác đang chiếm:

1. **Tắt hết** phần mềm đang kết nối tới một PLC cụ thể trong danh sách — ví dụ
   **block 70 (`192.169.1.170`)**.
2. Báo lại cho đội SCADA.
3. Chúng tôi kiểm ngay xem SCADA có nối được không.

Nếu nối được → xác nhận đúng nguyên nhân, và chỉ cần thống nhất quy tắc dùng
khe là xong.

Nếu vẫn không nối được → nguyên nhân nằm chỗ khác, chúng tôi sẽ đào tiếp.

---

## 7. Phía SCADA đã làm gì để giảm ảnh hưởng

Đã giãn nhịp thử lại riêng cho lỗi `0x20` từ 30 giây lên 5 phút. Kết quả đo:

| | Trước | Sau |
|---|---|---|
| Kết nối TCP mở mới | 158/phút | ~20/phút |
| Socket `TIME_WAIT` thường trực | 334 | 0 giữa hai đợt |

Việc này **chỉ ngừng đốt tài nguyên và giảm tranh chấp**, không giải phóng khe.
Nguyên nhân gốc vẫn cần câu trả lời ở mục 5.

---

## 8. Liên hệ

Mọi số liệu trong tài liệu này đo trực tiếp trên hệ thống đang chạy, có thể đo
lại bất cứ lúc nào. Nếu cần thêm bằng chứng hoặc muốn xem trực tiếp, xin liên
hệ đội SCADA.
