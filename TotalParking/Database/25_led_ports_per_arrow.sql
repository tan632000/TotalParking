-- Tach so cho trong theo TUNG MUI TEN, thay vi cong don ca bang.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= SAI SOT DANG SUA =======================
-- Bang anh xa khach gui truoc day ghi vd bang 53 la "ZONE 5 / ZONE 4 / ZONE 6".
-- File 19 hieu la MOT mui ten dan toi ca ba zone, nen CONG suc chua lai:
-- 109+152+55 = 316, va day 316 xuong dung mot cong.
--
-- Cach hieu do SAI. Khach vua cho biet so mui ten thuc te cua tung bang, va so
-- mui ten trung KHIT voi so zone trong danh sach o 10/11 bang:
--
--   bang 53: 3 mui ten (trai/phai/len)  <-> 3 zone (5,4,6)
--   bang 55: 2 mui ten (len/phai)       <-> 2 zone (4,6)
--   bang 58: 2 mui ten (len/phai)       <-> 2 zone (5,6)
--   bang 66: 2 mui ten (len/trai)       <-> 2 zone (2,1)
--   bang 57: 3 mui ten                  <-> 3 zone (2,2,1)   [da dung tu dau]
--   bang 65: 3 mui ten                  <-> 3 zone (2,6,2)   [da dung tu dau]
--
-- Bang 57 va 65 khach ghi ro "Cong 0,1,2" -> chung minh thu tu danh sach zone
-- CHINH LA thu tu cong. Vay dau gach cheo la "moi mui ten mot zone", khong phai
-- "cong gop".
--
-- Hau qua cua cach hieu cu: bon bang 53/55/58/66 dang hien mot con so lon gap
-- 2-3 lan su that tren MOT cot, trong khi cac cot con lai cua chinh bang do
-- khong duoc ghi gi. Tai xe re theo mui ten se toi mot zone nho hon nhieu so voi
-- con so ho vua doc.
--
-- ======================= THU TU CONG =======================
-- Theo tai lieu giao thuc (Hinh 3/4/5): P1 la cot TRAI NHAT cua mat bang,
-- roi sang phai. Danh sach zone cua khach xep cung thu tu do.
--
-- ======================= VI SAO VAN TAT MUI TEN =======================
-- Khach cho biet MOI BANG co nhung huong nao, nhung khong cho biet HUONG NAO
-- THUOC CONG NAO. Voi bang 1 mui ten thi khong the nham -> bat den xanh.
-- Voi bang nhieu mui ten thi van de arrow_color = 0 (den = an).
--
-- Chi sai huong con te hon khong chi gi: con so dung ma mui ten sai thi tai xe
-- doc duoc "con 152 cho" roi re nham sang zone khac.
--
-- ======================= BANG 51 =======================
-- Khach bao 2 mui ten (trai/phai) nhung danh sach zone chi co MOT: ZONE 4.
-- Day la bang duy nhat lech. Chua ro mui ten con lai dan toi dau, nen cong 1
-- de is_active = 0: cot do se de TRONG thay vi hien mot con so doan bua.

USE total_parking;

-- ------------------------------------------------- them cong con thieu
INSERT INTO led_panel_port (panel_id, port_index, arrow_direction, arrow_color, arrow_state,
                            scope, zone_list, is_active, note)
SELECT p.panel_id, v.port_index, 0, 0, 0, 'ZONES', v.zone_list, v.is_active, v.note
FROM   led_panel p
JOIN ( SELECT '51' AS code, 1 AS port_index, NULL  AS zone_list, 0 AS is_active,
              'Khach bao 2 mui ten nhung chi cho 1 zone - chua ro cot nay dan toi dau' AS note
       UNION ALL SELECT '53', 1, '4',  1, NULL
       UNION ALL SELECT '53', 2, '6',  1, NULL
       UNION ALL SELECT '55', 1, '6',  1, NULL
       UNION ALL SELECT '58', 1, '6',  1, NULL
       UNION ALL SELECT '66', 1, '1',  1, NULL
) v ON v.code = p.code
ON DUPLICATE KEY UPDATE
       zone_list = VALUES(zone_list),
       is_active = VALUES(is_active),
       note      = VALUES(note);

-- ------------------------------------------------- sua cong 0 cua cac bang da tach
-- Cong 0 dang giu ca danh sach gop; gio chi giu zone DAU TIEN.
UPDATE led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
SET    pp.zone_list = CASE p.code
           WHEN '53' THEN '5'
           WHEN '55' THEN '4'
           WHEN '58' THEN '5'
           WHEN '66' THEN '2'
       END
WHERE  pp.port_index = 0 AND p.code IN ('53','55','58','66');

-- ------------------------------------------------- huong mui ten (chi bang 1 huong)
-- 'len' = Up = 0, mau 2 = xanh, 0 = dung yen.
UPDATE led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
SET    pp.arrow_direction = 0,
       pp.arrow_color     = 2,
       pp.arrow_state     = 0
WHERE  pp.port_index = 0 AND p.code IN ('52','54','56','67');

-- ------------------------------------------------------------------ doi chieu

-- 1. Toan bo cau hinh cong sau khi sua
SELECT p.code AS bang, pp.port_index AS cong,
       CASE pp.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END AS huong,
       CASE pp.arrow_color WHEN 0 THEN 'AN' WHEN 1 THEN 'do'
            WHEN 2 THEN 'xanh' ELSE 'vang' END AS mau_mui_ten,
       pp.zone_list, pp.is_active AS bat, pp.note
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
ORDER  BY p.code, pp.port_index;

-- 2. So se len tung cot, so sanh voi so dang hien truoc khi sua
SELECT p.code AS bang, pp.port_index AS cong, pp.zone_list,
       c.free_l5m AS co_khi, c.free_standard AS do_thuong
FROM   led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
LEFT   JOIN v_led_capacity_zone c ON c.zone_id = pp.zone_list
WHERE  pp.scope = 'ZONES' AND pp.is_active = 1
ORDER  BY p.code, pp.port_index;

-- 3. Cong nao con thieu anh xa (se de trong)
SELECT p.code, pp.port_index, pp.note
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE  pp.scope = 'ZONES' AND (pp.zone_list IS NULL OR pp.zone_list = '' OR pp.is_active = 0);
