-- Anh xa mui ten tung bang LED -> zone, do KHACH HANG cung cap.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO CAN =======================
-- Truoc file nay ca 12 bang deu scope = TOTAL: hien so TOAN BAI voi mui ten an.
-- Dung su that nhung vo dung — bang chi huong ma khong chi duoc huong nao.
--
-- Vi tri bang tren ban ve KHONG suy ra duoc dieu nay. Da thu: tim 11 cum nhan
-- "LOTS AVAILABLE" roi gan zone theo block gan nhat, ket qua 4/11 cum nam ngay
-- RANH GIOI hai ba zone. Va bang 57/65 moi con co 3 mui ten tu cung mot cho ->
-- toa do khong the phan biet duoc. Phai hoi nguoi di thuc dia.
--
-- ======================= DINH DANG =======================
-- zone_list: danh sach zone_id cach nhau dau phay, vd '5,4,6'.
-- Mot mui ten dan toi nhieu zone thi CONG suc chua cua chung lai — tai xe re
-- theo huong do se toi duoc bat ky zone nao trong danh sach.
--
-- ======================= LUU Y VE MUI TEN =======================
-- Khach cho biet mui ten DAN TOI zone nao, nhung KHONG cho biet mui ten CHI VE
-- HUONG NAO tren man hinh (len/phai/xuong/trai). Nen van de arrow_color = 0 (den
-- = an). Bat mui ten voi huong doan bua se chi tai xe di sai duong — te hon la
-- khong chi gi.
-- Khi co huong that: UPDATE arrow_direction (0=len 1=phai 2=xuong 3=trai) va
-- arrow_color (2=xanh) cho tung cong.

USE total_parking;

-- ------------------------------------------------- v_led_capacity_zone
-- Suc chua theo TUNG ZONE, cung ba bo dem voi v_led_capacity.
-- Bo day cong don cac zone ma mui ten dan toi.
CREATE OR REPLACE VIEW v_led_capacity_zone AS
SELECT z.zone_id,
       t.total_l5m, t.total_l48m, t.total_standard,
       u.used_l5m,  u.used_l48m,  u.used_standard,
       GREATEST(CAST(t.total_l5m      AS SIGNED) - u.used_l5m,      0) AS free_l5m,
       GREATEST(CAST(t.total_l48m     AS SIGNED) - u.used_l48m,     0) AS free_l48m,
       GREATEST(CAST(t.total_standard AS SIGNED) - u.used_standard, 0) AS free_standard
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
    FROM parking_session s JOIN block b ON b.block_id = s.block_id
    WHERE s.active_card_id IS NOT NULL AND b.zone_id = z.zone_id
) u ON TRUE
WHERE  z.is_active = 1;

-- ------------------------------------------------- anh xa mui ten -> zone
UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
SET    pp.scope = 'ZONES',
       pp.zone_list = CASE
           WHEN p.code = '51' AND pp.port_index = 0 THEN '4'
           WHEN p.code = '52' AND pp.port_index = 0 THEN '5'
           WHEN p.code = '53' AND pp.port_index = 0 THEN '5,4,6'
           WHEN p.code = '54' AND pp.port_index = 0 THEN '2'
           WHEN p.code = '55' AND pp.port_index = 0 THEN '4,6'
           WHEN p.code = '56' AND pp.port_index = 0 THEN '3'
           WHEN p.code = '57' AND pp.port_index = 0 THEN '2'
           WHEN p.code = '57' AND pp.port_index = 1 THEN '2'
           WHEN p.code = '57' AND pp.port_index = 2 THEN '1'
           WHEN p.code = '58' AND pp.port_index = 0 THEN '5,6'
           WHEN p.code = '65' AND pp.port_index = 0 THEN '2'
           WHEN p.code = '65' AND pp.port_index = 1 THEN '6'
           WHEN p.code = '65' AND pp.port_index = 2 THEN '2'
           WHEN p.code = '66' AND pp.port_index = 0 THEN '2,1'
           WHEN p.code = '67' AND pp.port_index = 0 THEN '2'
       END,
       pp.note = 'Mui ten dan toi zone nay (khach cung cap). Huong hien thi chua biet -> mui ten van an.'
WHERE  p.code IN ('51','52','53','54','55','56','57','58','65','66','67');

-- Bang dau ham giu TOTAL: no la bang tong o loi vao, khong chi huong.
UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
SET    pp.scope = 'TOTAL', pp.zone_list = NULL,
       pp.note = 'Bang tong o loi vao - hien suc chua toan bai'
WHERE  p.code = '50';

-- ------------------------------------------------------------------ doi chieu
SELECT p.code, p.name, pp.port_index, pp.scope, pp.zone_list, pp.is_active
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
ORDER  BY p.code + 0, pp.port_index;

SELECT * FROM v_led_capacity_zone ORDER BY zone_id;

-- Cong nao con thieu anh xa (se bi bo qua khi day)
SELECT p.code, pp.port_index
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE  pp.scope = 'ZONES' AND (pp.zone_list IS NULL OR pp.zone_list = '');
