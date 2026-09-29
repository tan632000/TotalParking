-- =========================================================================
-- Gieo lại bảng LED 65 theo bản cập nhật của docs/LumiSlotsMatrix.xlsx
-- =========================================================================
--
-- Khách cập nhật file ngày 29/09/2026 vì bản trước ghi nhầm hướng. So hai bản,
-- ĐÚNG MỘT bảng đổi, và chỉ đổi hướng chứ không đổi danh sách block:
--
--     bang 65  block 1~62     Tr  ->  P
--     bang 65  block 85~112   P   ->  Tr
--
-- 19 cổng còn lại của 10 bảng kia giữ nguyên từng chữ.
--
-- ===================== VÌ SAO GIEO LẠI CHỨ KHÔNG ĐẢO arrow_direction =====================
--
-- `31_led_arrow_swap_lr.sql` đã lập ra nguyên tắc, và nó vẫn đúng ở đây:
--
--     "Chỉ số cổng là sự thật phần cứng, con số nào nằm ở cột nào thì đã đúng
--      sẵn. Cái sai duy nhất là hình mũi tên đặt nhầm bên."
--
-- Lần này thì ngược lại: hình mũi tên trên từng cổng đã đúng (khách xem bảng
-- thật ngày 17/09 và xác nhận), cái sai là DANH SÁCH BLOCK bị gắn vào cổng
-- nhầm, do migration 53 gieo theo cặp (mã bảng, arrow_direction) lấy từ bản
-- xlsx cũ.
--
-- Trạng thái trước khi chạy file này:
--     cong 0  phai   giu block 85..112   (le ra phai la 1..62)
--     cong 1  len    giu block 70..84    (dung, khong doi)
--     cong 2  trai   giu block 1..62     (le ra phai la 85..112)
--
-- Nên sửa đúng là DỜI danh sách sang cổng đúng, GIỮ NGUYÊN `arrow_direction`.
-- Đảo `arrow_direction` cũng cho ra cùng kết quả hiển thị, nhưng nó phá vỡ sự
-- thật phần cứng mà khách đã xác nhận tận nơi, và lần sau ai đọc cũng sẽ tưởng
-- mũi tên trên bảng 65 lắp ngược.
--
-- Không dùng `UPDATE ... SET port_index` để hoán vị: khoá chính là
-- (panel_id, port_index, block_id) nên bước trung gian sẽ đụng khoá, mà mượn
-- một `port_index` tạm thì vướng khoá ngoại sang `led_panel_port`. Xoá rồi gieo
-- lại đúng theo mẫu của migration 53 vừa gọn vừa tự nó là bằng chứng.
--
-- Đối chiếu:  python tools/doc_ma_tran_led.py
-- Cần quyền DDL? KHÔNG — chỉ DELETE/INSERT, tài khoản ứng dụng chạy được.


-- ----------------------------------------------------------------- bản chụp
-- Chỉ chụp khi bảng sao lưu còn RỖNG, cùng lý do với migration 51 và 52: lần
-- chạy thứ hai mà chụp đè thì bản gốc để hoàn tác biến mất.
CREATE TABLE IF NOT EXISTS led_port_block_sao_luu_55 LIKE led_port_block;
SET @da_chup := (SELECT COUNT(*) FROM led_port_block_sao_luu_55);
INSERT IGNORE INTO led_port_block_sao_luu_55
SELECT * FROM led_port_block WHERE @da_chup = 0;

CREATE TABLE IF NOT EXISTS led_port_sensor_sao_luu_55 LIKE led_port_sensor;
SET @da_chup2 := (SELECT COUNT(*) FROM led_port_sensor_sao_luu_55);
INSERT IGNORE INTO led_port_sensor_sao_luu_55
SELECT * FROM led_port_sensor WHERE @da_chup2 = 0;


-- ------------------------------------------------------------------ gieo lại
-- Chỉ đụng bảng 65. Mười bảng còn lại không bị chạm tới.
DELETE pb FROM led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
WHERE  p.code = '65';

DELETE ps FROM led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
WHERE  p.code = '65';

