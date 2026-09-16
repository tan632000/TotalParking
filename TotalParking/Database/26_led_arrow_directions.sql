-- Bat mui ten cho cac bang NHIEU huong.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= DU LIEU GOC =======================
-- Khach cho biet moi bang co nhung huong nao:
--   led 1  (.51) 2 huong  trai/phai
--   led 2  (.52) 1 huong  len          [da bat o file 25]
--   led 3  (.53) 3 huong  trai/phai/len
--   led 4  (.54) 1 huong  len          [da bat o file 25]
--   led 5  (.55) 2 huong  len/phai
--   led 6  (.56) 1 huong  len          [da bat o file 25]
--   led 7  (.57) 3 huong  trai/phai/len
--   led 8  (.58) 2 huong  len/phai
--   led 9  (.65) 3 huong  trai/phai/len
--   led 10 (.66) 2 huong  len/trai
--   led 11 (.67) 1 huong  len          [da bat o file 25]
--
-- ======================= QUY TAC GAN CONG =======================
-- Khach cho biet TAP HOP huong cua moi bang, khong cho biet huong nao thuoc
-- cong nao. Gan theo quy uoc cua chinh nha cung cap (giaothucketnoi.pdf):
--
--   Hinh 3 (bang 3 huong): P1 = cot TRAI, P2 = cot GIUA, P3 = cot PHAI,
--                          va ve san ba mui ten theo thu tu  <-  ^  ->
--   Hinh 4 (bang 2 huong): P1 = tam TRAI, P2 = tam PHAI
--
-- Vay: xep mui ten tu trai sang phai theo chinh huong no chi.
--   {trai, len, phai} -> P1=trai, P2=len, P3=phai
--   {len, phai}       -> P1=len,  P2=phai
--   {len, trai}       -> P1=trai, P2=len
--   {trai, phai}      -> P1=trai, P2=phai
--
-- ======================= DIEU NAY KHONG ANH HUONG TOI SO =======================
-- Thu tu ZONE da chot tu bang khach gui (file 25) va gan theo CHI SO CONG, nen
-- con so tren moi cot khong phu thuoc vao viec gan huong o day. Neu gan sai
-- huong thi chi cai hinh mui ten sai cho, con so van dung.
--
-- Hai bang dang ngo nhat, neu hien truong bao nguoc thi sua dung mot dong:
--   .66 {len, trai} : dang de P0=trai, P1=len
--   .51 {trai, phai}: cong 1 van TAT vi chua biet no dan toi zone nao
--
-- Ma huong: 0=len(Up) 1=phai(Right) 2=xuong(Down) 3=trai(Left)
-- Mau 2 = xanh. Trang thai 0 = dung yen (khong chay nhap nhay).

USE total_parking;

UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
JOIN ( SELECT '51' AS code, 0 AS port_index, 3 AS dir UNION ALL SELECT '51', 1, 1
       UNION ALL SELECT '53', 0, 3 UNION ALL SELECT '53', 1, 0 UNION ALL SELECT '53', 2, 1
       UNION ALL SELECT '55', 0, 0 UNION ALL SELECT '55', 1, 1
       UNION ALL SELECT '57', 0, 3 UNION ALL SELECT '57', 1, 0 UNION ALL SELECT '57', 2, 1
       UNION ALL SELECT '58', 0, 0 UNION ALL SELECT '58', 1, 1
       UNION ALL SELECT '65', 0, 3 UNION ALL SELECT '65', 1, 0 UNION ALL SELECT '65', 2, 1
       UNION ALL SELECT '66', 0, 3 UNION ALL SELECT '66', 1, 0
) v ON v.code = p.code AND v.port_index = pp.port_index
SET    pp.arrow_direction = v.dir,
       pp.arrow_color     = 2,
       pp.arrow_state     = 0;

-- Cong 51/1 van chua biet dan toi zone nao -> giu TAT, mui ten khong duoc sang.
UPDATE led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
SET    pp.arrow_color = 0
WHERE  p.code = '51' AND pp.port_index = 1;

-- ------------------------------------------------------------------ doi chieu
SELECT p.code AS bang, pp.port_index AS cong,
       CASE pp.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END AS huong,
       CASE pp.arrow_color WHEN 0 THEN 'AN' WHEN 1 THEN 'do'
            WHEN 2 THEN 'xanh' ELSE 'vang' END AS mui_ten,
       pp.zone_list AS zone, pp.is_active AS bat
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
ORDER  BY p.code, pp.port_index;

SELECT COUNT(*) AS cong_con_an_mui_ten
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE  pp.arrow_color = 0 AND p.kind = 'DIRECTIONAL';
