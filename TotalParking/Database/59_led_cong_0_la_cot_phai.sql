-- =========================================================================
-- Bảng LED nhiều hướng: CỔNG 0 LÀ CỘT PHẢI NHẤT
-- =========================================================================
-- MySQL 8.0.19+. Chạy lại nhiều lần an toàn. UTF-8 KHÔNG BOM.
-- Chỉ UPDATE -> tài khoản ứng dụng chạy được.
--
-- ===================== QUY TẮC HIỂN THỊ =====================
--
-- Mũi tên TRÁI nằm cột trái, LÊN ở giữa, PHẢI nằm cột phải.
--
-- ===================== CỔNG NÀO LÀ CỘT NÀO =====================
--
-- port_index đánh từ PHẢI sang TRÁI: cổng 0 là cột phải nhất. Ngược với
-- Hình 3/4 của giaothucketnoi.pdf, và đã hai lần bị đoán sai theo tài liệu đó:
--
--   26_led_arrow_directions.sql  giả định cổng 0 = cột trái -> khách báo ngược
--                                (17/09), 31_led_arrow_swap_lr.sql đảo lại.
--   bản đầu của file này (12/10) lại giả định cổng 0 = cột trái và đảo gương
--                                cả bảng -> ảnh hiện trường chụp SAU khi chạy
--                                cho thấy cột trái của bảng 65 hiện 0435, là số
--                                của cổng 2 (hướng PHẢI, khối 1~62); cột trái
--                                bảng 51 hiện số của cổng 1. Ba bảng 51/57/65
--                                cùng một kết luận.
--
-- Mũi tên và con số của nó luôn đi đúng cặp trên mọi ảnh, nên dữ liệu khách
-- (LumiSlotsMatrix.xlsx) đúng. Chỉ có cột là sai.
--
-- Trạng thái đúng vì thế là trạng thái TRƯỚC bản đầu của file này:
--
--     bảng 51          cổng 1 TRÁI | cổng 0 PHẢI
--     bảng 53/57/65    cổng 2 TRÁI | cổng 1 LÊN | cổng 0 PHẢI
--     bảng 58          cổng 1 LÊN  | cổng 0 PHẢI
--     bảng 66          cổng 1 TRÁI | cổng 0 LÊN
--
-- ===================== FILE NÀY LÀM GÌ =====================
--
-- Đảo gương lại (2 cổng: 0<->1; 3 cổng: 0<->2), mang theo danh sách khối và
-- cảm biến của từng hướng — đúng phép biến đổi bản đầu đã làm, theo chiều
-- ngược lại. Chỉ chạy trên bảng mà cổng 0 còn mang hướng của bản đầu (TRÁI,
-- hoặc LÊN với bảng 58), nên trên DB chưa từng chạy bản đầu thì file này
-- không làm gì.
--
-- Bản đầu ĐÃ BỊ THAY bởi file này và không còn trong repo — chạy lại nó sẽ
-- đảo sai một lần nữa.
--
-- Cảm biến thuộc cả hai cổng (bảng 51 và 66) không đụng tới: đảo gương thì nó
-- vẫn thuộc cả hai, đổi nó sẽ đâm vào khoá chính của dòng song sinh.
--
-- Bảng 55 không đổi: hai cổng giống hệt nhau, cùng LÊN (xem file 57).
--
-- Sau khi chạy: POST /LedStatus/Reload (từ chính máy chủ) để vòng đẩy nạp lại
-- cấu hình cổng, không cần khởi động lại ứng dụng.

START TRANSACTION;

-- k.dir0_sai: hướng cổng 0 đang mang khi bảng còn ở trạng thái SAI
-- (0 = lên, 1 = phải, 3 = trái).
--
-- Bảng dẫn xuất DISTINCT ở NOT EXISTS: MySQL cấm UPDATE một bảng rồi đọc lại
-- chính nó trong subquery (lỗi 1093). DISTINCT buộc bảng dẫn xuất được dựng
-- riêng trước, không bị gộp vào câu UPDATE.

