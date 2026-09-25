# Hướng dẫn vận hành — cảnh báo và sự cố

**Dành cho:** người vận hành SCADA TotalParking
**Cập nhật:** 25/09/2026 (bổ sung mục 4 và 5)

---

## 1. Cảnh báo đến từ đâu

Trước đây trang **Cảnh báo** hiện 6 dòng và 4 con số **viết cứng trong HTML** — chúng hiện ra kể cả khi không có sự cố nào, và không đổi khi có sự cố thật. Giờ mọi thứ trên trang đó đến từ cơ sở dữ liệu.

Có **hai nguồn** sinh ra cảnh báo:

| Nguồn | Ai tạo | Khi nào |
|---|---|---|
| `Lỗi thiết bị` | Máy tự sinh | PLC mất kết nối liên tục quá **5 phút** |
| `Lỗi thao tác` | Người bấm | Bấm nút **BÁO SỰ CỐ** trên trang Điều khiển vận hành |

Chưa có cảnh báo tự động cho LED, CCU hay camera. Những thiết bị đó hỏng thì trang Cảnh báo **không** báo gì.

### Ba nơi hiện cảnh báo, cùng một nguồn

| Nơi | Hiện gì | Mục |
|---|---|---|
| **Thanh trên cùng** (mọi trang) | Chip tình trạng + số cảnh báo chờ xử lý | 4 |
| **Trang chủ** (Dashboard) | Bảng cảnh báo, ba bộ đếm, biểu ngữ đỏ | 5 |
| **Trang Cảnh báo** | Danh sách đầy đủ, bộ lọc, nút xác nhận | 3 |

Cả ba đọc cùng một bảng trong cơ sở dữ liệu. **Ba nơi lệch nhau nghĩa là có chỗ đang hỏng** — xem mục 10.

---

## 2. Cảnh báo PLC mất kết nối

### Nó hoạt động thế nào

Cứ 30 giây hệ thống rà một lượt. Khối nào có PLC **chắc chắn đang chết** liên tục quá ngưỡng thì sinh **đúng một dòng** cảnh báo mức *Nghiêm trọng*.

PLC còn chết thì **không sinh thêm dòng nào nữa**. Đây là điều quan trọng: một PLC chết ba ngày mà mỗi lượt sinh một dòng thì bảng sẽ có 8.640 dòng cho một sự cố, và trang Cảnh báo thành vô dụng.

Khi PLC nối lại, dòng cũ được **đánh dấu đã hết** — không bị xoá. Lịch sử sự cố là thứ cần nhất khi điều tra về sau. Lần sự cố tiếp theo trên cùng khối vẫn sinh được dòng mới.

### Hai lớp chống rung

PLC chập chờn vài giây **không** tạo ra cảnh báo:

1. Hệ thống đòi **3 lần quan sát liên tiếp** cùng trạng thái (nhịp 5 giây) trước khi ghi nhận PLC là chết — tức khoảng 15 giây ổn định.
2. Rồi phải chết liên tục thêm **5 phút** nữa mới sinh cảnh báo.

Ngưỡng ngắn hơn thì bảng đầy cảnh báo tự tắt, và người vận hành bắt đầu bỏ qua chúng — đó là cách một hệ thống cảnh báo chết.

### Đổi ngưỡng

Sửa trong `Web.config` của **bản đang chạy** (`C:\Users\Admin\Documents\Web\totalParking\Web.config`):

```xml
<add key="plc:canhBaoMatKetNoiSauPhut" value="5" />
```

> ⚠️ Ghi `Web.config` làm ứng dụng **tự khởi động lại**. Đừng sửa lúc đang có xe vào ra.

> ⚠️ Sửa `Web.config` trong thư mục mã nguồn là **vô nghĩa** — ứng dụng không đọc file đó.

---

## 3. Đọc trang Cảnh báo

Trang tự làm mới **15 giây một lần**. Góc trên có dòng *"Cập nhật hh:mm:ss"* cho biết lần đọc gần nhất.

**Bảng trống ≠ mất kết nối máy chủ.** Hai tình huống hiện khác nhau:

| Thân bảng hiện | Nghĩa là |
|---|---|
| *"Không có cảnh báo nào."* | Tin tốt — không có sự cố nào |
| *"Không đọc được danh sách cảnh báo: ..."* (chữ đỏ) | Sự cố — trang đang mù, đừng tin các con số |

Bốn thẻ số liệu phía trên đếm theo nguồn và theo trạng thái xác nhận, cùng từ một lần đọc với bảng bên dưới.

### Giới hạn số dòng

Trang trả về **tất cả** cảnh báo chưa xác nhận, cộng tối đa **200** dòng đã xác nhận gần nhất. Muốn xem xa hơn phải tra bằng SQL (mục 8).

---

## 4. Thanh trên cùng — chip tình trạng

Thanh này có ở **mọi trang**, nên nó là thứ nhìn thấy nhiều nhất.

### Chip bên trái — tình trạng hệ thống

