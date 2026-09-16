# Tổng hợp phân tích hệ thống đỗ xe tự động -- Lumi Hanoi

## 1. Phạm vi tài liệu

Tài liệu này tổng hợp kết quả đối chiếu hai bản vẽ:

-   `IP CẢM BIẾN ĐỖ THƯỜNG.pdf`
-   `IP bảng led dẫn hướng.pdf`

Hai file sử dụng **cùng mặt bằng bố trí hệ thống đỗ xe**, cùng hệ thống
đánh số Block và cùng phân vùng Zone. Điểm khác nhau chủ yếu nằm ở lớp
thông tin IP/tín hiệu được thể hiện trên bản vẽ.

> **Lưu ý:** File `IP bảng led dẫn hướng.pdf` phù hợp hơn để quan sát
> trực quan Zone và Block vì ít thông tin IP cảm biến hơn, nhưng không
> phải là một mặt bằng Block/Zone mới hay một bảng phân loại Block khác.

------------------------------------------------------------------------

## 2. Tổng số Block và số chỗ đỗ

Theo bảng thống kê số lượng chỗ đỗ ô tô PUZZLE trên bản vẽ:

  Loại Block                  Số bộ   Số chỗ/SUV   Tổng số chỗ
  ----------------------- --------- ------------ -------------
  BLOCK 3 SPACES-5000L            9            3            27
  BLOCK 5 SPACES-4800L            1            5             5
  BLOCK 5 SPACES-5000L           40            5           200
  BLOCK 6 SPACES-5000L           22            6           132
  BLOCK 10 SPACES-5000L          40           10           400
  **TỔNG**                  **112**                    **764**

### Tổng kết

-   **Tổng số Block:** 112
-   **Tổng số chỗ đỗ:** 764
-   **Tổng số Block 3 spaces:** 9
-   **Tổng số Block 5 spaces:** 41
    -   1 Block loại 4800L
    -   40 Block loại 5000L
-   **Tổng số Block 6 spaces:** 22
-   **Tổng số Block 10 spaces:** 40

------------------------------------------------------------------------

## 3. Phân chia Block theo Zone

Đối chiếu trực tiếp vị trí Zone và số Block trên mặt bằng, phân chia như
sau:

  Zone         Các Block                Số Block
  ------------ ---------------------- ----------
  **ZONE 1**   97--112                    **16**
  **ZONE 2**   60--96                     **37**
  **ZONE 3**   39--50                     **12**
  **ZONE 4**   1--8, 33--38, 51--59       **23**
  **ZONE 5**   9--24                      **16**
  **ZONE 6**   25--32                      **8**
  **TỔNG**     1--112                    **112**

### Kiểm tra tổng

``` text
ZONE 1 = 16
ZONE 2 = 37
ZONE 3 = 12
ZONE 4 = 23
ZONE 5 = 16
ZONE 6 =  8
----------------
TỔNG   = 112 BLOCK
```

------------------------------------------------------------------------

## 4. Danh sách Block theo từng Zone

### ZONE 1

-   Block 97
-   Block 98
-   Block 99
-   Block 100
-   Block 101
-   Block 102
-   Block 103
-   Block 104
-   Block 105
-   Block 106
-   Block 107
-   Block 108
-   Block 109
-   Block 110
-   Block 111
-   Block 112

**Tổng: 16 Block**

------------------------------------------------------------------------

### ZONE 2

-   Block 60
-   Block 61
-   Block 62
-   Block 63
-   Block 64
-   Block 65
-   Block 66
-   Block 67
-   Block 68
-   Block 69
-   Block 70
-   Block 71
-   Block 72
-   Block 73
-   Block 74
-   Block 75
-   Block 76
-   Block 77
-   Block 78
-   Block 79
-   Block 80
-   Block 81
-   Block 82
-   Block 83
-   Block 84
-   Block 85
-   Block 86
-   Block 87
-   Block 88
-   Block 89
-   Block 90
-   Block 91
-   Block 92
-   Block 93
-   Block 94
-   Block 95
-   Block 96

**Tổng: 37 Block**

> **Lưu ý quan trọng:** Block 78 và Block 79 thuộc **ZONE 2**, không
> chuyển sang ZONE 3.

------------------------------------------------------------------------

### ZONE 3

-   Block 39
-   Block 40
-   Block 41
-   Block 42
-   Block 43
-   Block 44
-   Block 45
-   Block 46
-   Block 47
-   Block 48
-   Block 49
-   Block 50

**Tổng: 12 Block**

------------------------------------------------------------------------

### ZONE 4

Zone 4 gồm ba nhóm Block:

**Nhóm 1** - Block 1--8

**Nhóm 2** - Block 33--38

**Nhóm 3** - Block 51--59

Danh sách đầy đủ:

1, 2, 3, 4, 5, 6, 7, 8,\
33, 34, 35, 36, 37, 38,\
51, 52, 53, 54, 55, 56, 57, 58, 59

