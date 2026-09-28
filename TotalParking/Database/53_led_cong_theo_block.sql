-- =========================================================================
-- Cổng LED bám BLOCK và CẢM BIẾN, thôi bám zone
-- =========================================================================
--
-- Nguồn: docs/LumiSlotsMatrix.xlsx — khách trả lời mỗi cổng LED phục vụ những
-- block nào và những cảm biến đỗ thường nào. Đọc và đối chiếu bằng
-- tools/doc_ma_tran_led.py (thoát 0 nghĩa là bảng tự nhất quán).
--
-- ===================== VÌ SAO PHẢI BỎ MÔ HÌNH ZONE =====================
--
-- led_panel_port.zone_list cho mỗi mũi tên một danh sách zone, và
-- LedPublisher cộng sức chứa của các zone đó. Câu trả lời của khách KHÔNG
-- xếp được vào mô hình ấy: cổng 51-P phục vụ block 11..15 và 21..28, vắt qua
-- nhiều zone. Đo độ giống giữa tập block của từng cổng và tập block của từng
-- zone thì cao nhất chỉ 0.85 — KHÔNG cổng nào bằng đúng một zone, dù xét theo
-- cách chia zone cũ hay theo CL1.
--
-- Hệ quả tốt: sau file này, bảng LED KHÔNG còn phụ thuộc vào việc chia zone
-- đúng hay sai. Cổng đọc thẳng block và cảm biến.
--
-- ===================== HAI PHÉP CHỈNH KHI ĐỌC TÀI LIỆU =====================
--
-- 1. LỆCH +1. Tài liệu đánh cảm biến từ 1; pgs_sensor_map.vi_tri là số kênh,
--    chạy từ 2. Đo được ở cả 10 nhóm (zcu, lô): min = 2, max = n+1.
-- 2. ZCU 2 ĐẢO LÔ. Nhãn bản vẽ ghi lô 1 có 11 cảm biến, lô 2 có 7; đấu dây
--    thật thì ngược lại.
--
-- Áp cả hai thì 79/79 cảm biến của tài liệu phân giải đúng vào pgs_sensor_map,
-- không thiếu không thừa — ba nguồn độc lập cùng chỉ một chỗ.
--
-- ===================== BLOCK 63..69 CỐ Ý KHÔNG CÓ CỔNG =====================
--
-- Bảy khối này (55 ô, 7,2% sức chứa cơ khí) không cổng LED nào phục vụ. Đây
-- KHÔNG phải thiếu sót: xe vào chúng từ ram dốc, lối đó không đặt bảng LED
-- (khách xác nhận 28/09/2026; ram dưới chỉ vào, ram trên chỉ ra). Mọi cảm biến
-- nằm cạnh bảy khối đó đều đã thuộc cổng khác (54-Th, 65-Tr, 65-P, 57-Tr), và
-- không cổng nào trong số đó chứa khối 63..69.
--
-- Cần quyền DDL (tài khoản totalparking không có) -> chạy bằng root.


-- --------------------------------------------------------------- lược đồ
-- scope nhận thêm 'BLOCKS'. Giữ nguyên 'TOTAL' (bảng tổng đầu hầm) và 'ZONES'
-- để đường cũ không gãy khi file này chỉ chạy một phần.
ALTER TABLE led_panel_port DROP CHECK ck_led_scope;
ALTER TABLE led_panel_port
  ADD CONSTRAINT ck_led_scope CHECK (scope IN ('TOTAL', 'ZONES', 'BLOCKS'));

