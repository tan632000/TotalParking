-- =========================================================================
-- Bảng LED 2 hướng có mũi tên THẲNG: theo đúng quy ước P1/P2 của tài liệu
-- =========================================================================
-- MySQL 8.0.19+. Chạy lại nhiều lần an toàn. UTF-8 KHÔNG BOM.
-- Chỉ UPDATE -> tài khoản ứng dụng chạy được.
--
-- ===================== QUY TẮC =====================
--
-- Người dùng chốt (12/10/2026), khớp Hình 4 của "Giao thức giao tiếp giữa máy
-- tính và bảng led hiển thị hướng": P1 (port_index 0) là cột trái, P2
-- (port_index 1) là cột phải.
--
--     thẳng + trái  -> TRÁI là P1, THẲNG là P2      (bảng 66)
--     thẳng + phải  -> THẲNG là P1, PHẢI là P2      (bảng 58)
--
-- ===================== VÌ SAO CHỈ HAI BẢNG NÀY =====================
--
-- 59_led_cong_0_la_cot_phai.sql suy "cổng 0 là cột phải" từ ảnh hiện trường
-- của bảng 51, 57 và 65, rồi áp cho cả 58 và 66 — hai bảng không có trong ảnh.
-- Với 58 và 66 thì suy diễn đó sai: bảng 66 trước file 59 hiện LÊN ở cột trái
-- và khách đã báo sai đúng chỗ đó.
--
-- Bảng 51/53/57/65 đấu cáp ngược vị trí (khách xác nhận 14/10/2026) và được bù
-- riêng ở 63_led_dao_trai_phai_dau_nguoc.sql. File này chỉ lo 58 và 66.
--
-- ===================== CÁCH SỬA =====================
--
-- Đổi chỗ cổng 0 <-> 1, mang theo danh sách khối và cảm biến của từng hướng.
-- Chỉ chạy khi cổng 0 còn mang hướng SAI (58: PHẢI, 66: LÊN), nên lần chạy thứ
-- hai không làm gì. Cảm biến thuộc cả hai cổng của bảng 66 giữ nguyên.
--
-- Sau khi chạy: POST /LedStatus/Reload (từ chính máy chủ).

START TRANSACTION;

-- k.dir0_sai: hướng cổng 0 đang mang khi bảng còn SAI (0 = lên, 1 = phải).
-- Bảng dẫn xuất DISTINCT ở NOT EXISTS: tránh lỗi 1093 của MySQL khi UPDATE một
-- bảng rồi đọc lại chính nó trong subquery.

UPDATE led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
JOIN  (          SELECT '58' AS code, 1 AS dir0_sai
       UNION ALL SELECT '66', 0
) k ON k.code = p.code
JOIN   led_panel_port p0 ON p0.panel_id = pb.panel_id AND p0.port_index = 0
SET    pb.port_index = 1 - pb.port_index
WHERE  p0.arrow_direction = k.dir0_sai
  AND  pb.port_index IN (0, 1)
  AND  NOT EXISTS (SELECT 1 FROM (SELECT DISTINCT panel_id, port_index, block_id
                                  FROM led_port_block) t
                   WHERE t.panel_id   = pb.panel_id
                     AND t.block_id   = pb.block_id
                     AND t.port_index = 1 - pb.port_index);

UPDATE led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
JOIN  (          SELECT '58' AS code, 1 AS dir0_sai
       UNION ALL SELECT '66', 0
) k ON k.code = p.code
JOIN   led_panel_port p0 ON p0.panel_id = ps.panel_id AND p0.port_index = 0
SET    ps.port_index = 1 - ps.port_index
WHERE  p0.arrow_direction = k.dir0_sai
  AND  ps.port_index IN (0, 1)
  AND  NOT EXISTS (SELECT 1 FROM (SELECT DISTINCT panel_id, port_index, zcu_id, lo, vi_tri
                                  FROM led_port_sensor) t
                   WHERE t.panel_id   = ps.panel_id
                     AND t.zcu_id     = ps.zcu_id
                     AND t.lo         = ps.lo
                     AND t.vi_tri     = ps.vi_tri
                     AND t.port_index = 1 - ps.port_index);

-- Hướng chạy CUỐI: hai câu trên đọc hướng cổng 0 để biết bảng nào còn sai.
UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
JOIN   led_panel_port p0 ON p0.panel_id = pp.panel_id AND p0.port_index = 0
JOIN  (          SELECT '58' AS code, 0 AS port_index, 0 AS dir, 1 AS dir0_sai   -- P1 THẲNG
       UNION ALL SELECT '58', 1, 1, 1                                            -- P2 PHẢI
       UNION ALL SELECT '66', 0, 3, 0                                            -- P1 TRÁI
       UNION ALL SELECT '66', 1, 0, 0                                            -- P2 THẲNG
) v ON v.code = p.code AND v.port_index = pp.port_index
SET    pp.arrow_direction = v.dir
WHERE  p0.arrow_direction = v.dir0_sai;

COMMIT;

-- ------------------------------------------------------------------ đối chiếu
SELECT p.code AS bang, CONCAT('P', o.port_index + 1) AS cong,
       ELT(o.arrow_direction + 1, 'LEN', 'PHAI', 'XUONG', 'TRAI') AS huong,
       (SELECT GROUP_CONCAT(b.block_no ORDER BY b.block_no)
          FROM led_port_block pb JOIN block b ON b.block_id = pb.block_id
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index) AS khoi,
       (SELECT COUNT(*) FROM led_port_sensor ps
         WHERE ps.panel_id = o.panel_id AND ps.port_index = o.port_index) AS cam_bien
FROM   led_panel_port o JOIN led_panel p ON p.panel_id = o.panel_id
WHERE  p.code IN ('58', '66') AND o.is_active = 1
ORDER  BY CAST(p.code AS UNSIGNED), o.port_index;