-- ---- bang 65, mui ten phai (P)  (tai lieu: block 1~62)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62)
WHERE  p.code = '65';
-- cam bien, tai lieu: Z1, Z4, Z2, Z3.1.1~6
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 1
JOIN   (SELECT 1 AS z, 1 AS l, 2 AS i UNION ALL SELECT 1,1,3 UNION ALL SELECT 1,1,4 UNION ALL SELECT 1,1,5 UNION ALL SELECT 1,2,2 UNION ALL SELECT 1,2,3 UNION ALL SELECT 1,2,4 UNION ALL SELECT 1,2,5 UNION ALL SELECT 1,2,6 UNION ALL SELECT 1,2,7 UNION ALL SELECT 1,2,8 UNION ALL SELECT 1,2,9 UNION ALL SELECT 1,2,10 UNION ALL SELECT 1,2,11 UNION ALL SELECT 1,2,12 UNION ALL SELECT 1,2,13 UNION ALL SELECT 1,2,14 UNION ALL SELECT 2,1,2 UNION ALL SELECT 2,1,3 UNION ALL SELECT 2,1,4 UNION ALL SELECT 2,1,5 UNION ALL SELECT 2,1,6 UNION ALL SELECT 2,1,7 UNION ALL SELECT 2,1,8 UNION ALL SELECT 2,2,2 UNION ALL SELECT 2,2,3 UNION ALL SELECT 2,2,4 UNION ALL SELECT 2,2,5 UNION ALL SELECT 2,2,6 UNION ALL SELECT 2,2,7 UNION ALL SELECT 2,2,8 UNION ALL SELECT 2,2,9 UNION ALL SELECT 2,2,10 UNION ALL SELECT 2,2,11 UNION ALL SELECT 2,2,12 UNION ALL SELECT 3,1,2 UNION ALL SELECT 3,1,3 UNION ALL SELECT 3,1,4 UNION ALL SELECT 3,1,5 UNION ALL SELECT 3,1,6 UNION ALL SELECT 3,1,7 UNION ALL SELECT 4,1,2 UNION ALL SELECT 4,1,3 UNION ALL SELECT 4,2,2 UNION ALL SELECT 4,2,3 UNION ALL SELECT 4,2,4 UNION ALL SELECT 4,2,5 UNION ALL SELECT 4,2,6) v
WHERE  p.code = '65';

-- ---- bang 65, mui ten len (Th)  (tai lieu: block 70~84)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84)
WHERE  p.code = '65';
-- cam bien, tai lieu: Z3.2.1~7
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 3 AS z, 2 AS l, 2 AS i UNION ALL SELECT 3,2,3 UNION ALL SELECT 3,2,4 UNION ALL SELECT 3,2,5 UNION ALL SELECT 3,2,6 UNION ALL SELECT 3,2,7 UNION ALL SELECT 3,2,8) v
WHERE  p.code = '65';

-- ---- bang 65, mui ten trai (Tr)  (tai lieu: block 85~112)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112)
WHERE  p.code = '65';
-- cam bien, tai lieu: Z0, Z3.2.8~10
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 3
JOIN   (SELECT 0 AS z, 1 AS l, 2 AS i UNION ALL SELECT 0,1,3 UNION ALL SELECT 0,1,4 UNION ALL SELECT 0,1,5 UNION ALL SELECT 0,1,6 UNION ALL SELECT 0,1,7 UNION ALL SELECT 0,1,8 UNION ALL SELECT 0,2,2 UNION ALL SELECT 0,2,3 UNION ALL SELECT 0,2,4 UNION ALL SELECT 0,2,5 UNION ALL SELECT 0,2,6 UNION ALL SELECT 0,2,7 UNION ALL SELECT 0,2,8 UNION ALL SELECT 0,2,9 UNION ALL SELECT 0,2,10 UNION ALL SELECT 0,2,11 UNION ALL SELECT 0,2,12 UNION ALL SELECT 0,2,13 UNION ALL SELECT 0,2,14 UNION ALL SELECT 0,2,15 UNION ALL SELECT 3,2,9 UNION ALL SELECT 3,2,10 UNION ALL SELECT 3,2,11) v
WHERE  p.code = '65';

-- ------------------------------------------------------------------ đối chiếu
-- Kỳ vọng sau khi chạy:
--     cong 0  phai   62 block  (1..62)
--     cong 1  len    15 block  (70..84)
--     cong 2  trai   28 block  (85..112)
SELECT o.port_index,
       CASE o.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END AS huong,
       (SELECT COUNT(*) FROM led_port_block pb
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index)  AS so_block,
       (SELECT MIN(b.block_no) FROM led_port_block pb JOIN block b ON b.block_id = pb.block_id
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index)  AS block_nho_nhat,
       (SELECT MAX(b.block_no) FROM led_port_block pb JOIN block b ON b.block_id = pb.block_id
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index)  AS block_lon_nhat,
       (SELECT COUNT(*) FROM led_port_sensor ps
         WHERE ps.panel_id = o.panel_id AND ps.port_index = o.port_index)  AS so_cam_bien
FROM   led_panel_port o
JOIN   led_panel p ON p.panel_id = o.panel_id
WHERE  p.code = '65'
ORDER  BY o.port_index;

-- Tổng toàn hệ thống phải không đổi: 347 cặp block, 248 cặp cảm biến.
SELECT (SELECT COUNT(*) FROM led_port_block)  AS cap_cong_block,
       (SELECT COUNT(*) FROM led_port_sensor) AS cap_cong_cam_bien;


-- ===================== HOÀN TÁC =====================
--
--   DELETE pb FROM led_port_block pb JOIN led_panel p ON p.panel_id = pb.panel_id
--    WHERE p.code = '65';
--   INSERT INTO led_port_block SELECT * FROM led_port_block_sao_luu_55 s
--    WHERE s.panel_id = (SELECT panel_id FROM led_panel WHERE code = '65');
--   DELETE ps FROM led_port_sensor ps JOIN led_panel p ON p.panel_id = ps.panel_id
--    WHERE p.code = '65';
--   INSERT INTO led_port_sensor SELECT * FROM led_port_sensor_sao_luu_55 s
--    WHERE s.panel_id = (SELECT panel_id FROM led_panel WHERE code = '65');