CREATE TABLE IF NOT EXISTS led_port_block (
    panel_id   SMALLINT UNSIGNED NOT NULL,
    port_index TINYINT  UNSIGNED NOT NULL,
    block_id   SMALLINT UNSIGNED NOT NULL,
    PRIMARY KEY (panel_id, port_index, block_id),
    KEY ix_lpb_block (block_id),
    CONSTRAINT fk_lpb_port  FOREIGN KEY (panel_id, port_index)
        REFERENCES led_panel_port (panel_id, port_index) ON DELETE CASCADE,
    CONSTRAINT fk_lpb_block FOREIGN KEY (block_id) REFERENCES block (block_id)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS led_port_sensor (
    panel_id   SMALLINT UNSIGNED NOT NULL,
    port_index TINYINT  UNSIGNED NOT NULL,
    zcu_id     TINYINT  UNSIGNED NOT NULL,
    lo         TINYINT  UNSIGNED NOT NULL,
    vi_tri     TINYINT  UNSIGNED NOT NULL,
    PRIMARY KEY (panel_id, port_index, zcu_id, lo, vi_tri),
    KEY ix_lps_sensor (zcu_id, lo, vi_tri),
    CONSTRAINT fk_lps_port   FOREIGN KEY (panel_id, port_index)
        REFERENCES led_panel_port (panel_id, port_index) ON DELETE CASCADE,
    -- Khoá ngoại này là lá chắn: gieo nhầm một cảm biến không tồn tại sẽ hỏng
    -- ngay tại đây thay vì âm thầm làm bảng LED thiếu một ô.
    CONSTRAINT fk_lps_sensor FOREIGN KEY (zcu_id, lo, vi_tri)
        REFERENCES pgs_sensor_map (zcu_id, lo, vi_tri)
) ENGINE = InnoDB;


-- ------------------------------------------------------- sửa hướng bảng 52
-- Khách ghi bảng 52 có một mũi tên TRÁI; cơ sở dữ liệu đang để LÊN.
-- 10/11 bảng còn lại đã khớp sẵn. Phải sửa TRƯỚC khi gieo, vì các câu gieo
-- bên dưới tìm cổng theo (mã bảng, arrow_direction).
UPDATE led_panel_port o
  JOIN led_panel p ON p.panel_id = o.panel_id
   SET o.arrow_direction = 3            -- 0 lên, 1 phải, 2 xuống, 3 trái
 WHERE p.code = '52';

-- Trong mỗi bảng, không có hai cổng nào trùng hướng, nên (mã bảng, hướng) là
-- khoá duy nhất. Kiểm điều đó ngay: nếu sai, các câu gieo sẽ nhân đôi dòng.
SELECT p.code, o.arrow_direction, COUNT(*) AS so_cong_trung_huong
FROM   led_panel p JOIN led_panel_port o ON o.panel_id = p.panel_id
WHERE  p.kind = 'DIRECTIONAL'
GROUP  BY p.code, o.arrow_direction HAVING COUNT(*) > 1;


-- ------------------------------------------------------------------ gieo
-- Chạy lại được: xoá sạch rồi gieo lại, nên không sinh dòng thừa.
DELETE FROM led_port_block;
DELETE FROM led_port_sensor;

-- ---- bang 52, mui ten trai (Tr)  (tai lieu ghi: block 1 ~ 8)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (1, 2, 3, 4, 5, 6, 7, 8)
WHERE  p.code = '52';
-- cam bien do thuong, tai lieu ghi: Z1.1.1~4,  Z1.2.1~3
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 1 AS z, 1 AS l, 2 AS i UNION ALL SELECT 1,1,3 UNION ALL SELECT 1,1,4 UNION ALL SELECT 1,1,5 UNION ALL SELECT 1,2,2 UNION ALL SELECT 1,2,3 UNION ALL SELECT 1,2,4) v
WHERE  p.code = '52';

-- ---- bang 58, mui ten len (Th - thang)  (tai lieu ghi: block 11,12,14,15)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (11, 12, 14, 15)
WHERE  p.code = '58';
-- cam bien do thuong, tai lieu ghi: Z1.2.4~8
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 1 AS z, 2 AS l, 5 AS i UNION ALL SELECT 1,2,6 UNION ALL SELECT 1,2,7 UNION ALL SELECT 1,2,8 UNION ALL SELECT 1,2,9) v
WHERE  p.code = '58';

