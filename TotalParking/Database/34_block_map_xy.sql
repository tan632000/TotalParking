-- Toa do CHINH XAC cua 112 block tren ban do, de ve so do dieu huong.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi ALTER + UPDATE.
--
-- ======================= VI SAO CAN BANG NAY =======================
-- origin_x / origin_y la toa do trong he cua BAN VE CAD. Muon cham len ban do
-- thi phai doi sang he toa do cua BUC ANH, va do la cho da that bai BON LAN:
--
--   khop trong tam 6 da giac zone          -> 68%, lech he thong
--   khop hop bao vung mau trong anh        -> ty le x/y lech 15.6%, bat kha thi
--   do khoi mau roi khop hop bao           -> 79%
--   can theo tung zone                     -> block dung zone nhung lech khoi khoi
--
-- Nguyen nhan chung: anh zones_map.jpeg duoc tao DOC LAP voi ban ve, khong co
-- diem neo chung nao. Moi phep khop deu la doan.
--
-- ======================= CACH GIAI =======================
-- Tu RENDER ban ve vector ra anh (Images/plan_map.jpg, tu trang 1 cua
-- LM-CL1-BAS-NTC-CP-SHD-0001-02). Khi chinh minh ve buc anh thi phep bien doi la
-- do minh dat ra:
--
--     diem_pdf = toa_do_raw * 0.06          (CTM o dau content stream)
--     pixel    = diem_pdf * 2.0             (he so phong khi render)
--     pixel_y  = (cao_trang - diem_pdf_y)   (PDF y len, anh y xuong)
--
-- Sai so bang khong theo dinh nghia. Da kiem chung bang mat: cham 112 vong tron
-- len anh vua render, tat ca deu trung khit nhan so block.
--
-- Cot "SO LUONG (BO)" cua bang model o goc duoi-trai cung dung co chu 150.3
-- va cung cho ra so trong 1..112 (9 / 40 / 22 / 40), nen loc theo co chu la
-- CHUA DU. Da lo ra khi thay block 40, 9, 22 co toa do o goc bang thay vi
-- tren mat bang. Bo them vung x 9500..9700, y 4400..5500 moi sach.
--
-- ======================= HE TOA DO =======================
-- map_x / map_y nam trong khung viewBox 1594 x 1300 cua Images/plan_map.jpg.
--
-- TOA DO LA TAM HINH HOC CUA BLOCK, KHONG PHAI CHO BAN VE IN SO.
-- Ban ve dat vong tron so lech ve mep trai cua block; lay dung cho do lam toa do
-- thi voi block rong (19, 20) cham roi han sang mot ben, nhin nhu no thuoc block
-- ben canh. Nay toa do lay tu HOP BAO cac hinh to mau cua block trong content
-- stream trang 1 (bon mau: 0.84/0.90/0.52, 0.95/0.13/0.05, 0.64/0.93/0.49,
-- 0.04/0.96/0.93), roi lay tam hop bao. Lech so voi cach cu: trung vi 5 theo x,
-- -3 theo y; lon nhat 22 (block 19 va 20).
--
-- KHUNG DA DOI MOT LAN: ban cat cu 1200 x 1139 om sat hop bao 112 block, le dung
-- 31 px ca bon phia, nen tren man hinh no cat mat rim toa nha va hai nhan zone.
-- Anh duoc cat lai rong hon tu CUNG mot ban render (trang 1, zoom 2.0), chi doi
-- cua so cat: (620,230) 2731x2592  ->  (297,155) 3628x2959. Ti le khong doi, nen
-- phep doi toa do la TINH TIEN THUAN TUY: x += 142, y += 33. Ap dung y het cho
-- lane_node trong 37_lane_network.sql va cho GateX/GateY trong BlockMapRepository.
-- Doi anh nen thi PHAI sinh lai bang nay, neu khong cham se lech.

USE total_parking;

-- Cot rieng, KHONG ghi de origin_x/origin_y: hai he toa do khac nhau va deu con
-- dung. origin_* dung de tinh khoang cach va gate_rank; map_* dung de ve.
-- MySQL khong co ADD COLUMN IF NOT EXISTS -> kiem tra roi moi them, de file nay
-- chay lai duoc nhieu lan ma khong loi.
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'block'
               AND COLUMN_NAME = 'map_x');
