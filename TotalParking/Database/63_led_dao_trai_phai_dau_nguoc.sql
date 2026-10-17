-- =========================================================================
-- Bảng LED 51, 53, 57, 65: đảo trái/phải để bù đấu nối ngược
-- =========================================================================
-- MySQL 8.0.19+. Chạy lại nhiều lần an toàn. UTF-8 KHÔNG BOM.
-- Chỉ UPDATE -> tài khoản ứng dụng chạy được.
--
-- ===================== VÌ SAO =====================
--
-- 61_led_theo_tai_lieu.sql đặt mọi bảng theo tài liệu giao thức: P1 (cổng 0) là
-- cột trái. Khách báo ngày 14/10/2026: bên thiết kế đấu cáp NGƯỢC vị trí ở bốn
-- bảng 51, 53, 57, 65, nên trên bốn bảng này cổng 0 hiện ở cột PHẢI — đúng như
-- ảnh hiện trường docs/direction_1..3.jpg (bảng 65, 57, 51). Khách nhờ bù trong
-- phần mềm thay vì đổi cáp.
--
-- Bảng 58 và 66 đấu đúng tài liệu, KHÔNG đổi.
--
-- ===================== CÁCH SỬA =====================
--
-- Đảo gương cổng (2 cổng: 0<->1; 3 cổng: 0<->2, cổng giữa LÊN đứng yên), mang
-- theo danh sách khối và cảm biến của từng hướng. Dữ liệu cổng sau file này:
--
--     bảng 51          P1 PHẢI | P2 TRÁI          (hiển thị: TRÁI | PHẢI)
--     bảng 53/57/65    P1 PHẢI | P2 LÊN | P3 TRÁI (hiển thị: TRÁI | LÊN | PHẢI)
--
-- Chỉ chạy khi cổng 0 còn mang TRÁI (trạng thái của file 61), nên chạy lại
-- không làm gì. Cảm biến thuộc cả hai cổng của bảng 51 giữ nguyên.
--
-- Sau khi chạy: POST /LedStatus/Reload (từ chính máy chủ).

START TRANSACTION;

-- k.dir0_cu = 3: cổng 0 đang mang TRÁI, tức chưa bù đấu nối ngược.
-- Bảng dẫn xuất DISTINCT ở NOT EXISTS: tránh lỗi 1093 của MySQL khi UPDATE một
-- bảng rồi đọc lại chính nó trong subquery.

UPDATE led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
JOIN  (          SELECT '51' AS code, 2 AS n, 3 AS dir0_cu
       UNION ALL SELECT '53', 3, 3
       UNION ALL SELECT '57', 3, 3
       UNION ALL SELECT '65', 3, 3
) k ON k.code = p.code
JOIN   led_panel_port p0 ON p0.panel_id = pb.panel_id AND p0.port_index = 0
SET    pb.port_index = k.n - 1 - pb.port_index
WHERE  p0.arrow_direction = k.dir0_cu
  AND  pb.port_index <> k.n - 1 - pb.port_index
  AND  NOT EXISTS (SELECT 1 FROM (SELECT DISTINCT panel_id, port_index, block_id
                                  FROM led_port_block) t
                   WHERE t.panel_id   = pb.panel_id
                     AND t.block_id   = pb.block_id
                     AND t.port_index = k.n - 1 - pb.port_index);

UPDATE led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
JOIN  (          SELECT '51' AS code, 2 AS n, 3 AS dir0_cu
       UNION ALL SELECT '53', 3, 3
       UNION ALL SELECT '57', 3, 3
       UNION ALL SELECT '65', 3, 3
) k ON k.code = p.code
JOIN   led_panel_port p0 ON p0.panel_id = ps.panel_id AND p0.port_index = 0
SET    ps.port_index = k.n - 1 - ps.port_index
WHERE  p0.arrow_direction = k.dir0_cu
  AND  ps.port_index <> k.n - 1 - ps.port_index
  AND  NOT EXISTS (SELECT 1 FROM (SELECT DISTINCT panel_id, port_index, zcu_id, lo, vi_tri
                                  FROM led_port_sensor) t
                   WHERE t.panel_id   = ps.panel_id
                     AND t.zcu_id     = ps.zcu_id
                     AND t.lo         = ps.lo
                     AND t.vi_tri     = ps.vi_tri
                     AND t.port_index = k.n - 1 - ps.port_index);

-- Hướng chạy CUỐI: hai câu trên đọc hướng cổng 0 để biết bảng nào còn cần đảo.
UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
JOIN   led_panel_port p0 ON p0.panel_id = pp.panel_id AND p0.port_index = 0
JOIN  (          SELECT '51' AS code, 0 AS port_index, 1 AS dir, 3 AS dir0_cu
       UNION ALL SELECT '51', 1, 3, 3
       UNION ALL SELECT '53', 0, 1, 3 UNION ALL SELECT '53', 1, 0, 3 UNION ALL SELECT '53', 2, 3, 3
       UNION ALL SELECT '57', 0, 1, 3 UNION ALL SELECT '57', 1, 0, 3 UNION ALL SELECT '57', 2, 3, 3
       UNION ALL SELECT '65', 0, 1, 3 UNION ALL SELECT '65', 1, 0, 3 UNION ALL SELECT '65', 2, 3, 3
) v ON v.code = p.code AND v.port_index = pp.port_index
SET    pp.arrow_direction = v.dir
WHERE  p0.arrow_direction = v.dir0_cu;

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
WHERE  p.code IN ('51', '53', '57', '58', '65', '66') AND o.is_active = 1
ORDER  BY CAST(p.code AS UNSIGNED), o.port_index;