| Màu | Chữ | Nghĩa là |
|---|---|---|
| 🟢 Xanh | Bình thường | Không còn cảnh báo nào đang mở **và** không còn cảnh báo nào chờ xác nhận |
| 🟠 Hổ phách | Cảnh báo | Có cảnh báo đang mở, nhưng không có cảnh báo mức *Nghiêm trọng* |
| 🔴 Đỏ nhấp nháy | Sự cố | Có ít nhất một cảnh báo mức *Nghiêm trọng* đang mở |
| ⚪ Xám | Mất kết nối | **Không đọc được máy chủ** — đừng tin con số bên cạnh |

> Chip xanh đòi **cả hai** điều kiện. Một cảnh báo đã có người xác nhận nhưng thiết bị vẫn hỏng thì hệ thống **chưa** bình thường, nên chip vẫn không xanh.

### Chip bên phải — số cảnh báo

Số cảnh báo **chưa ai xác nhận**. Nó nhấp nháy **chỉ khi** có sự cố nghiêm trọng.

Trước đây chip này luôn ghi cứng "4 Alarm" và nhấp nháy vĩnh viễn kể cả lúc hệ thống hoàn toàn bình thường. Nhấp nháy mãi thì người ta học cách phớt lờ, và lúc có sự cố thật nó trông không khác gì.

Khi mất kết nối, số đổi thành `—`. **Số cũ không được giữ lại** — số cũ trông y hệt số đúng.

### Đồng hồ

Chạy theo giờ máy chủ SCADA. Trước đây nó đứng im ở `16:02:42 · 08/06/2026`.

---

## 5. Trang chủ — bảng cảnh báo

Bảng này tự làm mới **15 giây một lần** và hiện **mọi cảnh báo chưa xác nhận**.

### Ba con số phía trên

Ba bộ đếm *Lỗi Thao Tác*, *Lỗi Thiết Bị*, *Đến Hạn Bảo Trì* và biểu ngữ đỏ trên cùng đều tính trên **cùng một tập** với bảng bên dưới: cảnh báo chưa xác nhận.

**Tổng ba bộ đếm luôn bằng số dòng trong bảng.** Thấy chúng lệch nhau nghĩa là có chỗ đang hỏng — báo lại kỹ thuật, đừng tự suy luận.

### Cột "Nguồn phát"

Ghép từ ba phần, **bỏ qua phần nào không có**:

- `Z2` — khu vực
- `Block 21` — số khối
- `PLC 21` — thiết bị

Cảnh báo do người bấm nút BÁO SỰ CỐ **không có số khối** (lý do ở mục 7), nên nó chỉ hiện khu vực và tên khối trên màn hình.

### Cột "Thời gian" hiện đủ ngày

Bảng này chứa **mọi** cảnh báo chưa ai xác nhận, không giới hạn tuổi. Một sự cố từ ba hôm trước vẫn nằm đó. Vì vậy cột Thời gian hiện đủ `yyyy-MM-dd HH:mm:ss` — chỉ hiện giờ thì dễ đọc nhầm sự cố cũ thành "sáng nay".

### Ba trạng thái của bảng, đừng nhầm

| Thân bảng hiện | Nghĩa là |
|---|---|
| Danh sách cảnh báo | Bình thường |
| *"Không có cảnh báo hoạt động. Hệ thống vận hành an toàn."* | Tin tốt — **máy chủ đã trả lời** và không có gì |
| *"Không đọc được cảnh báo từ máy chủ: ..."* (chữ đỏ) | Trang đang mù. Biểu ngữ đỏ trên cùng vẫn hiện và cũng báo lỗi |

Hai câu cuối **khác nhau hoàn toàn**. Câu giữa nghĩa là an toàn thật; câu cuối nghĩa là không biết gì cả.

### Nút Xác nhận

Bấm → hệ thống hỏi tên → ghi vào cơ sở dữ liệu → dòng biến khỏi bảng. Cảnh báo **không** bị xoá, chỉ được đánh dấu đã có người thấy; xem lại ở trang Cảnh báo hoặc bằng SQL.

> ⚠️ **Trang chủ trước đây bịa ra cảnh báo.** Cho tới 25/09/2026, trang này tự sinh cảnh báo ngẫu nhiên mỗi 15 giây từ một danh sách câu viết sẵn — ví dụ *"Mất kết nối truyền thông Modbus TCP với PLC chính"*. Hệ thống này **không dùng Modbus TCP**. Còn thấy những câu như vậy ở đâu nghĩa là màn hình đó chưa được cập nhật.

---

## 6. Xác nhận một cảnh báo

Bấm **ACK** trên dòng cảnh báo → hệ thống hỏi tên người xác nhận.

> ⚠️ **Tên này là tự khai.** Hệ thống chưa có đăng nhập. Bất kỳ ai mở được trang cũng gõ được tên người khác. Cột *Người xác nhận* là dấu vết vận hành, **không phải bằng chứng danh tính**. Địa chỉ máy bấm được lưu kèm.

Hai người cùng bấm cách nhau vài giây thì người thứ hai nhận thông báo *"Cảnh báo này đã được người khác xác nhận trước đó"* — không đè mất dấu vết người đầu tiên.

Xác nhận **không** làm sự cố biến mất. Nó chỉ ghi rằng đã có người nhìn thấy.

