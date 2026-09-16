-- Bat bo day LED cho ca 12 bang.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= TRUOC KHI CHAY FILE NAY =======================
-- Chi bang dau ham (.50) duoc bat; 11 bang chi huong deu is_active = 0 va 8 trong
-- so do KHONG co dong nao trong led_panel_port. Hau qua: bo day chi biet toi mot
-- bang, 11 bang con lai phai day tay.
--
-- ======================= SCOPE = TOTAL, KHONG PHAI ZONES =======================
-- 11 bang chi huong dung ra phai hien so cua rieng zone ma mui ten dan toi. Nhung
-- cot zone_list dang NULL het: CHUA BIET bang nao chi ve zone nao. Ban ve co vi tri
-- bang LED, khong co thong tin mui ten -> zone.
--
-- Nen tam de scope = TOTAL va AN MUI TEN (arrow_color = 0 = den):
--   * ba bo dem doc la "toan bai con bao nhieu" -> dung su that
--   * hien mui ten kem so tong la noi "di huong nay con ngan ay cho" -> SAI, va
--     tai xe se di theo
--
-- Khi biet bang nao chi ve zone nao: doi scope thanh ZONES va dien zone_list.
-- So lieu theo zone DA CO SAN (v_zone_capacity), chi thieu dung anh xa do.

USE total_parking;

-- Bang chua co cong nao -> tao cong 0.
-- 57 va 65 la loai 3 huong, 56 la 1 huong: cac bang do da co san dong cong.
INSERT INTO led_panel_port
    (panel_id, port_index, scope, zone_list, arrow_direction, arrow_color, arrow_state, is_active, note)
SELECT p.panel_id, 0, 'TOTAL', NULL, 0, 0, 0, 1,
       'Tam hien so toan bai, an mui ten - chua biet mui ten dan toi zone nao'
FROM   led_panel p
WHERE  NOT EXISTS (SELECT 1 FROM led_panel_port pp WHERE pp.panel_id = p.panel_id)
ON DUPLICATE KEY UPDATE is_active = 1;

-- Cac cong da ton tai: chuyen ve TOTAL, an mui ten, bat len.
UPDATE led_panel_port
SET    scope           = 'TOTAL',
       zone_list       = NULL,
       arrow_color     = 0,      -- den = an mui ten
       arrow_direction = 0,
       arrow_state     = 0,
       is_active       = 1,
       note            = 'Tam hien so toan bai, an mui ten - chua biet mui ten dan toi zone nao';

UPDATE led_panel SET is_active = 1;

-- ------------------------------------------------------------------ doi chieu
SELECT p.code, p.name, p.ip_address, p.kind, p.is_active AS bang_bat,
       COUNT(pp.port_index) AS so_cong, SUM(pp.is_active) AS cong_bat,
       GROUP_CONCAT(DISTINCT pp.scope) AS scope
FROM   led_panel p LEFT JOIN led_panel_port pp ON pp.panel_id = p.panel_id
GROUP  BY p.panel_id ORDER BY p.code + 0;

SELECT COUNT(*) AS tong_cong_se_day FROM led_panel_port pp
JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE pp.is_active = 1 AND p.is_active = 1;
