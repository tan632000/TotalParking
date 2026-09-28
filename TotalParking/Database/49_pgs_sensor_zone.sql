-- =========================================================================
-- Ánh xạ cảm biến đỗ thường sang zone
-- =========================================================================
--
-- Mười một bảng LED chỉ hướng hiện số chỗ đỗ thường của zone mà mũi tên dẫn
-- tới, lấy từ v_led_capacity_zone.free_standard. Cột đó tính bằng
-- "tổng ô - số phiên gửi xe", mà khu đỗ thường không phát thẻ nên nó luôn bằng
-- nguyên sức chứa: mỗi mũi tên nói zone đó còn trống toàn bộ, kể cả khi đã đầy.
--
-- Migration 48 đã sửa con số TOÀN BÃI bằng cách đọc pgs_sensor_state. File này
-- làm nốt phần theo zone, và cần thêm thứ mà 48 chưa có: biết cảm biến nào
-- thuộc zone nào.
--
-- ===================== ÁNH XẠ NÀY LẤY TỪ ĐÂU =====================
--
-- docs/IP_normal_parking_sensor.pdf mang 79 chú thích dạng "Z0,2.7" kèm toạ độ,
-- đúng bằng số cảm biến đang lắp. Ba phép hiệu chỉnh trước khi dùng được:
--
--   1. LỆCH MỘT ĐƠN VỊ. Vị trí 1 của cả 10 lộ đều là "không lắp"; cảm biến thật
--      chạy từ vị trí 2. Vậy vi_tri = chỉ số bản vẽ + 1.
--
--   2. ZCU 2 ĐẢO HAI LỘ. Bản vẽ ghi lộ1=11 lộ2=7, hiện trường đọc lộ1=7 lộ2=11.
--      Người dùng xác nhận 28/09/2026 là đảo thật.
--
--   3. BẢN VẼ VẼ TRƯỚC KHI CÓ ZCU 4. Bảy địa chỉ mang nhãn Z3 bị trùng; bản sao
--      thứ hai thuộc ZCU 4. Năm nhãn lộ 2 phân định theo độ chụm (cách biệt
--      19-23%); hai nhãn lộ 1 theo tính liên tục của dãy RS485 - nhãn 1.3..1.6
--      chỉ xuất hiện một lần nên vị trí chắc chắn, chúng nằm thành hàng ngang
--      và cặp (933,674)/(957,672) nối tiếp đúng vào, cách nhãn gần nhất 22px
--      trong khi cặp kia cách 472px.
--
-- Sau ba phép đó, số cảm biến từng lộ khớp hiện trường ở CẢ MƯỜI lộ, chỉ số
-- liên tục từ 2, không còn địa chỉ trùng.
--
-- ===================== SỐ TỦ TRÊN BẢN VẼ KHÔNG LIÊN QUAN ID KHUNG ==========
--
-- Bản vẽ ghi năm tủ "ZCU 1".."ZCU 5", còn khung CCU đánh id 0..4. ĐỪNG suy ra
-- tủ n ứng với id n-1 — sai. Tương ứng thật, đo bằng cách cho mỗi nhãn cảm biến
-- bỏ phiếu cho tủ gần nó nhất:
--
--     tiền tố Z0  ->  tủ "ZCU 5"      (21/21 nhãn)
--     tiền tố Z1  ->  tủ "ZCU 3"
--     tiền tố Z2  ->  tủ "ZCU 1"      (18/18 nhãn)
--     tiền tố Z3  ->  tủ "ZCU 4"
--     id khung 4  ->  tủ "ZCU 2"      (tủ DUY NHẤT không có tiền tố nào)
--
-- Tiền tố Zn ỨNG ĐÚNG id khung n, xác nhận bằng dấu vân tay số cảm biến mỗi lộ:
-- Z0(7,14) Z1(4,13) Z3(6,10) khớp duy nhất với id0(7,14) id1(4,13) id3(6,10),
-- và Z2(11,7) khớp id2(7,11) sau khi đảo lộ.
--
-- ===================== BẰNG CHỨNG CHO HAI NHÃN LỘ 1 ==========
--
-- Ngoài tính liên tục của dãy, còn một phép đo độc lập: khoảng cách từ mỗi cảm
-- biến tới đường cáp CAT6 gần nhất (đường màu 0,0.5,0.25 trên bản vẽ). Cảm biến
-- trên cùng một chuỗi nằm song song với cùng một tuyến cáp nên khoảng cách của
-- chúng phải ĐỒNG ĐỀU:
--
--     Z3 lộ1 như đang gán:  113, 113, 114, 116, 118, 118   -> trải 5px
--     Z3 lộ1 nếu gán ngược:  19,  21, 113, 116, 118, 118   -> trải 99px
--
-- Khoảng cách tuyệt đối KHÔNG dùng để kết luận được — Z0 lộ1 cách cáp 350-404px
-- mà vẫn là một chuỗi hợp lệ. Chỉ độ đồng đều trong cùng một chuỗi mới có nghĩa.
--
-- Đã kiểm ngoài hiện trường 28/09/2026: giữa block 63 và 64 có đúng ba ô, cả ba
-- báo có xe, và người dùng đếm được đúng ba xe. Cụm đó thuộc ZCU 2 nên phép kiểm
-- này phủ cả hai phép hiệu chỉnh dễ sai nhất.
--
-- ===================== ZONE LẤY TỪ ĐÂU =====================
--
-- KHÔNG lấy từ nhãn "ZONE 1".."ZONE 6" trên bản vẽ: nhãn là một ĐIỂM còn zone
-- là một VÙNG, ghép theo nhãn gần nhất cho 17/21/2/12/10/17 - sai vì cảm biến ở
-- rìa bị hút sang nhãn gần hơn.
--
-- KHÔNG trích ranh giới zone từ lớp vector: 12_block_from_cad.sql đã thử và ghi
-- lại rằng bản vẽ CAD không hề vẽ ranh giới zone (đã kiểm cả 139 lớp).
--
-- Dùng 108 số hiệu block cơ khí làm mốc - chúng trải đều khắp mặt bằng và zone
-- của từng block là dữ liệu KHÁCH HÀNG cung cấp (14_zone_blocks_from_customer).
-- Mỗi cảm biến lấy zone theo bỏ phiếu của 3 block gần nhất, trọng số 1/khoảng
-- cách.
--
-- ===================== VÌ SAO SỬA LUÔN slot_count =====================
--
-- 14_zone_blocks_from_customer.sql tự ghi: "zone cua 6 cum do nen (901-906):
-- VAN LA SUY DOAN, file khach chi noi ve block co khi". Con số 13/18/2/17/11/19
-- chưa bao giờ được kiểm.
--
-- Đếm marker "1 LOTS" trên bản vẽ - mỗi marker là một ô đỗ thường - ra ĐÚNG 80,
-- khớp tổng cũ. Nhưng chia theo zone bằng cùng phép bỏ phiếu:
--
--        zone      1    2    3    4    5    6   tổng
--        ô vẽ     14   26    2   14   11   13    80
--        cảm biến 13   27    2   14   11   12    79
--        suy đoán 13   18    2   17   11   19    80
--
-- Hai phép đo ĐỘC LẬP - ô đỗ vẽ trên mặt bằng, và cảm biến thật ngoài hiện
-- trường - khớp nhau trong phạm vi 1 ô mỗi zone, và cùng lệch khỏi số suy đoán
-- tới 8 ô. Giữ số cũ thì zone 2 sẽ hiện "còn 26 trên tổng 18", một tỉ lệ vô lý.
--
-- Tổng vẫn là 80 nên sức chứa toàn bãi không đổi.