SET @sql := IF(@has = 0,
    'ALTER TABLE block
        ADD COLUMN map_x SMALLINT NULL COMMENT ''toa do X tren plan_map.jpg (viewBox 1594x1300)'',
        ADD COLUMN map_y SMALLINT NULL COMMENT ''toa do Y tren plan_map.jpg (viewBox 1594x1300)''',
    'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

UPDATE block b
JOIN (
    SELECT 1 AS block_no, 542 AS mx, 63 AS my
    UNION ALL SELECT 2, 594, 63
    UNION ALL SELECT 3, 697, 63
    UNION ALL SELECT 4, 749, 63
    UNION ALL SELECT 5, 484, 130
    UNION ALL SELECT 6, 548, 191
    UNION ALL SELECT 7, 729, 150
    UNION ALL SELECT 8, 783, 150
    UNION ALL SELECT 9, 872, 130
    UNION ALL SELECT 10, 942, 130
    UNION ALL SELECT 11, 979, 130
    UNION ALL SELECT 12, 1048, 130
    UNION ALL SELECT 13, 1109, 174
    UNION ALL SELECT 14, 1048, 182
    UNION ALL SELECT 15, 979, 182
    UNION ALL SELECT 16, 942, 182
    UNION ALL SELECT 17, 872, 182
    UNION ALL SELECT 18, 872, 226
    UNION ALL SELECT 19, 960, 234
    UNION ALL SELECT 20, 960, 286
    UNION ALL SELECT 21, 1061, 286
    UNION ALL SELECT 22, 1112, 286
    UNION ALL SELECT 23, 1164, 286
    UNION ALL SELECT 24, 1266, 286
    UNION ALL SELECT 25, 1164, 395
    UNION ALL SELECT 26, 1112, 395
    UNION ALL SELECT 27, 1107, 501
    UNION ALL SELECT 28, 1052, 501
    UNION ALL SELECT 29, 1011, 389
    UNION ALL SELECT 30, 905, 378
    UNION ALL SELECT 31, 860, 378
    UNION ALL SELECT 32, 853, 285
    UNION ALL SELECT 33, 794, 288
    UNION ALL SELECT 34, 749, 288
    UNION ALL SELECT 35, 706, 288
    UNION ALL SELECT 36, 586, 284
    UNION ALL SELECT 37, 544, 284
    UNION ALL SELECT 38, 489, 284
    UNION ALL SELECT 39, 438, 301
    UNION ALL SELECT 40, 327, 285
    UNION ALL SELECT 41, 282, 285
    UNION ALL SELECT 42, 230, 285
    UNION ALL SELECT 43, 179, 285
    UNION ALL SELECT 44, 231, 373
    UNION ALL SELECT 45, 282, 391
    UNION ALL SELECT 46, 334, 391
    UNION ALL SELECT 47, 386, 391
    UNION ALL SELECT 48, 446, 391
    UNION ALL SELECT 49, 490, 391
    UNION ALL SELECT 50, 542, 391
    UNION ALL SELECT 51, 586, 391
    UNION ALL SELECT 52, 697, 377
    UNION ALL SELECT 53, 749, 377
    UNION ALL SELECT 54, 794, 378
    UNION ALL SELECT 55, 697, 432
    UNION ALL SELECT 56, 749, 432
    UNION ALL SELECT 57, 794, 432
    UNION ALL SELECT 58, 861, 432
    UNION ALL SELECT 59, 905, 432
    UNION ALL SELECT 60, 776, 539
    UNION ALL SELECT 61, 905, 538
    UNION ALL SELECT 62, 894, 589
    UNION ALL SELECT 63, 1009, 642
    UNION ALL SELECT 64, 1112, 624
    UNION ALL SELECT 65, 1164, 624
    UNION ALL SELECT 66, 1164, 713
    UNION ALL SELECT 67, 1112, 731
    UNION ALL SELECT 68, 1060, 713
    UNION ALL SELECT 69, 1009, 731
    UNION ALL SELECT 70, 905, 639
    UNION ALL SELECT 71, 853, 639
    UNION ALL SELECT 72, 801, 639
    UNION ALL SELECT 73, 749, 639
    UNION ALL SELECT 74, 697, 639
    UNION ALL SELECT 75, 646, 639
    UNION ALL SELECT 76, 542, 624
    UNION ALL SELECT 77, 542, 588
    UNION ALL SELECT 78, 492, 539
    UNION ALL SELECT 79, 449, 580
    UNION ALL SELECT 80, 645, 731
    UNION ALL SELECT 81, 697, 713
    UNION ALL SELECT 82, 801, 713
    UNION ALL SELECT 83, 853, 713
    UNION ALL SELECT 84, 957, 714
    UNION ALL SELECT 85, 957, 751
    UNION ALL SELECT 86, 1016, 821
    UNION ALL SELECT 87, 845, 835
    UNION ALL SELECT 88, 793, 829
    UNION ALL SELECT 89, 749, 838
    UNION ALL SELECT 90, 706, 821
    UNION ALL SELECT 91, 697, 750
    UNION ALL SELECT 92, 801, 750
    UNION ALL SELECT 93, 853, 750
    UNION ALL SELECT 94, 863, 887
    UNION ALL SELECT 95, 863, 939
    UNION ALL SELECT 96, 1016, 857
    UNION ALL SELECT 97, 964, 946
    UNION ALL SELECT 98, 1009, 946
    UNION ALL SELECT 99, 1060, 946
    UNION ALL SELECT 100, 1112, 946
    UNION ALL SELECT 101, 1164, 946
    UNION ALL SELECT 102, 1209, 946
    UNION ALL SELECT 103, 1052, 857
    UNION ALL SELECT 104, 1009, 1038
    UNION ALL SELECT 105, 1061, 1038
    UNION ALL SELECT 106, 1105, 1038
    UNION ALL SELECT 107, 1216, 1039
    UNION ALL SELECT 108, 1268, 1039
    UNION ALL SELECT 109, 1320, 1039
    UNION ALL SELECT 110, 1164, 1137
    UNION ALL SELECT 111, 1112, 1137
    UNION ALL SELECT 112, 1060, 1137
) t ON t.block_no = b.block_no
SET    b.map_x = t.mx, b.map_y = t.my;

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS block_co_toa_do_map FROM block WHERE map_x IS NOT NULL;

SELECT zone_id,
       COUNT(*)        AS so_block,
       MIN(map_x) AS x_min, MAX(map_x) AS x_max,
       MIN(map_y) AS y_min, MAX(map_y) AS y_max
FROM   block WHERE map_x IS NOT NULL GROUP BY zone_id ORDER BY zone_id;
