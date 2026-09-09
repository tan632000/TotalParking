-- =====================================================================
--  DU LIEU GIA DE MO PHONG. KHONG PHAI DU LIEU THAT CUA BAI XE.
-- =====================================================================
--
-- Muc dich: cho trang Home/FloorPlan co so lieu de hien thi va cho
-- ZoneRouter co zone de chon, TRUOC khi lay duoc bang nang luc block
-- tu file CAD goc (docs/parking-session-db-design.md muc 12.1).
--
-- So block, so o, va ti le tang duoi o day la BIA RA. Chung chi giu
-- dung ba tinh chat can cho phan mo phong chay dung:
--   - moi zone deu co block, de co 6 zone cung tham gia dieu huong;
--   - co ca block Mechanical va Ground, de nhanh THUONG khong bi ket;
--   - so o khac nhau giua cac zone, de thay tac dung cua gate_rank
--     va cua viec can bang tai.
--
-- XOA TOAN BO du lieu nay khi co bang nang luc block that:
--   DELETE FROM parking_slot WHERE block_id IN
--     (SELECT block_id FROM block WHERE block_no BETWEEN 900 AND 999);
--   DELETE FROM block WHERE block_no BETWEEN 900 AND 999;
--
-- block_no tu 900 tro len de khong bao gio dam vao dai 1..112 cua ban ve.

USE total_parking;

-- gate_rank: thu tu gan -> xa tinh tu cong vao. Sau con so nay CUNG LA BIA:
-- 6 con so that can khao sat, xem muc 12.10 cua tai lieu thiet ke.
UPDATE zone SET gate_rank = 1 WHERE zone_id = 1;
UPDATE zone SET gate_rank = 2 WHERE zone_id = 2;
UPDATE zone SET gate_rank = 3 WHERE zone_id = 6;
UPDATE zone SET gate_rank = 4 WHERE zone_id = 4;
UPDATE zone SET gate_rank = 5 WHERE zone_id = 5;
UPDATE zone SET gate_rank = 6 WHERE zone_id = 3;

-- Moi zone: mot block co khi 10 o (2 tang x 5 cot) + mot cum do nen 6 cho.
INSERT INTO block
    (zone_id, block_no, kind, slot_count, bay_length_mm, tier_count, column_count)
VALUES
    (1, 901, 'Mechanical', 10, 5000, 2, 5),
    (2, 902, 'Mechanical', 10, 5000, 2, 5),
    (3, 903, 'Mechanical', 10, 5000, 2, 5),
    (4, 904, 'Mechanical', 10, 5000, 2, 5),
    (5, 905, 'Mechanical', 10, 5000, 2, 5),
    (6, 906, 'Mechanical', 10, 5000, 2, 5),
    (1, 951, 'Ground', 6, NULL, NULL, NULL),
    (2, 952, 'Ground', 6, NULL, NULL, NULL),
    (3, 953, 'Ground', 6, NULL, NULL, NULL),
    (4, 954, 'Ground', 6, NULL, NULL, NULL),
    (5, 955, 'Ground', 6, NULL, NULL, NULL),
    (6, 956, 'Ground', 6, NULL, NULL, NULL) AS new
ON DUPLICATE KEY UPDATE
    zone_id = new.zone_id, kind = new.kind, slot_count = new.slot_count,
    bay_length_mm = new.bay_length_mm, tier_count = new.tier_count,
    column_count = new.column_count;

-- Sinh o cho cac block co khi demo: tier 0..1 x col 0..4.
INSERT INTO parking_slot (block_id, slot_index, label, tier, col_index)
SELECT b.block_id,
       t.n * b.column_count + c.n,
       CONCAT('P', LPAD(t.n * b.column_count + c.n + 1, 2, '0')),
       t.n, c.n
FROM   block b
JOIN   (SELECT 0 AS n UNION ALL SELECT 1) t
JOIN   (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2
        UNION ALL SELECT 3 UNION ALL SELECT 4) c
WHERE  b.block_no BETWEEN 901 AND 906
  AND  t.n < b.tier_count
  AND  c.n < b.column_count
ON DUPLICATE KEY UPDATE slot_id = slot_id;
