-- Do DO PHU cua tung zone: bao nhieu o co khi thuc su vua doc duoc tu PLC.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO =======================
-- So tren bang LED duoc tinh la "suc chua tru di o dang giu the". O nao chua doc
-- duoc van tinh la TRONG -- co y, vi loai chung khoi mau so thi con so tut xuong
-- thap den muc tai xe tuong ham gan day.
--
-- Cai gia cua lua chon do: khi nhieu PLC mat ket noi, con so tren bang LAC QUAN
-- hon su that ma khong co dau hieu gi. Do thuc te trua 17/09: vong quet chi doc
-- duoc 177/755 o, nghia la he thong khong nhin thay 578 o -- xe co do trong do
-- thi bang van bao trong.
--
-- View nay khong sua con so. No cung cap thu con thieu: MUC DO TIN CAY cua con
-- so, de tang LED doi mau xanh -> vang khi do phu xuong thap.
--
-- ======================= THE NAO LA "VUA DOC DUOC" =======================
-- read_at nhich moi lan quet thanh cong o do (changed_at moi chi nhich khi noi
-- dung doi). Vong quet chay moi 45 giay, nen mot o khoe phai co read_at trong
-- vong vai phut.
--
-- Nguong 5 phut = khoang 6 vong quet. Du rong de mot vong loi le khong bao dong
-- gia, du hep de phat hien PLC rung trong vai phut.
--
-- ======================= CHI AP DUNG CHO O CO KHI =======================
-- Cho do thuong (80 cho) khong co dong nao trong plc_slot_state -- chung do cam
-- bien PGS bao, ma phan do thi chua giai duoc goi tin. Do phu cua chung luon la
-- 0/0.
--
-- KHONG dung con so 0/0 do de bat mau vang cho dong do thuong: no se vang vinh
-- vien, va mot canh bao luc nao cung sang thi khong con la canh bao. Su khong
-- chac chan cua con so 80 la co dinh va da biet, khac han voi su khong chac chan
-- cua so o co khi von thay doi theo tinh trang PLC.

USE total_parking;

-- ------------------------------------------------- v_zone_coverage
CREATE OR REPLACE VIEW v_zone_coverage AS
SELECT b.zone_id,
       COUNT(*)                                                   AS o_co_khi,
       SUM(s.read_at IS NOT NULL
           AND s.read_at >= NOW() - INTERVAL 5 MINUTE)            AS o_vua_doc,
       CASE WHEN COUNT(*) = 0 THEN NULL
            ELSE ROUND(100 * SUM(s.read_at IS NOT NULL
                 AND s.read_at >= NOW() - INTERVAL 5 MINUTE) / COUNT(*))
       END                                                        AS do_phu_pct
FROM   plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
GROUP  BY b.zone_id;

-- ------------------------------------------------- v_led_capacity_zone
-- Giu nguyen moi cot cu, chi THEM ba cot do phu. Tang C# doc bang ten cot nen
-- them cot khong lam hong gi.
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
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm >  4800 THEN b.slot_count END),0) AS total_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm <= 4800 THEN b.slot_count END),0) AS total_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                 THEN b.slot_count END),0) AS total_standard
    FROM block b WHERE b.zone_id = z.zone_id AND b.is_active = 1
) t ON TRUE
JOIN LATERAL (
    SELECT
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm >  4800 THEN 1 END),0) AS used_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm <= 4800 THEN 1 END),0) AS used_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                 THEN 1 END),0) AS used_standard
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.zone_id = z.zone_id AND b.is_active = 1
) u ON TRUE
LEFT   JOIN v_zone_coverage cv ON cv.zone_id = z.zone_id
WHERE  z.is_active = 1;

-- ------------------------------------------------- v_led_capacity (toan bai)
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
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm >  4800 THEN b.slot_count END), 0) AS total_l5m,
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm <= 4800 THEN b.slot_count END), 0) AS total_l48m,
        COALESCE(SUM(CASE WHEN b.kind = 'Ground'       THEN b.slot_count END), 0) AS total_standard
    FROM block b WHERE b.is_active = 1
) t
CROSS JOIN (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm >  4800 THEN 1 END), 0) AS used_l5m,
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm <= 4800 THEN 1 END), 0) AS used_l48m,
        COALESCE(SUM(CASE WHEN b.kind = 'Ground'       THEN 1 END), 0) AS used_standard,
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
SELECT z.zone_id, c.o_co_khi AS tong_o, c.o_vua_doc AS vua_doc, c.do_phu_pct AS phan_tram
FROM   v_zone_coverage c JOIN zone z ON z.zone_id = c.zone_id ORDER BY z.zone_id;

SELECT zone_id, total_l5m, free_l5m, slots_total, slots_fresh,
       ROUND(100*slots_fresh/NULLIF(slots_total,0)) AS do_phu_pct
FROM   v_led_capacity_zone ORDER BY zone_id;

SELECT total_l5m, free_l5m, slots_total, slots_fresh,
       ROUND(100*slots_fresh/NULLIF(slots_total,0)) AS do_phu_pct
FROM   v_led_capacity;