-- ---- bang 58, mui ten phai (P)  (tai lieu ghi: block 13,24)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (13, 24)
WHERE  p.code = '58';
-- cam bien do thuong, tai lieu ghi: Z1.2.9~13
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 1 AS z, 2 AS l, 10 AS i UNION ALL SELECT 1,2,11 UNION ALL SELECT 1,2,12 UNION ALL SELECT 1,2,13 UNION ALL SELECT 1,2,14) v
WHERE  p.code = '58';

-- ---- bang 53, mui ten trai (Tr)  (tai lieu ghi: block 11,12,14,15,13,24)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (11, 12, 13, 14, 15, 24)
WHERE  p.code = '53';
-- cam bien do thuong, tai lieu ghi: Z1.2.4~13
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 1 AS z, 2 AS l, 5 AS i UNION ALL SELECT 1,2,6 UNION ALL SELECT 1,2,7 UNION ALL SELECT 1,2,8 UNION ALL SELECT 1,2,9 UNION ALL SELECT 1,2,10 UNION ALL SELECT 1,2,11 UNION ALL SELECT 1,2,12 UNION ALL SELECT 1,2,13 UNION ALL SELECT 1,2,14) v
WHERE  p.code = '53';

-- ---- bang 53, mui ten len (Th - thang)  (tai lieu ghi: block 21,22,23)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (21, 22, 23)
WHERE  p.code = '53';
-- KHONG co cam bien do thuong: huong nay khong nhin thay o do nen nao.
-- Day la cong DUY NHAT nhu vay trong 21 cong.

-- ---- bang 53, mui ten phai (P)  (tai lieu ghi: block 25,26,27,28)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (25, 26, 27, 28)
WHERE  p.code = '53';
-- cam bien do thuong, tai lieu ghi: Z2.2.1~7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 2 AS z, 1 AS l, 2 AS i UNION ALL SELECT 2,1,3 UNION ALL SELECT 2,1,4 UNION ALL SELECT 2,1,5 UNION ALL SELECT 2,1,6 UNION ALL SELECT 2,1,7 UNION ALL SELECT 2,1,8) v
WHERE  p.code = '53';

-- ---- bang 55, mui ten len (Th - thang)  (tai lieu ghi: block 31~54)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54)
WHERE  p.code = '55';
-- cam bien do thuong, tai lieu ghi: Z4
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 4 AS z, 1 AS l, 2 AS i UNION ALL SELECT 4,1,3 UNION ALL SELECT 4,2,2 UNION ALL SELECT 4,2,3 UNION ALL SELECT 4,2,4 UNION ALL SELECT 4,2,5 UNION ALL SELECT 4,2,6) v
WHERE  p.code = '55';

-- ---- bang 55, mui ten phai (P)  (tai lieu ghi: block 1~10,16~20)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 16, 17, 18, 19, 20)
WHERE  p.code = '55';
-- cam bien do thuong, tai lieu ghi: Z1.1.1~4,  Z1.2.1~4
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 1 AS z, 1 AS l, 2 AS i UNION ALL SELECT 1,1,3 UNION ALL SELECT 1,1,4 UNION ALL SELECT 1,1,5 UNION ALL SELECT 1,2,2 UNION ALL SELECT 1,2,3 UNION ALL SELECT 1,2,4 UNION ALL SELECT 1,2,5) v
WHERE  p.code = '55';

-- ---- bang 56, mui ten len (Th - thang)  (tai lieu ghi: block 38~49)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49)
WHERE  p.code = '56';
-- cam bien do thuong, tai lieu ghi: Z4.1.1~2
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 4 AS z, 1 AS l, 2 AS i UNION ALL SELECT 4,1,3) v
WHERE  p.code = '56';

