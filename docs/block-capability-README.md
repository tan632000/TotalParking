# Bảng năng lực block — hướng dẫn điền

Điền vào `block-capability-template.csv`. Đây là dữ liệu khảo sát hiện trường,
không suy ra được từ phần mềm. Thiếu nó thì không triển khai được phần chọn block.

## Ý nghĩa từng cột

| Cột | Bắt buộc | Ý nghĩa |
|---|---|---|
| `block_id` | có | đã điền sẵn, không sửa |
| `zone_id` | **cần xác nhận** | đang lấy theo mock trong code — xem mục "Cần xác nhận" |
| `kind` | có | `Mechanical` (pallet cơ khí) hoặc `Ground` (đỗ nền) |
| `slot_count` | có | số ô **thật**. Code đang giả định 12 cho cả 18 block |
| `max_length_mm` | có với Mechanical | chiều dài xe tối đa khoang nhận được |
| `max_width_mm` | có với Mechanical | chiều rộng tối đa. Ghi rõ ở cột `notes` là **có tính gương hay không** |
| `max_height_mm` | có với Mechanical | chiều cao tối đa — thường là chiều hẹp nhất |
| `pallet_rating_kg` | có với Mechanical | tải trọng nâng, ví dụ `2200` hoặc `2600` |
| `rating_uniform` | có với Mechanical | `Y` nếu **mọi ô trong block** cùng tải trọng và cùng kích thước; `N` nếu khác nhau |
| `led_panel_id` | có | bảng LED nào chỉ đường tới block này |
| `notes` | không | ghi chú tự do |

## Cột quan trọng nhất: `rating_uniform`

Hệ pallet xếp tầng thường có sức nâng khác nhau giữa tầng dưới và tầng trên.

- **Tất cả `Y`** → cấu hình ở mức block, 18 dòng là đủ.
- **Có bất kỳ `N`** → phải khai báo tới mức **từng ô**, 216 dòng, và thuật toán chọn
  phải chọn ô theo năng lực chứ không phải "ô trống có chỉ số nhỏ nhất".

Câu trả lời này đổi cả cấu trúc dữ liệu lẫn thuật toán, nên cần trước khi viết code.

Cách nhận biết nhanh: nếu trong một block mọi pallet cùng một mã sản phẩm thì `Y`;
nếu tầng trệt dùng pallet khoẻ hơn để nhận xe nặng thì `N`.

## Cần xác nhận: bản đồ zone ↔ block

Code hiện gán như sau, nhưng đây là dữ liệu **mock**, chưa ai xác nhận:

| Zone | Block |
|---|---|
| 1 | A-01, A-02, A-03 |
| 2 | A-04, B-01, B-02 |
| 3 | B-03, B-04, C-01 |
| 4 | C-02, C-03, C-04 |
| 5 | D-01, D-02, D-03 |
| 6 | E-01, E-02, E-03 |

Điểm đáng ngờ: các cụm chữ cái **cắt ngang** ranh giới zone — zone 2 gồm A-04 nằm
cùng B-01 và B-02. Nếu chữ cái là cụm vật lý thật thì cách chia zone này khả năng
cao là bịa ra lúc dựng giao diện.

Việc này ảnh hưởng hai chỗ: cân bằng tải theo zone (tiêu chí xếp hạng chính khi
chọn block) và việc mỗi bảng LED trong hầm phục vụ zone nào.

## Còn cần thêm, ngoài file này

1. **Khoảng cách từ cổng vào tới mỗi zone** — tiêu chí xếp hạng khi nhiều block
   cùng phù hợp. Chỉ cần thứ tự gần/xa, không cần số mét chính xác.
2. **Danh mục bảng LED**: mỗi bảng gồm IP mạch điều khiển, chỉ số cổng (0–3),
   hướng mũi tên đã lắp, loại module màu, số chữ số hiển thị.
3. **Đồ thị dẫn đường**: mỗi bảng LED, mỗi hướng mũi tên dẫn tới tập block nào.
