-- Dao thu tu TRAI <-> PHAI cua mui ten tren cac bang LED chi huong.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi UPDATE -> tai khoan ung dung chay duoc.
--
-- ======================= VI SAO =======================
-- File 26 gan huong mui ten theo GIA DINH cua toi, khong phai du lieu khach cho:
-- khach chi noi moi bang co nhung huong nao, khong noi huong nao thuoc cong nao.
-- Toi suy theo quy uoc trong giaothucketnoi.pdf (Hinh 3/4/5): P1 la cot TRAI
-- NHAT roi sang phai, va xep mui ten tu trai sang phai theo chinh huong no chi.
--
-- Khach xem bang that (17/09) va bao NGUOC LAI. Gia dinh do sai.
--
-- ======================= CHI DAO HUONG, KHONG DAO ZONE =======================
-- Danh sach zone theo tung cong thi GIU NGUYEN. Day la du lieu KHACH CUNG CAP,
-- ghi ro "Cong 0, 1, 2 -> ZONE 2 / ZONE 2 / ZONE 1" cho bang 57 va 65 -- khong
-- phai thu toi suy ra. Chi so cong la su that phan cung, con so nao nam o cot
-- nao thi da dung san.
--
-- Cai sai duy nhat la HINH MUI TEN dat nham ben. Nen chi dao cai do.
--
-- NEU sau khi doi ma so tren tung cot lai nam sai cho, thi gia dinh nay cung sai
-- va phai dao ca zone_list -- nhung luc do can khach xac nhan lai bang anh xa
-- cong -> zone, chu khong doan tiep.
--
-- ======================= CACH DAO =======================
-- Dao NGUOC thu tu day huong cua tung bang:
--   53/57/65  trai, len, phai  ->  phai, len, trai
--   51        trai, phai       ->  phai, trai
--   55/58     len,  phai       ->  phai, len
--   66        trai, len        ->  len,  trai
--   52/54/56/67  chi mot huong "len" -> KHONG DOI
--
-- Ma huong: 0=len 1=phai 2=xuong 3=trai

USE total_parking;

UPDATE led_panel_port pp
JOIN   led_panel p ON p.panel_id = pp.panel_id
JOIN ( SELECT '51' AS code, 0 AS port_index, 1 AS dir UNION ALL SELECT '51', 1, 3
       UNION ALL SELECT '53', 0, 1 UNION ALL SELECT '53', 1, 0 UNION ALL SELECT '53', 2, 3
       UNION ALL SELECT '55', 0, 1 UNION ALL SELECT '55', 1, 0
       UNION ALL SELECT '57', 0, 1 UNION ALL SELECT '57', 1, 0 UNION ALL SELECT '57', 2, 3
       UNION ALL SELECT '58', 0, 1 UNION ALL SELECT '58', 1, 0
       UNION ALL SELECT '65', 0, 1 UNION ALL SELECT '65', 1, 0 UNION ALL SELECT '65', 2, 3
       UNION ALL SELECT '66', 0, 0 UNION ALL SELECT '66', 1, 3
) v ON v.code = p.code AND v.port_index = pp.port_index
SET    pp.arrow_direction = v.dir;

-- ------------------------------------------------------------------ doi chieu
SELECT p.code AS bang, pp.port_index AS cong,
       CASE pp.arrow_direction WHEN 0 THEN 'len' WHEN 1 THEN 'phai'
            WHEN 2 THEN 'xuong' ELSE 'trai' END AS huong_moi,
       CASE pp.arrow_color WHEN 0 THEN 'AN' WHEN 1 THEN 'do'
            WHEN 2 THEN 'xanh' ELSE 'vang' END AS mui_ten,
       pp.zone_list AS zone_giu_nguyen, pp.is_active AS bat
FROM   led_panel_port pp JOIN led_panel p ON p.panel_id = pp.panel_id
WHERE  p.kind = 'DIRECTIONAL'
ORDER  BY p.code, pp.port_index;
