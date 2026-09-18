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
-- map_x / map_y nam trong khung viewBox 1200 x 1139 cua Images/plan_map.jpg.
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
        ADD COLUMN map_x SMALLINT NULL COMMENT ''toa do X tren plan_map.jpg (viewBox 1200x1139)'',
        ADD COLUMN map_y SMALLINT NULL COMMENT ''toa do Y tren plan_map.jpg (viewBox 1200x1139)''',
    'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

UPDATE block b
JOIN (
    SELECT 1 AS block_no, 398 AS mx, 31 AS my
    UNION ALL SELECT 2, 450, 31
    UNION ALL SELECT 3, 552, 32
    UNION ALL SELECT 4, 605, 32
    UNION ALL SELECT 5, 340, 101
    UNION ALL SELECT 6, 403, 160
    UNION ALL SELECT 7, 584, 119
    UNION ALL SELECT 8, 639, 120
    UNION ALL SELECT 9, 726, 102
    UNION ALL SELECT 10, 797, 102
    UNION ALL SELECT 11, 831, 101
    UNION ALL SELECT 12, 902, 100
    UNION ALL SELECT 13, 961, 142
    UNION ALL SELECT 14, 902, 152
    UNION ALL SELECT 15, 829, 151
    UNION ALL SELECT 16, 795, 151
    UNION ALL SELECT 17, 724, 152
    UNION ALL SELECT 18, 724, 195
    UNION ALL SELECT 19, 796, 204
    UNION ALL SELECT 20, 797, 255
    UNION ALL SELECT 21, 914, 256
    UNION ALL SELECT 22, 967, 256
    UNION ALL SELECT 23, 1017, 256
    UNION ALL SELECT 24, 1119, 256
    UNION ALL SELECT 25, 1016, 371
    UNION ALL SELECT 26, 966, 367
    UNION ALL SELECT 27, 961, 471
    UNION ALL SELECT 28, 904, 470
    UNION ALL SELECT 29, 863, 360
    UNION ALL SELECT 30, 758, 349
    UNION ALL SELECT 31, 714, 348
    UNION ALL SELECT 32, 708, 257
    UNION ALL SELECT 33, 647, 258
    UNION ALL SELECT 34, 602, 259
    UNION ALL SELECT 35, 559, 260
    UNION ALL SELECT 36, 440, 252
    UNION ALL SELECT 37, 398, 252
    UNION ALL SELECT 38, 342, 253
    UNION ALL SELECT 39, 292, 271
    UNION ALL SELECT 40, 180, 255
    UNION ALL SELECT 41, 134, 255
    UNION ALL SELECT 42, 83, 254
    UNION ALL SELECT 43, 31, 255
    UNION ALL SELECT 44, 83, 348
    UNION ALL SELECT 45, 135, 366
    UNION ALL SELECT 46, 188, 365
    UNION ALL SELECT 47, 239, 365
    UNION ALL SELECT 48, 297, 360
    UNION ALL SELECT 49, 343, 360
    UNION ALL SELECT 50, 394, 360
    UNION ALL SELECT 51, 439, 360
    UNION ALL SELECT 52, 551, 349
    UNION ALL SELECT 53, 602, 348
    UNION ALL SELECT 54, 647, 348
    UNION ALL SELECT 55, 550, 402
    UNION ALL SELECT 56, 602, 403
    UNION ALL SELECT 57, 647, 402
    UNION ALL SELECT 58, 716, 401
    UNION ALL SELECT 59, 757, 401
    UNION ALL SELECT 60, 630, 507
    UNION ALL SELECT 61, 758, 507
    UNION ALL SELECT 62, 748, 559
    UNION ALL SELECT 63, 862, 608
    UNION ALL SELECT 64, 966, 596
    UNION ALL SELECT 65, 1017, 596
    UNION ALL SELECT 66, 1018, 685
    UNION ALL SELECT 67, 965, 702
    UNION ALL SELECT 68, 913, 685
    UNION ALL SELECT 69, 862, 686
    UNION ALL SELECT 70, 758, 608
    UNION ALL SELECT 71, 706, 608
    UNION ALL SELECT 72, 654, 610
    UNION ALL SELECT 73, 602, 607
    UNION ALL SELECT 74, 551, 608
    UNION ALL SELECT 75, 499, 608
    UNION ALL SELECT 76, 395, 592
    UNION ALL SELECT 77, 394, 557
    UNION ALL SELECT 78, 345, 507
    UNION ALL SELECT 79, 301, 550
    UNION ALL SELECT 80, 500, 686
    UNION ALL SELECT 81, 549, 686
    UNION ALL SELECT 82, 655, 687
    UNION ALL SELECT 83, 704, 686
    UNION ALL SELECT 84, 809, 686
    UNION ALL SELECT 85, 811, 721
    UNION ALL SELECT 86, 868, 790
    UNION ALL SELECT 87, 698, 806
    UNION ALL SELECT 88, 646, 800
    UNION ALL SELECT 89, 601, 800
    UNION ALL SELECT 90, 558, 799
    UNION ALL SELECT 91, 549, 718
    UNION ALL SELECT 92, 655, 719
    UNION ALL SELECT 93, 704, 719
    UNION ALL SELECT 94, 713, 859
    UNION ALL SELECT 95, 710, 906
    UNION ALL SELECT 96, 869, 824
    UNION ALL SELECT 97, 818, 914
    UNION ALL SELECT 98, 862, 916
    UNION ALL SELECT 99, 912, 917
    UNION ALL SELECT 100, 964, 916
    UNION ALL SELECT 101, 1013, 916
    UNION ALL SELECT 102, 1057, 914
    UNION ALL SELECT 103, 902, 825
    UNION ALL SELECT 104, 860, 1009
    UNION ALL SELECT 105, 907, 1007
    UNION ALL SELECT 106, 957, 1010
    UNION ALL SELECT 107, 1066, 1008
    UNION ALL SELECT 108, 1119, 1008
    UNION ALL SELECT 109, 1169, 1009
    UNION ALL SELECT 110, 1014, 1107
    UNION ALL SELECT 111, 964, 1108
    UNION ALL SELECT 112, 913, 1108
) t ON t.block_no = b.block_no
SET    b.map_x = t.mx, b.map_y = t.my;

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS block_co_toa_do_map FROM block WHERE map_x IS NOT NULL;

SELECT zone_id,
       COUNT(*)        AS so_block,
       MIN(map_x) AS x_min, MAX(map_x) AS x_max,
       MIN(map_y) AS y_min, MAX(map_y) AS y_max
FROM   block WHERE map_x IS NOT NULL GROUP BY zone_id ORDER BY zone_id;