USE total_parking;

-- ------------------------------------------------------------------ ánh xạ
CREATE TABLE IF NOT EXISTS pgs_sensor_map (
    zcu_id  TINYINT UNSIGNED NOT NULL,
    lo      TINYINT UNSIGNED NOT NULL,
    vi_tri  TINYINT UNSIGNED NOT NULL,
    zone_id TINYINT UNSIGNED NOT NULL,

    -- Tỉ lệ phiếu của zone thắng trên tổng phiếu. Dưới 0.6 nghĩa là cảm biến
    -- nằm sát ranh giới và phép bỏ phiếu sát sao - 8 trong 79 cảm biến rơi vào
    -- nhóm đó. Giữ lại để lần sau ai rà soát biết bắt đầu từ đâu, thay vì phải
    -- tính lại từ đầu.
    do_chac DECIMAL(4,3) NOT NULL,

    -- Nhãn nguyên văn trên bản vẽ, để đối chiếu bằng mắt. KHÁC với ba cột trên
    -- ở cả số lộ (ZCU 2 đảo) lẫn chỉ số (lệch một đơn vị), nên phải giữ cả hai
    -- cách gọi chứ không suy ra được từ nhau nếu quên một phép.
    nhan_ban_ve VARCHAR(16) NOT NULL,

    PRIMARY KEY (zcu_id, lo, vi_tri),
    KEY ix_pgs_sensor_map_zone (zone_id),
    CONSTRAINT fk_pgs_sensor_map_zone FOREIGN KEY (zone_id) REFERENCES zone (zone_id)
) ENGINE = InnoDB;

