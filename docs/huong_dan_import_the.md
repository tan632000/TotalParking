# Mẫu file nhập thẻ xe — hệ thống TotalParking

Hai file đi kèm:

- `mau_import_the.csv` — có sẵn 5 dòng ví dụ, dùng để đối chiếu định dạng
- `mau_import_the_trong.csv` — bản trắng, điền trực tiếp

Định dạng: **CSV, UTF-8 có BOM** (mở bằng Excel là hiện đúng tiếng Việt). Giữ nguyên
dòng tiêu đề, không đổi tên cột, không đổi thứ tự cột.

## Bốn cột bắt buộc

| Cột | Ý nghĩa | Giá trị hợp lệ |
|---|---|---|
| `ma_the` | Mã định danh mà đầu đọc RFID đọc được từ thẻ | Đúng 8 ký tự hex, ví dụ `61d41330` |
| `so_the` | Số in trên mặt thẻ | Không trùng nhau, ví dụ `S.06575` |
| `loai_khach` | Nhóm khách | `VANG` / `XT` / `GHI` |
| `hang_tai` | Hạng tải trọng của xe | `THUONG` / `2200KG` / `2600KG` |

**`loai_khach`**
- `VANG` — xe vãng lai
- `XT` — xe thẻ tháng
- `GHI` — nhóm OTO GHI

**`hang_tai`** quyết định xe được đưa vào loại chỗ đỗ nào:
- `2200KG` — pallet tải trọng 2200 kg, vào được mọi tầng
- `2600KG` — pallet tải trọng 2600 kg, **chỉ vào được tầng dưới cùng**
- `THUONG` — vượt tải pallet, chỉ đỗ nền

> Gán nhầm `2200KG` cho xe thực tế nặng 2600 kg sẽ đưa xe lên pallet tầng trên.
> Nếu không chắc hạng tải của một xe, để trống dòng đó và ghi chú lại, đừng đoán.

## Hai cột tuỳ chọn

| Cột | Nếu để trống |
|---|---|
| `lo_nhap` | Tự đặt theo tên file + ngày nhập. Dùng để lọc hoặc gỡ cả lô về sau |
| `kich_hoat` | Mặc định `1`. Đặt `0` nếu muốn nạp trước nhưng chưa cho dùng |

## Hai cột chỉ để tham khảo

`bien_so` và `chu_xe` **sẽ không được lưu** — hệ thống hiện chưa có chỗ chứa thông tin
này. Vẫn nên điền để đối chiếu bằng mắt khi kiểm tra file, nhưng nạp xong sẽ mất.

Cột hạn thẻ cũng chưa có chỗ lưu, nên tạm thời không đưa vào mẫu.

## Điều quan trọng nhất: KHÔNG dán biển số vào ô mã thẻ

Đây là lỗi phổ biến nhất. Trong file gửi ngày 17/09, **12 trên 38 dòng** có biển số nằm
ở ô mã định danh.

Sáu dòng phát hiện được ngay vì biển số chứa chữ cái không phải hex:

```
ma_the = 30K78831     (chữ K không phải ký tự hex)
```

Nhưng sáu dòng còn lại **toàn ký tự hex nên trông y hệt mã thẻ thật**:

```
SAI   ma_the = 30E83499   bien_so = 30E83499     <- đây là biển số
ĐÚNG  ma_the = A0FF0790   bien_so = 30M03135     <- đây là mã thẻ
```

Cách nhận biết: **mã thẻ không bao giờ trùng với biển số của chính xe đó**. Mã thẻ phải
lấy từ phần mềm quản lý thẻ của toà nhà, không phải từ biển số.

## Dòng bị bỏ qua

Mỗi dòng phải qua bảy điều kiện. Dòng nào trượt sẽ bị bỏ và hệ thống báo rõ lý do,
các dòng còn lại vẫn nạp bình thường.

1. `ma_the` đúng 8 ký tự hex
2. `ma_the` khác biển số của chính dòng đó
3. `ma_the` có giá trị lớn hơn 65535
4. `ma_the` không trùng trong file và chưa tồn tại trong hệ thống
5. `so_the` không trùng
6. `loai_khach` thuộc ba mã hợp lệ
7. `hang_tai` thuộc ba mã hợp lệ