-- ---- bang 51, mui ten trai (Tr)  (tai lieu ghi: block 1~10,16~20,30~54)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 16, 17, 18, 19, 20, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54)
WHERE  p.code = '51';
-- cam bien do thuong, tai lieu ghi: Z1.1.1~4,  Z1.2.1~4, Z4
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 1 AS z, 1 AS l, 2 AS i UNION ALL SELECT 1,1,3 UNION ALL SELECT 1,1,4 UNION ALL SELECT 1,1,5 UNION ALL SELECT 1,2,2 UNION ALL SELECT 1,2,3 UNION ALL SELECT 1,2,4 UNION ALL SELECT 1,2,5 UNION ALL SELECT 4,1,2 UNION ALL SELECT 4,1,3 UNION ALL SELECT 4,2,2 UNION ALL SELECT 4,2,3 UNION ALL SELECT 4,2,4 UNION ALL SELECT 4,2,5 UNION ALL SELECT 4,2,6) v
WHERE  p.code = '51';

-- ---- bang 51, mui ten phai (P)  (tai lieu ghi: block 11~15,21~28)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (11, 12, 13, 14, 15, 21, 22, 23, 24, 25, 26, 27, 28)
WHERE  p.code = '51';
-- cam bien do thuong, tai lieu ghi: Z1.2.4~13, Z2.2.1~7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 1 AS z, 2 AS l, 5 AS i UNION ALL SELECT 1,2,6 UNION ALL SELECT 1,2,7 UNION ALL SELECT 1,2,8 UNION ALL SELECT 1,2,9 UNION ALL SELECT 1,2,10 UNION ALL SELECT 1,2,11 UNION ALL SELECT 1,2,12 UNION ALL SELECT 1,2,13 UNION ALL SELECT 1,2,14 UNION ALL SELECT 2,1,2 UNION ALL SELECT 2,1,3 UNION ALL SELECT 2,1,4 UNION ALL SELECT 2,1,5 UNION ALL SELECT 2,1,6 UNION ALL SELECT 2,1,7 UNION ALL SELECT 2,1,8) v
WHERE  p.code = '51';

-- ---- bang 54, mui ten len (Th - thang)  (tai lieu ghi: block 1~61)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61)
WHERE  p.code = '54';
-- cam bien do thuong, tai lieu ghi: Z1, Z4, Z2, Z3.1.1~6
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 1 AS z, 1 AS l, 2 AS i UNION ALL SELECT 1,1,3 UNION ALL SELECT 1,1,4 UNION ALL SELECT 1,1,5 UNION ALL SELECT 1,2,2 UNION ALL SELECT 1,2,3 UNION ALL SELECT 1,2,4 UNION ALL SELECT 1,2,5 UNION ALL SELECT 1,2,6 UNION ALL SELECT 1,2,7 UNION ALL SELECT 1,2,8 UNION ALL SELECT 1,2,9 UNION ALL SELECT 1,2,10 UNION ALL SELECT 1,2,11 UNION ALL SELECT 1,2,12 UNION ALL SELECT 1,2,13 UNION ALL SELECT 1,2,14 UNION ALL SELECT 2,1,2 UNION ALL SELECT 2,1,3 UNION ALL SELECT 2,1,4 UNION ALL SELECT 2,1,5 UNION ALL SELECT 2,1,6 UNION ALL SELECT 2,1,7 UNION ALL SELECT 2,1,8 UNION ALL SELECT 2,2,2 UNION ALL SELECT 2,2,3 UNION ALL SELECT 2,2,4 UNION ALL SELECT 2,2,5 UNION ALL SELECT 2,2,6 UNION ALL SELECT 2,2,7 UNION ALL SELECT 2,2,8 UNION ALL SELECT 2,2,9 UNION ALL SELECT 2,2,10 UNION ALL SELECT 2,2,11 UNION ALL SELECT 2,2,12 UNION ALL SELECT 3,1,2 UNION ALL SELECT 3,1,3 UNION ALL SELECT 3,1,4 UNION ALL SELECT 3,1,5 UNION ALL SELECT 3,1,6 UNION ALL SELECT 3,1,7 UNION ALL SELECT 4,1,2 UNION ALL SELECT 4,1,3 UNION ALL SELECT 4,2,2 UNION ALL SELECT 4,2,3 UNION ALL SELECT 4,2,4 UNION ALL SELECT 4,2,5 UNION ALL SELECT 4,2,6) v
WHERE  p.code = '54';