DELETE FROM pgs_sensor_map;
INSERT INTO pgs_sensor_map (zcu_id, lo, vi_tri, zone_id, do_chac, nhan_ban_ve) VALUES
    (0,1, 2,2, 0.779, 'Z0,1.1'),
    (0,1, 3,1, 1.000, 'Z0,1.2'),
    (0,1, 4,1, 1.000, 'Z0,1.3'),
    (0,1, 5,1, 1.000, 'Z0,1.4'),
    (0,1, 6,1, 1.000, 'Z0,1.5'),
    (0,1, 7,1, 1.000, 'Z0,1.6'),
    (0,1, 8,1, 1.000, 'Z0,1.7'),
    (0,2, 2,2, 1.000, 'Z0,2.1'),
    (0,2, 3,2, 1.000, 'Z0,2.2'),
    (0,2, 4,2, 1.000, 'Z0,2.3'),
    (0,2, 5,2, 1.000, 'Z0,2.4'),
    (0,2, 6,2, 0.757, 'Z0,2.5'),
    (0,2, 7,2, 0.574, 'Z0,2.6'),
    (0,2, 8,1, 0.549, 'Z0,2.7'),
    (0,2, 9,2, 0.517, 'Z0,2.8'),
    (0,2,10,1, 0.725, 'Z0,2.9'),
    (0,2,11,1, 1.000, 'Z0,2.10'),
    (0,2,12,1, 1.000, 'Z0,2.11'),
    (0,2,13,1, 1.000, 'Z0,2.12'),
    (0,2,14,1, 1.000, 'Z0,2.13'),
    (0,2,15,1, 1.000, 'Z0,2.14'),
    (1,1, 2,4, 1.000, 'Z1,1.1'),
    (1,1, 3,4, 1.000, 'Z1,1.2'),
    (1,1, 4,4, 1.000, 'Z1,1.3'),
    (1,1, 5,4, 1.000, 'Z1,1.4'),
    (1,2, 2,4, 1.000, 'Z1,2.1'),
    (1,2, 3,4, 1.000, 'Z1,2.2'),
    (1,2, 4,5, 0.526, 'Z1,2.3'),
    (1,2, 5,5, 1.000, 'Z1,2.4'),
    (1,2, 6,5, 1.000, 'Z1,2.5'),
    (1,2, 7,5, 1.000, 'Z1,2.6'),
    (1,2, 8,5, 1.000, 'Z1,2.7'),
    (1,2, 9,5, 1.000, 'Z1,2.8'),
    (1,2,10,5, 1.000, 'Z1,2.9'),
    (1,2,11,5, 1.000, 'Z1,2.10'),
    (1,2,12,5, 1.000, 'Z1,2.11'),
    (1,2,13,5, 1.000, 'Z1,2.12'),
    (1,2,14,5, 1.000, 'Z1,2.13'),
    (2,1, 2,6, 1.000, 'Z2,2.1'),
    (2,1, 3,6, 1.000, 'Z2,2.2'),
    (2,1, 4,6, 1.000, 'Z2,2.3'),
    (2,1, 5,6, 1.000, 'Z2,2.4'),
    (2,1, 6,6, 1.000, 'Z2,2.5'),
    (2,1, 7,6, 0.785, 'Z2,2.6'),
    (2,1, 8,6, 1.000, 'Z2,2.7'),
    (2,2, 2,6, 0.746, 'Z2,1.1'),
    (2,2, 3,6, 0.742, 'Z2,1.2'),
    (2,2, 4,6, 0.756, 'Z2,1.3'),
    (2,2, 5,6, 0.746, 'Z2,1.4'),
    (2,2, 6,6, 0.731, 'Z2,1.5'),
    (2,2, 7,2, 0.549, 'Z2,1.6'),
    (2,2, 8,2, 0.593, 'Z2,1.7'),
    (2,2, 9,2, 0.731, 'Z2,1.8'),
    (2,2,10,2, 0.743, 'Z2,1.9'),
    (2,2,11,2, 1.000, 'Z2,1.10'),
    (2,2,12,2, 1.000, 'Z2,1.11'),
    (3,1, 2,4, 0.640, 'Z3,1.1'),
    (3,1, 3,4, 0.590, 'Z3,1.2'),
    (3,1, 4,4, 0.508, 'Z3,1.3'),
    (3,1, 5,2, 0.782, 'Z3,1.4'),
    (3,1, 6,2, 1.000, 'Z3,1.5'),
    (3,1, 7,2, 0.782, 'Z3,1.6'),
    (3,2, 2,2, 1.000, 'Z3,2.1'),
    (3,2, 3,2, 1.000, 'Z3,2.2'),
    (3,2, 4,2, 1.000, 'Z3,2.3'),
    (3,2, 5,2, 1.000, 'Z3,2.4'),
    (3,2, 6,2, 1.000, 'Z3,2.5'),
    (3,2, 7,2, 1.000, 'Z3,2.6'),
    (3,2, 8,2, 1.000, 'Z3,2.7'),
    (3,2, 9,2, 1.000, 'Z3,2.8'),
    (3,2,10,2, 1.000, 'Z3,2.9'),
    (3,2,11,2, 1.000, 'Z3,2.10'),
    (4,1, 2,3, 1.000, 'Z3,1.1'),
    (4,1, 3,3, 1.000, 'Z3,1.2'),
    (4,2, 2,4, 1.000, 'Z3,2.1'),
    (4,2, 3,4, 1.000, 'Z3,2.2'),
    (4,2, 4,4, 1.000, 'Z3,2.3'),
    (4,2, 5,4, 1.000, 'Z3,2.4'),
    (4,2, 6,4, 1.000, 'Z3,2.5');

