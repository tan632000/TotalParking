-- Doi cach dem hai bo dem co khi: HAI NGUONG CHIEU DAI XE, khong phai hai loai khoang.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= KHACH DA CHOT (17/09) =======================
-- "L < 5 M" va "L < 4.8 M" tren mat bang LED la CHIEU DAI XE, khong phai chieu
-- dai khoang. Nen hai bo dem LONG NHAU chu khong roi nhau:
--
--   dong L < 5 M    = so o trong nhan duoc xe dai duoi 5.0 m
--                   = CHI khoang 5000L  (xe 4.8-5.0 m khong lot khoang 4800L)
--
--   dong L < 4.8 M  = so o trong nhan duoc xe dai duoi 4.8 m
--                   = TAT CA khoang, ca 4800L lan 5000L
--                     (xe 4.5 m do duoc ca hai loai khoang)
--
-- Suy ra: L < 4.8 M LUON LON HON HOAC BANG L < 5 M. Neu thay nguoc lai thi co
-- loi o dau do.
--
-- ======================= TRUOC DAY SAI THE NAO =======================
-- File 24 va 29 dem hai bo roi nhau:
--     l5m  = bay_length_mm >  4800
--     l48m = bay_length_mm <= 4800
--
-- Voi du lieu hien tai (ca 112 block deu 5000mm) thi l48m = 0, va dong L < 4.8 M
-- tren bang bi tat. Do la cai loi khach bao.
--
-- ======================= HE QUA NGAY =======================
-- Sau file nay, dong L < 4.8 M SANG LEN voi so that (754) ma KHONG can biet
-- block nao la loai 4800L -- vi no dem tat ca khoang.
--
-- So hieu 3 block loai 4.8 m chi con anh huong toi dong L < 5 M: khi gan xong,
-- dong do se tut tu 755 xuong 755 tru di so o cua 3 block ay. Van can hoi khach,
-- nhung khong con chan viec hien thi nua.

USE total_parking;

CREATE OR REPLACE VIEW v_led_capacity_zone AS
SELECT z.zone_id,
       t.total_l5m, t.total_l48m, t.total_standard,
       u.used_l5m,  u.used_l48m,  u.used_standard,
       GREATEST(CAST(t.total_l5m      AS SIGNED) - u.used_l5m,      0) AS free_l5m,
       GREATEST(CAST(t.total_l48m     AS SIGNED) - u.used_l48m,     0) AS free_l48m,
       GREATEST(CAST(t.total_standard AS SIGNED) - u.used_standard, 0) AS free_standard,
       COALESCE(cv.o_co_khi,  0) AS slots_total,
       COALESCE(cv.o_vua_doc, 0) AS slots_fresh
FROM   zone z
JOIN LATERAL (
    SELECT
      -- xe duoi 5.0 m: chi khoang 5000L
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN b.slot_count END),0) AS total_l5m,
      -- xe duoi 4.8 m: MOI khoang co khi
      COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN b.slot_count END),0) AS total_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                THEN b.slot_count END),0) AS total_standard
    FROM block b WHERE b.zone_id = z.zone_id AND b.is_active = 1
) t ON TRUE
JOIN LATERAL (
    SELECT
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN 1 END),0) AS used_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN 1 END),0) AS used_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                THEN 1 END),0) AS used_standard
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.zone_id = z.zone_id AND b.is_active = 1
) u ON TRUE
LEFT   JOIN v_zone_coverage cv ON cv.zone_id = z.zone_id
WHERE  z.is_active = 1;

CREATE OR REPLACE VIEW v_led_capacity AS
SELECT
    t.total_l5m, t.total_l48m, t.total_standard,
    u.used_l5m,  u.used_l48m,  u.used_standard, u.used_unassigned,
    GREATEST(CAST(t.total_l5m      AS SIGNED) - u.used_l5m,      0) AS free_l5m,
    GREATEST(CAST(t.total_l48m     AS SIGNED) - u.used_l48m,     0) AS free_l48m,
    GREATEST(CAST(t.total_standard AS SIGNED) - u.used_standard, 0) AS free_standard,
    cv.slots_total, cv.slots_fresh
FROM (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN b.slot_count END),0) AS total_l5m,
        COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN b.slot_count END),0) AS total_l48m,
        COALESCE(SUM(CASE WHEN b.kind='Ground'                                THEN b.slot_count END),0) AS total_standard
    FROM block b WHERE b.is_active = 1
) t
CROSS JOIN (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN 1 END),0) AS used_l5m,
        COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN 1 END),0) AS used_l48m,
        COALESCE(SUM(CASE WHEN b.kind='Ground'                                THEN 1 END),0) AS used_standard,
        (SELECT COUNT(*) FROM parking_session ps
         WHERE ps.active_card_id IS NOT NULL AND ps.block_id IS NULL) AS used_unassigned
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.is_active = 1
) u
CROSS JOIN (
    SELECT COALESCE(SUM(o_co_khi),0) AS slots_total,
           COALESCE(SUM(o_vua_doc),0) AS slots_fresh
    FROM   v_zone_coverage
) cv;

-- ------------------------------------------------------------------ doi chieu
SELECT 'toan bai' AS pham_vi, total_l5m, free_l5m, total_l48m, free_l48m,
       (free_l48m >= free_l5m) AS long_nhau_dung
FROM   v_led_capacity
UNION ALL
SELECT CONCAT('zone ',zone_id), total_l5m, free_l5m, total_l48m, free_l48m,
       (free_l48m >= free_l5m)
FROM   v_led_capacity_zone ORDER BY pham_vi;

-- Phan bo chieu dai khoang hien tai. Khi khach cho biet 3 block loai 4.8 m thi
-- dong 4800 moi xuat hien, va total_l5m se giam di dung so o cua chung.
SELECT bay_length_mm, COUNT(*) AS so_block, SUM(slot_count) AS so_o
FROM   block WHERE kind='Mechanical' AND is_active=1 GROUP BY bay_length_mm;