-- ---- bang 65, mui ten trai (Tr)  (tai lieu ghi: block 1~62)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62)
WHERE  p.code = '65';
-- cam bien do thuong, tai lieu ghi: Z1, Z4, Z2, Z3.1.1~6
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 1 AS z, 1 AS l, 2 AS i UNION ALL SELECT 1,1,3 UNION ALL SELECT 1,1,4 UNION ALL SELECT 1,1,5 UNION ALL SELECT 1,2,2 UNION ALL SELECT 1,2,3 UNION ALL SELECT 1,2,4 UNION ALL SELECT 1,2,5 UNION ALL SELECT 1,2,6 UNION ALL SELECT 1,2,7 UNION ALL SELECT 1,2,8 UNION ALL SELECT 1,2,9 UNION ALL SELECT 1,2,10 UNION ALL SELECT 1,2,11 UNION ALL SELECT 1,2,12 UNION ALL SELECT 1,2,13 UNION ALL SELECT 1,2,14 UNION ALL SELECT 2,1,2 UNION ALL SELECT 2,1,3 UNION ALL SELECT 2,1,4 UNION ALL SELECT 2,1,5 UNION ALL SELECT 2,1,6 UNION ALL SELECT 2,1,7 UNION ALL SELECT 2,1,8 UNION ALL SELECT 2,2,2 UNION ALL SELECT 2,2,3 UNION ALL SELECT 2,2,4 UNION ALL SELECT 2,2,5 UNION ALL SELECT 2,2,6 UNION ALL SELECT 2,2,7 UNION ALL SELECT 2,2,8 UNION ALL SELECT 2,2,9 UNION ALL SELECT 2,2,10 UNION ALL SELECT 2,2,11 UNION ALL SELECT 2,2,12 UNION ALL SELECT 3,1,2 UNION ALL SELECT 3,1,3 UNION ALL SELECT 3,1,4 UNION ALL SELECT 3,1,5 UNION ALL SELECT 3,1,6 UNION ALL SELECT 3,1,7 UNION ALL SELECT 4,1,2 UNION ALL SELECT 4,1,3 UNION ALL SELECT 4,2,2 UNION ALL SELECT 4,2,3 UNION ALL SELECT 4,2,4 UNION ALL SELECT 4,2,5 UNION ALL SELECT 4,2,6) v
WHERE  p.code = '65';

-- ---- bang 65, mui ten len (Th - thang)  (tai lieu ghi: block 70~84)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84)
WHERE  p.code = '65';
-- cam bien do thuong, tai lieu ghi: Z3.2.1~7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 3 AS z, 2 AS l, 2 AS i UNION ALL SELECT 3,2,3 UNION ALL SELECT 3,2,4 UNION ALL SELECT 3,2,5 UNION ALL SELECT 3,2,6 UNION ALL SELECT 3,2,7 UNION ALL SELECT 3,2,8) v
WHERE  p.code = '65';