-- --------------------------------------------------------- khối cơ khí
UPDATE led_port_block pb
JOIN   led_panel p ON p.panel_id = pb.panel_id
JOIN  (          SELECT '51' AS code, 2 AS n, 3 AS dir0_sai
       UNION ALL SELECT '53', 3, 3
       UNION ALL SELECT '57', 3, 3
       UNION ALL SELECT '58', 2, 0
       UNION ALL SELECT '65', 3, 3
       UNION ALL SELECT '66', 2, 3
) k ON k.code = p.code
JOIN   led_panel_port p0 ON p0.panel_id = pb.panel_id AND p0.port_index = 0
SET    pb.port_index = k.n - 1 - pb.port_index
WHERE  p0.arrow_direction = k.dir0_sai
  AND  pb.port_index <> k.n - 1 - pb.port_index
  AND  NOT EXISTS (SELECT 1 FROM (SELECT DISTINCT panel_id, port_index, block_id
                                  FROM led_port_block) t
                   WHERE t.panel_id   = pb.panel_id
                     AND t.block_id   = pb.block_id
                     AND t.port_index = k.n - 1 - pb.port_index);

-- --------------------------------------------------------- cảm biến đỗ nền
UPDATE led_port_sensor ps
JOIN   led_panel p ON p.panel_id = ps.panel_id
JOIN  (          SELECT '51' AS code, 2 AS n, 3 AS dir0_sai
       UNION ALL SELECT '53', 3, 3
       UNION ALL SELECT '57', 3, 3
       UNION ALL SELECT '58', 2, 0
       UNION ALL SELECT '65', 3, 3
       UNION ALL SELECT '66', 2, 3
) k ON k.code = p.code
JOIN   led_panel_port p0 ON p0.panel_id = ps.panel_id AND p0.port_index = 0
SET    ps.port_index = k.n - 1 - ps.port_index
WHERE  p0.arrow_direction = k.dir0_sai
  AND  ps.port_index <> k.n - 1 - ps.port_index
  AND  NOT EXISTS (SELECT 1 FROM (SELECT DISTINCT panel_id, port_index, zcu_id, lo, vi_tri
                                  FROM led_port_sensor) t
                   WHERE t.panel_id   = ps.panel_id
                     AND t.zcu_id     = ps.zcu_id
                     AND t.lo         = ps.lo
                     AND t.vi_tri     = ps.vi_tri
                     AND t.port_index = k.n - 1 - ps.port_index);

-- --------------------------------------------------------- hướng mũi tên
-- Ghi tường minh hướng ĐÚNG của từng cổng: cổng 0 là cột phải, nên đọc bảng
-- dưới đây từ dưới lên là thấy bảng hiện từ trái sang phải. Chạy CUỐI CÙNG:
-- hai câu trên đọc hướng cổng 0 để biết bảng nào còn cần đảo.
UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
JOIN   led_panel_port p0 ON p0.panel_id = pp.panel_id AND p0.port_index = 0
JOIN  (          SELECT '51' AS code, 0 AS port_index, 1 AS dir, 3 AS dir0_sai
       UNION ALL SELECT '51', 1, 3, 3
       UNION ALL SELECT '53', 0, 1, 3 UNION ALL SELECT '53', 1, 0, 3 UNION ALL SELECT '53', 2, 3, 3
       UNION ALL SELECT '57', 0, 1, 3 UNION ALL SELECT '57', 1, 0, 3 UNION ALL SELECT '57', 2, 3, 3
       UNION ALL SELECT '58', 0, 1, 0 UNION ALL SELECT '58', 1, 0, 0
       UNION ALL SELECT '65', 0, 1, 3 UNION ALL SELECT '65', 1, 0, 3 UNION ALL SELECT '65', 2, 3, 3
       UNION ALL SELECT '66', 0, 0, 3 UNION ALL SELECT '66', 1, 3, 3
) v ON v.code = p.code AND v.port_index = pp.port_index
SET    pp.arrow_direction = v.dir
WHERE  p0.arrow_direction = v.dir0_sai;

COMMIT;

-- ------------------------------------------------------------------ đối chiếu
-- Xếp cổng GIẢM dần = thứ tự cột từ TRÁI sang PHẢI trên bảng thật.
SELECT p.code AS bang, o.port_index AS cong,
       ELT(o.arrow_direction + 1, 'LEN', 'PHAI', 'XUONG', 'TRAI') AS huong,
       (SELECT GROUP_CONCAT(b.block_no ORDER BY b.block_no)
          FROM led_port_block pb JOIN block b ON b.block_id = pb.block_id
         WHERE pb.panel_id = o.panel_id AND pb.port_index = o.port_index) AS khoi,
       (SELECT COUNT(*) FROM led_port_sensor ps
         WHERE ps.panel_id = o.panel_id AND ps.port_index = o.port_index) AS cam_bien
FROM   led_panel_port o JOIN led_panel p ON p.panel_id = o.panel_id
WHERE  p.code IN ('51', '53', '57', '58', '65', '66') AND o.is_active = 1
ORDER  BY CAST(p.code AS UNSIGNED), o.port_index DESC;