-- ------------------------------------------------------------------ sức chứa
-- Theo số ô "1 LOTS" đếm được trên bản vẽ. Tổng giữ nguyên 80.
UPDATE block SET slot_count = 14 WHERE block_no = 901;   -- zone 1, cũ 13
UPDATE block SET slot_count = 26 WHERE block_no = 902;   -- zone 2, cũ 18
UPDATE block SET slot_count =  2 WHERE block_no = 903;   -- zone 3, không đổi
UPDATE block SET slot_count = 14 WHERE block_no = 904;   -- zone 4, cũ 17
UPDATE block SET slot_count = 11 WHERE block_no = 905;   -- zone 5, không đổi
UPDATE block SET slot_count = 13 WHERE block_no = 906;   -- zone 6, cũ 19


-- ------------------------------------------------------------------ view theo zone
-- Số ô đỗ thường còn trống của TỪNG zone, đọc từ cảm biến.
--
-- Cùng quy ước với v_pgs_standard ở migration 48: chỉ trang_thai = 0 là trống,
-- ô lỗi và ô không lắp không tính; ngưỡng tươi 5 phút.
CREATE OR REPLACE VIEW v_pgs_standard_zone AS
SELECT m.zone_id,
       COUNT(*)                                     AS tong_cam_bien,
       SUM(s.trang_thai = 0)                        AS trong,
       SUM(s.trang_thai = 1)                        AS co_xe,
       SUM(s.read_at >= NOW(3) - INTERVAL 5 MINUTE) AS con_tuoi