-- ---- bang 65, mui ten phai (P)  (tai lieu ghi: block 85~112)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112)
WHERE  p.code = '65';
-- cam bien do thuong, tai lieu ghi: Z0, Z3.2.8~10
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 0 AS z, 1 AS l, 2 AS i UNION ALL SELECT 0,1,3 UNION ALL SELECT 0,1,4 UNION ALL SELECT 0,1,5 UNION ALL SELECT 0,1,6 UNION ALL SELECT 0,1,7 UNION ALL SELECT 0,1,8 UNION ALL SELECT 0,2,2 UNION ALL SELECT 0,2,3 UNION ALL SELECT 0,2,4 UNION ALL SELECT 0,2,5 UNION ALL SELECT 0,2,6 UNION ALL SELECT 0,2,7 UNION ALL SELECT 0,2,8 UNION ALL SELECT 0,2,9 UNION ALL SELECT 0,2,10 UNION ALL SELECT 0,2,11 UNION ALL SELECT 0,2,12 UNION ALL SELECT 0,2,13 UNION ALL SELECT 0,2,14 UNION ALL SELECT 0,2,15 UNION ALL SELECT 3,2,9 UNION ALL SELECT 3,2,10 UNION ALL SELECT 3,2,11) v
WHERE  p.code = '65';

-- ---- bang 67, mui ten len (Th - thang)  (tai lieu ghi: block 76~79)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (76, 77, 78, 79)
WHERE  p.code = '67';
-- cam bien do thuong, tai lieu ghi: Z3.2.1~4
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 3 AS z, 2 AS l, 2 AS i UNION ALL SELECT 3,2,3 UNION ALL SELECT 3,2,4 UNION ALL SELECT 3,2,5) v
WHERE  p.code = '67';

-- ---- bang 57, mui ten trai (Tr)  (tai lieu ghi: block 85,86)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (85, 86)
WHERE  p.code = '57';
-- cam bien do thuong, tai lieu ghi: Z0.2.3~6
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 0 AS z, 2 AS l, 4 AS i UNION ALL SELECT 0,2,5 UNION ALL SELECT 0,2,6 UNION ALL SELECT 0,2,7) v
WHERE  p.code = '57';

-- ---- bang 57, mui ten len (Th - thang)  (tai lieu ghi: block 87,94~112)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (87, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112)
WHERE  p.code = '57';
-- cam bien do thuong, tai lieu ghi: Z0.2.1~2, Z0.2.7~14, Z0.1.7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 0 AS z, 1 AS l, 8 AS i UNION ALL SELECT 0,2,2 UNION ALL SELECT 0,2,3 UNION ALL SELECT 0,2,8 UNION ALL SELECT 0,2,9 UNION ALL SELECT 0,2,10 UNION ALL SELECT 0,2,11 UNION ALL SELECT 0,2,12 UNION ALL SELECT 0,2,13 UNION ALL SELECT 0,2,14 UNION ALL SELECT 0,2,15) v
WHERE  p.code = '57';

-- ---- bang 57, mui ten phai (P)  (tai lieu ghi: block 88~93)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (88, 89, 90, 91, 92, 93)
WHERE  p.code = '57';
-- cam bien do thuong, tai lieu ghi: Z3.2.8~10
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 3 AS z, 2 AS l, 9 AS i UNION ALL SELECT 3,2,10 UNION ALL SELECT 3,2,11) v
WHERE  p.code = '57';

-- ---- bang 66, mui ten trai (Tr)  (tai lieu ghi: block 96~103)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (96, 97, 98, 99, 100, 101, 102, 103)
WHERE  p.code = '66';
-- cam bien do thuong, tai lieu ghi: Z0.2.7~14, Z0.1.7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 0 AS z, 1 AS l, 8 AS i UNION ALL SELECT 0,2,8 UNION ALL SELECT 0,2,9 UNION ALL SELECT 0,2,10 UNION ALL SELECT 0,2,11 UNION ALL SELECT 0,2,12 UNION ALL SELECT 0,2,13 UNION ALL SELECT 0,2,14 UNION ALL SELECT 0,2,15) v
WHERE  p.code = '66';