---

## 7. Nút BÁO SỰ CỐ

Trên trang **Điều khiển vận hành**, ô nút chế độ có nút đỏ ngoài cùng bên phải, trước đây ghi `ESTOP`.

> ### 🛑 Nút này KHÔNG dừng máy
>
> Nó **chỉ ghi nhận** một dòng sự cố vào cơ sở dữ liệu để các máy khác nhìn thấy. Nó không gửi lệnh nào xuống PLC và sẽ không bao giờ gửi.
>
> **Dừng khẩn cấp thật là nút cơ khí ngoài hiện trường.** Không có cách nào thay thế nó bằng phần mềm.

Nút cũ tệ hơn nhiều: nó mang nhãn dừng khẩn cấp nhưng chỉ tô đỏ ô **trong chính trình duyệt đang mở**. Người vận hành bấm, thấy ô chuyển đỏ, tin rằng đã xử lý — trong khi không có gì xảy ra và máy bên cạnh không biết gì.

### Dùng thế nào

1. Chọn khối trên lưới.
2. Bấm **BÁO SỰ CỐ** → xác nhận trong hộp thoại.
3. Nút chuyển thành **ĐÃ BÁO SỰ CỐ**, thẻ khối có nhãn đỏ *"ĐANG BÁO SỰ CỐ"*.
4. Máy khác đang mở trang thấy trong vòng **15 giây**, không cần tải lại trang.

Mỗi lần bấm là **một dòng riêng** — bấm ba lần thì có ba dòng. Đó là chủ ý: mỗi lần báo là một sự kiện cần có dấu vết.

Dấu sự cố mất đi khi có người **xác nhận** dòng đó ở trang Cảnh báo. Chưa có nút "đóng sự cố" riêng.

### Khối ghi vào cảnh báo là tên trên màn hình

Danh sách khối trên trang này (`Block A-01`, `Block A-02`...) là **tên hiển thị**, không phải số khối thật trong 112 khối của bãi. Nên cảnh báo lưu đúng cái tên bạn nhìn thấy, và để trống cột số khối. Ghi một con số suy đoán vào đó sẽ trỏ sai khối khi điều tra.

---

## 8. Tra cứu bằng SQL

Cảnh báo đang mở, mới nhất trước:

```sql
SELECT canh_bao_id, xay_ra_luc, nguon, muc_do, block_no, thiet_bi, mo_ta
FROM   canh_bao
WHERE  het_luc IS NULL
ORDER  BY xay_ra_luc DESC;
```

Lịch sử sự cố của một khối:

```sql
SELECT xay_ra_luc, het_luc, mo_ta, xac_nhan_boi, xac_nhan_luc
FROM   canh_bao
WHERE  block_no = 21
ORDER  BY xay_ra_luc DESC;
```

Ý nghĩa các cột hay dùng:

| Cột | Nghĩa |
|---|---|
| `het_luc` | `NULL` = sự cố đang diễn ra; có giá trị = đã qua |
| `xac_nhan_luc` | `NULL` = chưa ai nhìn thấy |
| `xac_nhan_boi` | Tên **tự khai**, xem cảnh báo ở mục 6 |
| `khoa_chong_trung` | Dấu kỹ thuật, chỉ có giá trị khi cảnh báo đang mở. **Đừng sửa tay.** |

> ⚠️ **Đừng xoá dòng** để "dọn" bảng. Sự cố đã qua được đánh dấu bằng `het_luc`, và lịch sử là thứ cần nhất khi điều tra.

---

## 9. Những việc hệ thống KHÔNG làm

Đọc kỹ mục này — hiểu sai một dòng ở đây là hiểu sai cả hệ thống.

| Không làm | Nghĩa là |
|---|---|
| Không dừng máy | Không nút nào trên SCADA dừng được thiết bị |
| Không gửi cảnh báo ra ngoài | Không SMS, không email, không còi. Phải có người nhìn màn hình |
| Không cảnh báo cho LED / CCU / camera | Chỉ PLC mới có cảnh báo tự động |
| Không giới hạn số dòng trên trang chủ | Cảnh báo tồn đọng nhiều thì bảng dài ra, chưa có phân trang |
| Không tự đóng sự cố do người báo | Chỉ cảnh báo PLC mới tự đóng khi thiết bị nối lại |
| Không xác thực người xác nhận | Tên là tự khai |
| Không sinh cảnh báo khi tắt vòng poll PLC | `plc:enabled = false` thì `is_connected` ngừng cập nhật, nên không rà nữa |

---

## 10. Khi nghi ngờ trang đang hiện số sai

1. Mở trang Cảnh báo, xem dòng *"Cập nhật hh:mm:ss"* — nếu đứng yên quá 15 giây thì trang đang mất kết nối máy chủ.
2. Nếu thân bảng là chữ đỏ *"Không đọc được..."* → sự cố ở máy chủ hoặc CSDL, **không phải** là không có cảnh báo.
3. Đối chiếu bằng SQL ở mục 8. Cơ sở dữ liệu là nguồn đúng; màn hình chỉ là bản chiếu.
