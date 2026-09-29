-- =========================================================================
-- Bảng LED 55 còn MỘT hướng, theo bản cập nhật của docs/LumiSlotsMatrix.xlsx
-- =========================================================================
--
-- Khách cập nhật file lần hai ngày 29/09/2026. So với bản đang dùng, toàn bộ
-- thay đổi nằm ở bảng 55:
--
--     cong THANG   block 31~54        ->  block 33~54     (bo khoi 31, 32)
--     cong PHAI    block 1~10,16~20   ->  KHONG CON
--
-- Tổng số cổng toàn hệ thống: 21 -> 20. 19 cổng của 10 bảng còn lại giữ nguyên
-- từng chữ.
--
-- ===================== NỬA PHẢI CỦA BẢNG 55 SẼ TẮT =====================
--
-- Đây là thay đổi người lái xe NHÌN THẤY NGAY, nên đã hỏi và người dùng xác
-- nhận "làm y nguyên file mô tả" (29/09/2026). Ảnh chụp hiện trường lúc 11:54
-- cùng ngày cho thấy bảng 55 đang sáng cả hai mũi tên — mũi tên phải hiện
-- 0091 / 0091 / 0006 — nên sau khi chạy file này nửa đó sẽ tối.
--
-- ===================== VÌ SAO KHÔNG ĐẶT is_active = 0 =====================
--
-- Cổng 0 mất hết block và cảm biến, nhưng vẫn GIỮ is_active = 1. Có chủ ý.
--
-- `v_led_capacity_port` vẫn trả một dòng toàn số 0 cho cổng đó, `LedCapacity
-- .HasData` vì thế false, và `LedPublisher` gửi `LedHub.Blank(port)` — tức lệnh
-- XOÁ TRẮNG, mỗi nhịp 5 giây. Nửa bảng tắt hẳn và tắt liên tục.
--
-- Nếu đặt is_active = 0 thì vòng đẩy bỏ qua cổng đó hoàn toàn, bảng không nhận
-- lệnh nào nữa và sẽ GIỮ NGUYÊN con số cũ đứng im trên màn hình cho tới khi mất
-- điện. Một con số đóng băng trông y hệt số đang sống — đó mới là bảng nói dối,
-- đúng thứ mà chú thích trong LedPublisher cảnh báo.
--
-- ===================== KHÔNG KHỐI NÀO MẤT CHỖ CHỈ ĐƯỜNG =====================
--
-- Đã kiểm trước khi chạy: những khối rời khỏi bảng 55 vẫn còn cổng khác quảng bá.
--
--     block 1, 5       -> 51/Tr, 52/Tr, 54/Th, 65/P
--     block 16, 20     -> 51/Tr, 54/Th, 65/P
--     block 31, 32     -> 51/Tr, 54/Th, 65/P
--
-- Đối chiếu:  python tools/doc_ma_tran_led.py
-- Chỉ DELETE/INSERT -> tài khoản ứng dụng chạy được.


-- ----------------------------------------------------------------- bản chụp
-- Chỉ chụp khi bảng sao lưu còn RỖNG, cùng lý do với các migration trước.
CREATE TABLE IF NOT EXISTS led_port_block_sao_luu_56 LIKE led_port_block;
SET @da_chup := (SELECT COUNT(*) FROM led_port_block_sao_luu_56);
INSERT IGNORE INTO led_port_block_sao_luu_56
SELECT * FROM led_port_block WHERE @da_chup = 0;

CREATE TABLE IF NOT EXISTS led_port_sensor_sao_luu_56 LIKE led_port_sensor;
SET @da_chup2 := (SELECT COUNT(*) FROM led_port_sensor_sao_luu_56);
INSERT IGNORE INTO led_port_sensor_sao_luu_56
SELECT * FROM led_port_sensor WHERE @da_chup2 = 0;


-- ------------------------------------------------------------------ gieo lại
-- Chỉ đụng bảng 55. Sau câu xoá, cổng 0 (phải) không còn dòng nào và sẽ không
-- được gieo lại — đó chính là điều bản mới mô tả.
DELETE pb FROM led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
WHERE  p.code = '55';

DELETE ps FROM led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
WHERE  p.code = '55';

-- ---- bang 55, mui ten len (Th)  (tai lieu: block 33~54)
INSERT INTO led_port_block (panel_id, port_index, block_id)
SELECT o.panel_id, o.port_index, b.block_id
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   block b ON b.kind = 'Mechanical' AND b.block_no IN (33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54)
WHERE  p.code = '55';
-- cam bien, tai lieu: Z4
INSERT INTO led_port_sensor (panel_id, port_index, zcu_id, lo, vi_tri)
SELECT o.panel_id, o.port_index, v.z, v.l, v.i
FROM   led_panel p
JOIN   led_panel_port o ON o.panel_id = p.panel_id AND o.arrow_direction = 0
JOIN   (SELECT 4 AS z, 1 AS l, 2 AS i UNION ALL SELECT 4,1,3 UNION ALL SELECT 4,2,2 UNION ALL SELECT 4,2,3 UNION ALL SELECT 4,2,4 UNION ALL SELECT 4,2,5 UNION ALL SELECT 4,2,6) v
WHERE  p.code = '55';

-- ------------------------------------------------------------------ đối chiếu
-- Kỳ vọng:
--     cong 0  phai   0 khoi, 0 cam bien   -> se bi xoa trang moi nhip
--     cong 1  len   22 khoi (33..54), 7 cam bien
SELECT o.port_index,
       CASE o.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
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

-- Cổng 0 phải cho HasData = false thì mới bị xoá trắng. Ba cột này đều 0 là đạt.
SELECT c.port_index, c.total_l5m, c.total_l48m, c.total_standard
FROM   v_led_capacity_port c
JOIN   led_panel p ON p.panel_id = c.panel_id
WHERE  p.code = '55' ORDER BY c.port_index;

-- Tổng toàn hệ thống: 347 -> 330 cặp block, 248 -> 240 cặp cảm biến.
-- (bỏ 15 khối cổng phải + 24 khối cổng thẳng cũ, gieo lại 22 -> giảm 17;
--  bỏ 8 cảm biến cổng phải + 7 cổng thẳng cũ, gieo lại 7 -> giảm 8)
SELECT (SELECT COUNT(*) FROM led_port_block)  AS cap_cong_block,
       (SELECT COUNT(*) FROM led_port_sensor) AS cap_cong_cam_bien;


-- ===================== HOÀN TÁC =====================
--
--   DELETE pb FROM led_port_block pb JOIN led_panel p ON p.panel_id = pb.panel_id
--    WHERE p.code = '55';
--   INSERT INTO led_port_block SELECT * FROM led_port_block_sao_luu_56 s
--    WHERE s.panel_id = (SELECT panel_id FROM led_panel WHERE code = '55');
--   DELETE ps FROM led_port_sensor ps JOIN led_panel p ON p.panel_id = ps.panel_id
--    WHERE p.code = '55';
--   INSERT INTO led_port_sensor SELECT * FROM led_port_sensor_sao_luu_56 s
--    WHERE s.panel_id = (SELECT panel_id FROM led_panel WHERE code = '55');