-- ---- bang 66, mui ten len (Th - thang)  (tai lieu ghi: block 95, 104~112)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (95, 104, 105, 106, 107, 108, 109, 110, 111, 112)
WHERE  p.code = '66';
-- cam bien do thuong, tai lieu ghi: Z0.1.1~7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 0 AS z, 1 AS l, 2 AS i UNION ALL SELECT 0,1,3 UNION ALL SELECT 0,1,4 UNION ALL SELECT 0,1,5 UNION ALL SELECT 0,1,6 UNION ALL SELECT 0,1,7 UNION ALL SELECT 0,1,8) v
WHERE  p.code = '66';

-- Từ đây 21 cổng chỉ hướng đọc theo block, không đọc zone nữa.
UPDATE led_panel_port o
  JOIN led_panel p ON p.panel_id = o.panel_id
   SET o.scope = 'BLOCKS'
 WHERE p.kind = 'DIRECTIONAL';


-- ------------------------------------------------------------------ view
-- Cùng quy ước với v_pgs_standard_zone: chỉ trang_thai = 0 là trống, ô lỗi và
-- ô không lắp không tính; ngưỡng tươi 5 phút.
CREATE OR REPLACE VIEW v_pgs_standard_port AS
SELECT ps.panel_id, ps.port_index,
       COUNT(*)                                     AS tong_cam_bien,
       SUM(s.trang_thai = 0)                        AS trong,
       SUM(s.trang_thai = 1)                        AS co_xe,
       SUM(s.read_at >= NOW(3) - INTERVAL 5 MINUTE) AS con_tuoi
FROM   led_port_sensor ps
JOIN   pgs_sensor_state s
       ON s.zcu_id = ps.zcu_id AND s.lo = ps.lo AND s.vi_tri = ps.vi_tri
WHERE  s.trang_thai <> 3
GROUP  BY ps.panel_id, ps.port_index;

-- Độ phủ PLC của riêng những block mà cổng này quảng bá. Cùng ý nghĩa với
-- v_zone_coverage: bao nhiêu ô vừa đọc được trong 5 phút.
CREATE OR REPLACE VIEW v_led_port_coverage AS
SELECT pb.panel_id, pb.port_index,
       COUNT(*)                                                AS o_co_khi,
       SUM(s.read_at IS NOT NULL
           AND s.read_at >= NOW() - INTERVAL 5 MINUTE)         AS o_vua_doc
FROM   led_port_block pb
JOIN   plc_slot_state s ON s.block_id = pb.block_id
GROUP  BY pb.panel_id, pb.port_index;

-- Sức chứa theo TỪNG CỔNG. Hai dòng cơ khí giữ nguyên công thức của
-- v_led_capacity_zone, chỉ đổi phạm vi gộp từ zone sang danh sách block.
--
-- KHÁC MỘT ĐIỂM so với bản theo zone: total_standard ở đây là SỐ CẢM BIẾN mà
-- cổng quảng bá, không phải slot_count của block kind='Ground'. Lý do: cổng
-- được khai theo cảm biến, và ô đỗ thường vốn được đo bằng cảm biến chứ không
-- bằng phiên gửi xe. Lấy slot_count sẽ cho một mẫu số mà không phép đo nào
-- kiểm được.
CREATE OR REPLACE VIEW v_led_capacity_port AS
SELECT o.panel_id, o.port_index,
       t.total_l5m, t.total_l48m,
       COALESCE(sn.tong_cam_bien, 0) AS total_standard,
       u.used_l5m, u.used_l48m,
       COALESCE(sn.co_xe, 0)         AS used_standard,
       GREATEST(CAST(t.total_l5m  AS SIGNED) - u.used_l5m,  0) AS free_l5m,
       GREATEST(CAST(t.total_l48m AS SIGNED) - u.used_l48m, 0) AS free_l48m,
       -- Cùng cách dự phòng như bản theo zone: chưa có số liệu tươi thì trả về
       -- sức chứa chứ KHÔNG trả về 0. LEAST kẹp theo số cảm biến để không bao
       -- giờ hiện nhiều chỗ trống hơn số ô thật.
       LEAST(
           CASE WHEN sn.con_tuoi IS NULL OR sn.con_tuoi = 0
                THEN COALESCE(sn.tong_cam_bien, 0)
                ELSE sn.trong
           END,
           COALESCE(sn.tong_cam_bien, 0)
       ) AS free_standard,
       COALESCE(sn.tong_cam_bien, 0) AS standard_sensors,
       COALESCE(sn.con_tuoi, 0)      AS standard_fresh,
       COALESCE(cv.o_co_khi, 0)      AS slots_total,
       COALESCE(cv.o_vua_doc, 0)     AS slots_fresh
