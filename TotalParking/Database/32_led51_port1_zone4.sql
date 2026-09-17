-- Bat cong 1 (cot TRAI) cua bang LED 51, dan toi zone 4.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi UPDATE -> tai khoan ung dung chay duoc.
--
-- ======================= VI SAO =======================
-- Bang 51 co HAI mui ten, nhung bang anh xa khach gui ban dau chi ghi mot zone
-- (ZONE 4). Khong ro cot con lai dan toi dau nen file 25 de cong 1 is_active = 0.
--
-- Hau qua ngoai hien truong: cot do KHONG BAO GIO duoc ghi, nen no giu nguyen
-- noi dung tu luc bat nguon bo mach -- hien '0001' mau DO o ca ba dong, dung im
-- nhieu ngay. Bang LED giu noi dung cuoi cung vinh vien va khong co watchdog.
--
-- Khach xac nhan 17/09: CA HAI mui ten deu dan toi ZONE 4 (hai loi di khac nhau
-- vao cung mot khu). Bang anh xa khong thieu, chi ghi gon mot dong cho ca hai.
--
-- ======================= CACH NHAN DANG BANG 51 =======================
-- Khach doc so tren bang canh block 30:
--   cot PHAI  0152 vang / 0152 vang / 0017 xanh   <- trung khit zone 4
--   cot TRAI  0001 do   / 0001 do   / 0001 do     <- cot chua tung duoc ghi
--
-- Ba con so 152/152/17 chi co o zone 4, nen day chac chan la bang 51.
--
-- ======================= HE QUA PHU: P1 NAM BEN PHAI =======================
-- Cot do minh ghi la cong 0 (= P1 theo giao thuc), va no nam ben PHAI. Tai lieu
-- nha cung cap ve P1 la cot TRAI NHAT -- thuc te nguoc lai.
-- Day la xac nhan bang quan sat cho viec dao huong mui ten o file 31.

USE total_parking;

UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
SET    pp.zone_list = '4',
       pp.is_active = 1,
       pp.note      = 'Ca hai mui ten deu dan toi zone 4 - khach xac nhan 17/09'
WHERE  p.code = '51' AND pp.port_index = 1;

-- ------------------------------------------------------------------ doi chieu
SELECT p.code AS bang, pp.port_index AS cong,
       CASE pp.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END AS huong,
       pp.scope, pp.zone_list AS zone, pp.is_active AS bat, pp.note
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE  p.code = '51' ORDER BY pp.port_index;

-- Con cong nao chua duoc ghi khong
SELECT p.code, pp.port_index, pp.zone_list, pp.is_active
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE  pp.is_active = 0 OR (pp.scope = 'ZONES' AND (pp.zone_list IS NULL OR pp.zone_list = ''));
