# Hướng dẫn vận hành — hai cờ trạng thái PLC

**Dành cho:** người vận hành SCADA TotalParking
**Cập nhật:** 24/09/2026

---

## 1. Hai cờ, hai câu hỏi khác nhau

Hệ thống có **hai** công tắc cho mỗi khối đỗ. Chúng trông giống nhau nhưng trả lời hai câu hỏi hoàn toàn khác:

| Cờ | Trả lời câu hỏi | Hạ xuống thì sao |
|---|---|---|
| `plc_device.is_active` | *Có kết nối và đọc PLC này không?* | SCADA thôi giám sát khối đó — không biết PLC sống hay chết |
| `block.is_active` | *Khối này có nhận xe mới và tính vào sức chứa không?* | Khối thôi nhận xe mới, số chỗ trống trên bảng LED giảm |

**Điều quan trọng nhất cần nhớ:** hạ `block.is_active` **không** làm mất xe đang gửi. Xe trong khối đó vẫn lấy ra bình thường — chỉ là không nhận thêm xe mới.

### Khi nào dùng cờ nào

| Tình huống | Hạ cờ nào |
|---|---|
| Khối đang có thợ sửa chữa | `block.is_active = 0` (thôi nhận xe, vẫn giám sát được PLC) |
| PLC hỏng phải tháo ra khỏi hệ thống | `plc_device.is_active = 0` |
| Khối chưa nghiệm thu xong | `block.is_active = 0` |

Trước đây hai cờ bị trộn: hạ cờ vận hành thì SCADA cũng thôi giám sát PLC luôn — mất khả năng biết thiết bị còn sống hay không đúng lúc cần biết nhất. Giờ đã tách.

---

## 2. Ba cột cho biết PLC đang thế nào

Xem bằng SQL:

```sql
SELECT b.block_no, d.is_connected, d.connected_changed_at, d.last_probe_at
FROM plc_device d JOIN block b ON b.block_id = d.block_id
ORDER BY b.block_no;
```

| Cột | Ý nghĩa |
|---|---|
| `is_connected` | `1` sống · `0` chết · `NULL` không nằm trong vòng giám sát |
| `connected_changed_at` | Trạng thái đó giữ nguyên **từ bao giờ** |
| `last_probe_at` | Lần quan sát gần nhất là **lúc nào** |

### Cách đọc cho đúng

`NULL` khác `0`. `NULL` nghĩa là *không biết* (khối không được giám sát), còn `0` nghĩa là *biết chắc đang chết*.

**Luôn nhìn `last_probe_at` trước.** Nếu nó cũ hơn vài phút thì toàn bộ số liệu đang đóng băng — thường là do site đã dừng. Lúc đó `is_connected` không còn đáng tin, dù nó ghi `1`.

> Ví dụ dễ nhầm: một PLC online ổn định ba ngày và một site đã chết năm phút trước **cho ra `connected_changed_at` giống hệt nhau**. Chỉ `last_probe_at` phân biệt được hai tình huống đó.

---

## 3. Bảng theo dõi khối lệch

Đây là chỗ nhìn ra vấn đề nhanh nhất:

```sql
SELECT * FROM v_plc_lech_tang;
```

Nó chỉ liệt kê khối **đang có vấn đề**. Bảng rỗng nghĩa là mọi thứ khớp nhau.

| Cột `tinh_trang` | Nghĩa là gì | Nên làm gì |
|---|---|---|
| `PLC chet ma van nhan xe` | Khối vẫn được xếp xe nhưng SCADA không đọc được PLC | Kiểm tra nguồn/mạng của khối. Nếu hỏng lâu, hạ `block.is_active = 0` |
| `PLC song nhung nguoi da khoa` | Có người chủ động khoá khối | Bình thường nếu đang sửa chữa. Nhớ mở lại khi xong |
| `Khong giam sat ma van nhan xe` | Khối nhận xe nhưng không ai giám sát PLC | Bật `plc_device.is_active = 1` rồi gọi Reload (xem mục 5) |

Cũng xem được qua trình duyệt: `http://localhost:8080/PlcStatus/Index`, mục `cong_van_hanh`.

---

## 4. Tự hạ / tự bật khối

Hệ thống có thể **tự** đưa khối ra khỏi vận hành khi PLC chết lâu, và tự đưa vào lại khi PLC sống lại.

### Mặc định TẮT

Bật trong `Web.config`:

```xml
<add key="plc:tuDongCongVanHanh" value="true" />
```

**Trước khi bật, cần biết:** ngay khi bật, mọi khối có PLC chết quá ngưỡng sẽ bị hạ **cùng lúc**, và số chỗ trống trên bảng LED giảm thấy rõ. Nên bật vào lúc có người nhìn được bảng LED, không bật rồi bỏ đi.

Kiểm trước xem sẽ có bao nhiêu khối bị hạ:

```sql
SELECT COUNT(*) FROM v_plc_lech_tang WHERE tinh_trang = 'PLC chet ma van nhan xe';
```

### Bốn lớp an toàn

1. **Công tắc tổng** — mặc định tắt, phải bật tường minh.
2. **Trễ khác nhau hai chiều** — hạ sau 15 phút mất kết nối, bật lại sau 5 phút nối lại. Khối chập chờn vài giây không bao giờ chạm tới cờ.
3. **Trần 4 lần đổi mỗi ngày** — khối dao động quá nhiều thì hệ thống **ngừng tự đổi** và chờ người xử lý. Đó là dấu hiệu hỏng phần cứng, không phải thứ để máy tự chữa.
4. **Không đụng khối người khoá tay** — hệ thống chỉ mở lại những khối chính nó đã hạ. Khối bro khoá để thợ làm việc thì máy không được phép mở.