FROM   led_panel_port o
JOIN LATERAL (
    SELECT COALESCE(SUM(CASE WHEN b.bay_length_mm > 4800 THEN b.slot_count END), 0) AS total_l5m,
           COALESCE(SUM(b.slot_count), 0)                                           AS total_l48m
    FROM   led_port_block pb
    JOIN   block b ON b.block_id = pb.block_id AND b.is_active = 1
    WHERE  pb.panel_id = o.panel_id AND pb.port_index = o.port_index
) t ON TRUE
JOIN LATERAL (
    SELECT COALESCE(SUM(CASE WHEN b.bay_length_mm > 4800 THEN 1 END), 0) AS used_l5m,
           COUNT(*)                                                      AS used_l48m
    FROM   led_port_block pb
    JOIN   block b ON b.block_id = pb.block_id AND b.is_active = 1
    JOIN   v_slot_taken s ON s.block_id = b.block_id
    WHERE  pb.panel_id = o.panel_id AND pb.port_index = o.port_index
) u ON TRUE
LEFT   JOIN v_pgs_standard_port  sn ON sn.panel_id = o.panel_id AND sn.port_index = o.port_index
LEFT   JOIN v_led_port_coverage  cv ON cv.panel_id = o.panel_id AND cv.port_index = o.port_index
WHERE  o.scope = 'BLOCKS';


-- ------------------------------------------------------------------ đối chiếu
SELECT (SELECT COUNT(*) FROM led_port_block)          AS cap_cong_block,
       (SELECT COUNT(*) FROM led_port_sensor)         AS cap_cong_cam_bien,
       (SELECT COUNT(*) FROM led_panel_port
         WHERE scope = 'BLOCKS')                      AS cong_doc_theo_block,
       (SELECT COUNT(*) FROM v_led_capacity_port)     AS dong_view;

SELECT p.code, o.port_index,
       CASE o.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END       AS huong,
       c.total_l48m AS o_co_khi, c.free_l48m AS trong_co_khi,
       c.standard_sensors AS cam_bien, c.free_standard AS trong_do_thuong
FROM   v_led_capacity_port c
JOIN   led_panel_port o ON o.panel_id = c.panel_id AND o.port_index = c.port_index
JOIN   led_panel p ON p.panel_id = o.panel_id
ORDER  BY CAST(p.code AS UNSIGNED), o.port_index;


-- ===================== HOÀN TÁC =====================
--
--   UPDATE led_panel_port SET scope = 'ZONES'
--    WHERE panel_id IN (SELECT panel_id FROM led_panel WHERE kind = 'DIRECTIONAL');
--   UPDATE led_panel_port o JOIN led_panel p ON p.panel_id = o.panel_id
--      SET o.arrow_direction = 0 WHERE p.code = '52';
--   DROP VIEW IF EXISTS v_led_capacity_port, v_led_port_coverage, v_pgs_standard_port;
--   DROP TABLE IF EXISTS led_port_sensor, led_port_block;
--   ALTER TABLE led_panel_port DROP CHECK ck_led_scope;
--   ALTER TABLE led_panel_port
--     ADD CONSTRAINT ck_led_scope CHECK (scope IN ('TOTAL', 'ZONES'));
