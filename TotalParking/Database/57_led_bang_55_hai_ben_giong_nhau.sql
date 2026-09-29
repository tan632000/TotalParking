-- =========================================================================
-- Bảng LED 55: hai bên hiện GIỐNG HỆT nhau, cùng chỉ THẲNG
-- =========================================================================
--
-- Sửa cách hiểu sai ở `56_led_bang_55_mot_huong.sql`.
--
-- Bản xlsx ngày 29/09 chỉ còn MỘT dòng cho bảng 55. File 56 đọc điều đó thành
-- "bảng chỉ còn một hướng, cổng kia bỏ trống", nên cổng 0 bị bỏ hết khối và bị
-- xoá trắng mỗi nhịp — tức nửa bảng tắt đen.
--
-- Người dùng đính chính (29/09): bảng 55 có HAI BÊN, và cả hai đều chỉ THẲNG
-- với CÙNG MỘT con số. Dòng duy nhất trong file mô tả hướng chung của cả bảng,
-- không phải mô tả một cổng; hai cổng vật lý cùng phản chiếu nó.
--
-- ===================== VÌ SAO CHÉP TỪ CỔNG 1 CHỨ KHÔNG GHI LẠI =====================
--
-- Danh sách khối và cảm biến của cổng 0 lấy bằng `INSERT ... SELECT` từ chính
-- cổng 1, không gõ lại số khối. Hai bên phải giống nhau là RÀNG BUỘC, nên cách
-- viết nào để chúng lệch được thì cách đó sai. Lần sau đổi danh sách của bảng
-- 55, chỉ cần sửa cổng 1 rồi chạy lại file này là hai bên tự khớp.
--
-- Đối chiếu:  python tools/doc_ma_tran_led.py
-- Chỉ DELETE/INSERT/UPDATE -> tài khoản ứng dụng chạy được.


-- ----------------------------------------------------------------- bản chụp
CREATE TABLE IF NOT EXISTS led_port_block_sao_luu_57 LIKE led_port_block;
SET @da_chup := (SELECT COUNT(*) FROM led_port_block_sao_luu_57);
INSERT IGNORE INTO led_port_block_sao_luu_57
SELECT * FROM led_port_block WHERE @da_chup = 0;

CREATE TABLE IF NOT EXISTS led_port_sensor_sao_luu_57 LIKE led_port_sensor;
SET @da_chup2 := (SELECT COUNT(*) FROM led_port_sensor_sao_luu_57);
INSERT IGNORE INTO led_port_sensor_sao_luu_57
SELECT * FROM led_port_sensor WHERE @da_chup2 = 0;


-- ------------------------------------------------------- hướng của cổng 0
-- 0 = lên/thẳng. Cổng 0 đang mang 1 = phải, là di sản của bản xlsx cũ khi bảng
-- 55 còn thật sự có hai hướng khác nhau.
UPDATE led_panel_port o
  JOIN led_panel p ON p.panel_id = o.panel_id
   SET o.arrow_direction = 0, o.is_active = 1
 WHERE p.code = '55' AND o.port_index = 0;


-- ------------------------------------------------- chép cổng 1 sang cổng 0
-- Xoá trước để chạy lại được nhiều lần mà không nhân đôi.
DELETE pb FROM led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
WHERE  p.code = '55' AND pb.port_index = 0;

DELETE ps FROM led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
WHERE  p.code = '55' AND ps.port_index = 0;

INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT pb.panel_id, 0, pb.block_id
FROM   led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
WHERE  p.code = '55' AND pb.port_index = 1;

INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT ps.panel_id, 0, ps.zcu_id, ps.lo, ps.vi_tri
FROM   led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
WHERE  p.code = '55' AND ps.port_index = 1;


-- ------------------------------------------------------------------ đối chiếu
-- Kỳ vọng: hai dòng GIỐNG NHAU từng cột, trừ port_index.
SELECT o.port_index,
       CASE o.arrow_direction WHEN 0 THEN 'THANG' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END AS huong,
       o.is_active,
       (SELECT COUNT(*) FROM led_port_block pb
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index) AS so_khoi,
       (SELECT MIN(b.block_no) FROM led_port_block pb JOIN block b ON b.block_id = pb.block_id
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index) AS khoi_nho_nhat,
       (SELECT MAX(b.block_no) FROM led_port_block pb JOIN block b ON b.block_id = pb.block_id
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index) AS khoi_lon_nhat,
       (SELECT COUNT(*) FROM led_port_sensor ps
         WHERE ps.panel_id = o.panel_id AND ps.port_index = o.port_index) AS so_cam_bien
FROM   led_panel_port o
JOIN   led_panel p ON p.panel_id = o.panel_id
WHERE  p.code = '55'
ORDER  BY o.port_index;

-- Số hiển thị của hai cổng phải TRÙNG KHÍT. Câu này trả 0 dòng là đạt.
SELECT 'HAI BEN LECH NHAU' AS canh_bao, a.port_index, b.port_index,
       a.free_l48m, b.free_l48m, a.free_standard, b.free_standard
FROM   v_led_capacity_port a
JOIN   v_led_capacity_port b ON b.panel_id = a.panel_id AND b.port_index > a.port_index
JOIN   led_panel p ON p.panel_id = a.panel_id
WHERE  p.code = '55'
  AND (a.free_l5m <> b.free_l5m OR a.free_l48m <> b.free_l48m
       OR a.free_standard <> b.free_standard);

SELECT (SELECT COUNT(*) FROM led_port_block)  AS cap_cong_block,
       (SELECT COUNT(*) FROM led_port_sensor) AS cap_cong_cam_bien;


-- ===================== HOÀN TÁC =====================
--
--   UPDATE led_panel_port o JOIN led_panel p ON p.panel_id = o.panel_id
--      SET o.arrow_direction = 1 WHERE p.code = '55' AND o.port_index = 0;
--   DELETE pb FROM led_port_block pb JOIN led_panel p ON p.panel_id = pb.panel_id
--    WHERE p.code = '55' AND pb.port_index = 0;
--   DELETE ps FROM led_port_sensor ps JOIN led_panel p ON p.panel_id = ps.panel_id
--    WHERE p.code = '55' AND ps.port_index = 0;