**Tổng: 23 Block**

------------------------------------------------------------------------

### ZONE 5

-   Block 9
-   Block 10
-   Block 11
-   Block 12
-   Block 13
-   Block 14
-   Block 15
-   Block 16
-   Block 17
-   Block 18
-   Block 19
-   Block 20
-   Block 21
-   Block 22
-   Block 23
-   Block 24

**Tổng: 16 Block**

------------------------------------------------------------------------

### ZONE 6

-   Block 25
-   Block 26
-   Block 27
-   Block 28
-   Block 29
-   Block 30
-   Block 31
-   Block 32

**Tổng: 8 Block**

------------------------------------------------------------------------

## 5. Đối chiếu hai file PDF

  -----------------------------------------------------------------------
  Nội dung                IP CẢM BIẾN ĐỖ THƯỜNG   IP bảng led dẫn hướng
  ----------------------- ----------------------- -----------------------
  Mặt bằng                Giống                   Giống

  Đánh số Block           Giống                   Giống

  Phân chia Zone          Giống                   Giống

  Bảng thống kê Model     Có                      Có

  IP cảm biến đỗ xe       Có nhiều                Không phải lớp thông
                                                  tin chính

  IP bảng LED dẫn hướng   Không phải lớp thông    Có
                          tin chính               

  Đường CAT6              Có                      Có

  Khả năng quan sát       Bị nhiều IP cảm biến    **Dễ quan sát hơn**
  Block/Zone              che hơn                 
  -----------------------------------------------------------------------

### Kết luận đối chiếu

Hai file **không phải hai phương án mặt bằng khác nhau**.

Có thể hiểu đơn giản:

``` text
CÙNG MẶT BẰNG
     │
     ├── File 1: lớp IP CẢM BIẾN ĐỖ THƯỜNG
     │
     └── File 2: lớp IP BẢNG LED DẪN HƯỚNG
```

Vì vậy, khi xác định **Zone → Block**, có thể dùng
`IP bảng led dẫn hướng.pdf` để đọc trực quan hơn.

------------------------------------------------------------------------

## 6. Lưu ý về việc xác định loại Block

Bảng thống kê của bản vẽ xác nhận tổng số lượng từng loại Block:

-   9 Block × 3 spaces
-   1 Block × 5 spaces -- 4800L
-   40 Block × 5 spaces -- 5000L
-   22 Block × 6 spaces
-   40 Block × 10 spaces

Tuy nhiên, **chưa nên gán loại Model cụ thể cho từng Block 1--112 chỉ
dựa trên thứ tự số Block**.

Đặc biệt:

-   Không được mặc định Block 1--9 là 3 spaces, Block 10--50 là 5
    spaces... nếu chưa đối chiếu hình dạng thực tế trên bản vẽ.
-   Block 5 spaces có hai loại chiều dài: **4800L** và **5000L**.
-   Cần đối chiếu hình học/kích thước từng cụm Block trên bản vẽ để xác
    định chính xác loại.
-   Bảng Block → Model chi tiết trước đó không nên dùng làm dữ liệu
    chính thức nếu chưa được kiểm tra lại trực tiếp trên bản vẽ.

------------------------------------------------------------------------

## 7. Dữ liệu chuẩn hiện tại

Nếu dùng tài liệu này làm cơ sở cho việc xây dựng dữ liệu SCADA, hiện có
thể xem các thông tin sau là dữ liệu đã được tổng hợp:

### Zone → Block

``` text
ZONE 1 → 97–112
ZONE 2 → 60–96
ZONE 3 → 39–50
ZONE 4 → 1–8, 33–38, 51–59
ZONE 5 → 9–24
ZONE 6 → 25–32
```

### Tổng số

``` text
112 Block
764 chỗ đỗ
6 Zone
```

### Phân loại Model tổng thể

``` text
3 spaces-5000L  →   9 Block
5 spaces-4800L  →   1 Block
5 spaces-5000L  →  40 Block
6 spaces-5000L  →  22 Block
10 spaces-5000L →  40 Block
--------------------------------
TỔNG            → 112 Block
```

------------------------------------------------------------------------

## 8. Nguồn dữ liệu

Nguồn chính: bản vẽ `IP bảng led dẫn hướng.pdf`.

Bảng thống kê Model trong bản vẽ xác nhận 112 bộ cơ khí và 764 chỗ đỗ.
Phần phân chia Zone/Block được đối chiếu trực tiếp từ mặt bằng và số
Block hiển thị trên bản vẽ.

> **Khuyến nghị:** Nếu cần lập tiếp bảng chi tiết
> `Zone → Block → Model → số spaces → IP cảm biến → IP LED`, nên thực
> hiện bước đối chiếu trực tiếp từng Block trên bản vẽ để tránh suy đoán
> loại Block theo số thứ tự.