FROM   pgs_sensor_map m
JOIN   pgs_sensor_state s
       ON s.zcu_id = m.zcu_id AND s.lo = m.lo AND s.vi_tri = m.vi_tri
WHERE  s.trang_thai <> 3
GROUP  BY m.zone_id;


-- Hai dòng cơ khí giữ NGUYÊN VĂN từ 30_led_car_length_nested.sql. Chỉ nhánh
-- Ground đổi nguồn. Cùng cách dự phòng như migration 48: chưa có số liệu tươi
-- thì trả về sức chứa chứ KHÔNG trả về 0.
CREATE OR REPLACE VIEW v_led_capacity_zone AS
SELECT z.zone_id,
       t.total_l5m, t.total_l48m, t.total_standard,
       u.used_l5m,  u.used_l48m,
       COALESCE(g.co_xe, 0) AS used_standard,
       GREATEST(CAST(t.total_l5m  AS SIGNED) - u.used_l5m,  0) AS free_l5m,
       GREATEST(CAST(t.total_l48m AS SIGNED) - u.used_l48m, 0) AS free_l48m,
       -- LEAST kep so o trong theo suc chua cua zone. Zone 2 dang co 27 cam
       -- bien tren 26 o: ranh gioi giua zone 1 va zone 2 lech mot o giua hai
       -- phep dem (o ve tren ban ve, va cam bien that). Khong kep thi lúc ca 27
       -- o cung trong, bang LED se hien "27 tren tong 26" — nguoi doc mat niem
       -- tin vao ca ba con so chu khong chi con so do.
       LEAST(
           CASE WHEN g.con_tuoi IS NULL OR g.con_tuoi = 0
                THEN t.total_standard
                ELSE g.trong
           END,
           t.total_standard
       ) AS free_standard,
       COALESCE(g.tong_cam_bien, 0) AS standard_sensors,
       COALESCE(g.con_tuoi, 0)      AS standard_fresh,
       COALESCE(cv.o_co_khi,  0) AS slots_total,
       COALESCE(cv.o_vua_doc, 0) AS slots_fresh
FROM   zone z
JOIN LATERAL (
    SELECT
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN b.slot_count END),0) AS total_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN b.slot_count END),0) AS total_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                THEN b.slot_count END),0) AS total_standard
    FROM block b WHERE b.zone_id = z.zone_id AND b.is_active = 1
) t ON TRUE
JOIN LATERAL (
    SELECT
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN 1 END),0) AS used_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN 1 END),0) AS used_l48m
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.zone_id = z.zone_id AND b.is_active = 1
) u ON TRUE
LEFT   JOIN v_pgs_standard_zone g ON g.zone_id = z.zone_id
LEFT   JOIN v_zone_coverage     cv ON cv.zone_id = z.zone_id
WHERE  z.is_active = 1;


-- ------------------------------------------------------------------ đối chiếu
SELECT zone_id, total_standard, used_standard, free_standard,
       standard_sensors, standard_fresh
FROM   v_led_capacity_zone ORDER BY zone_id;