Lớp thứ tư là lớp bảo vệ con người. Đừng bao giờ gỡ nó.

### Chỉnh ngưỡng

```xml
<add key="plc:haSauPhut"        value="15" />
<add key="plc:batSauPhut"       value="5" />
<add key="plc:tranDoiCoMoiNgay" value="4" />
```

Ngưỡng nhỏ nhất là **1 phút**. Đặt quá ngắn thì khối chập chờn sẽ bị bật tắt liên tục và tài xế thấy số trên bảng nhảy loạn.

### Khối bị chạm trần thì làm sao

Hệ thống ngừng tự đổi khối đó cho tới hết ngày. Muốn mở lại ngay:

```sql
UPDATE block SET so_lan_doi_hom_nay = 0 WHERE block_no = <số khối>;
```

Nhưng trước khi làm, nên tìm hiểu vì sao khối đó dao động — chạm trần là **triệu chứng**, không phải vấn đề.

---

## 5. Thao tác thường gặp

### Đưa một PLC mới vào giám sát

```sql
UPDATE plc_device d JOIN block b ON b.block_id = d.block_id
   SET d.is_active = 1 WHERE b.block_no = <số khối>;
```

Rồi **bắt buộc** gọi:

```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:8080/PlcStatus/Reload" -Body ""
```

> ⚠️ **Quên bước Reload là lỗi hay gặp nhất.** Câu `UPDATE` không tự làm hệ thống đọc lại. Đã từng xảy ra: cơ sở dữ liệu ghi 107 PLC mà hệ thống chỉ giám sát 87 — 20 PLC không ai biết là đang không được theo dõi.

Kiểm lại hai số phải bằng nhau:

```sql
SELECT COUNT(*) FROM plc_device WHERE is_active = 1;
```
so với số khối trong `http://localhost:8080/PlcStatus/Index`.

> ⚠️ **Đưa một khối vào giám sát là GHI xuống PLC của nó.** Nhịp đọc đầu tiên ghi `D1004 = 0`. Với khối đã nghiệm thu thì vô hại, nhưng **nếu đang có thợ làm việc trên khối đó thì phải hỏi trước**.

### Khoá một khối để sửa chữa

```sql
UPDATE block SET is_active = 0, tu_dong_ha_luc = NULL WHERE block_no = <số khối>;
```

Cột `tu_dong_ha_luc = NULL` là phần **quan trọng**: nó đánh dấu "người khoá", nên hệ thống tự động sẽ không mở lại. Nếu bỏ sót, khối có thể bị mở lại sau vài phút trong khi thợ vẫn đang làm.

Mở lại khi xong:

```sql
UPDATE block SET is_active = 1 WHERE block_no = <số khối>;
```

Không cần Reload — cờ này đọc trực tiếp từ cơ sở dữ liệu.

### Tắt gấp tính năng tự động

Sửa `Web.config` bản đang chạy:

```xml
<add key="plc:tuDongCongVanHanh" value="false" />
```

Có hiệu lực trong vòng một phút, không cần khởi động lại. Các khối đã bị hạ **vẫn nằm ở trạng thái hạ** — mở lại thủ công nếu cần:

```sql
UPDATE block SET is_active = 1, tu_dong_ha_luc = NULL WHERE is_active = 0;
```

---

## 6. Kiểm tra nhanh khi nghi có vấn đề

```sql
-- 1. Con so tong quan
SELECT (SELECT COUNT(*) FROM plc_device WHERE is_active = 1) AS dang_giam_sat,
       (SELECT COUNT(*) FROM plc_device WHERE is_connected = 0) AS plc_chet,
       (SELECT COUNT(*) FROM block WHERE is_active = 0)         AS khoi_khong_nhan_xe,
       (SELECT COUNT(*) FROM v_plc_lech_tang)                   AS khoi_lech;

-- 2. So lieu co dang dong bang khong
SELECT MAX(last_probe_at) AS quan_sat_gan_nhat FROM plc_device;
```

Nếu `quan_sat_gan_nhat` cũ hơn một phút → hệ thống có thể đã dừng, mọi số liệu trong bảng không còn đáng tin.

Nhật ký mọi lần hệ thống tự đổi cờ nằm ở `App_Data/plc_audit.log`, tìm chuỗi `CONG VAN HANH`.

---

## 7. Những điều chưa hoàn thiện

Ghi lại để người tiếp nhận biết, không phải lỗi cần sửa gấp:

1. **Khối vừa được đưa vào giám sát giữ nguyên giá trị cũ ở `D1000`** cho tới lượt quẹt thẻ đầu tiên. Tài xế tìm xe đầu tiên ở khối đó có thể đọc phải số của đợt kiểm tra trước.
2. **Độ phủ (`coverage_pct`) vẫn đếm cả khối đã tắt vận hành** — con số này hơi lạc quan hơn thực tế.
3. **Trần 4 lần đổi/ngày là con số ước lượng**, chưa hiệu chỉnh theo hành vi thật của bãi. Nếu thấy khối hay chạm trần mà phần cứng bình thường thì nên nâng lên.
