-- Phan bo block theo zone, lay tu docs/tong_hop_zone_blocks.md do KHACH HANG cung cap.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO FILE NAY THAY THE 12_block_from_cad.sql =======================
--
-- 12_block_from_cad.sql gan zone bang SUY DOAN: ban ve CAD khong he ve ranh gioi
-- zone (da kiem ca 139 lop, chi co 6 NHAN CHU "ZONE 1".."ZONE 6"), nen zone duoc
-- suy ra bang phep bien doi affine tu 6 nhan do sang he toa do zones_map.jpeg roi
-- kiem tra diem trong polygon. Do chinh xac cua cach do da do lai:
--
--   * phep bien doi sai trung binh 23 px, lon nhat 34 px tren chinh 6 diem dung
--     de dung no (khung 1016 px)
--   * 53/111 block (48%) nam sat hoac ngoai ranh gioi -> co the gan nham zone
--   * block_no chi la so thu tu TAM, KHONG phai so in tren ban ve
--
-- File nay bo toan bo cach do. Zone va so block lay truc tiep tu bang khach gui.
--
-- DOI CHIEU DA LAM (99/106 khop):
--   Ghep tu dong nhan so tren ban ve voi nhan "BLOCK n SPACES-xxxxL" cho ket qua
--   trung khop file khach o 99 block. 7 block lech deu co dang DAO CHO hai nhan
--   canh nhau (vd block 87/88: file ghi 3,10 - ghep tu dong ra 10,3), tuc la loi
--   cua phep ghep tham lam, khong phai loi cua file khach.
--
-- CON LECH VOI BAN VE, CAN KHACH XAC NHAN:
--   * Tong o: file khach 755, bang thong ke in tren ban ve ghi "SUC CHUA: 764 XE".
--     Lech 9 o.
--   * Bang tom tat cuoi file khach ghi "10 spaces = 40 block" nhung danh sach chi
--     tiet chi co 39. Neu la 40 thi tong block thanh 113, mau thuan voi dong
--     TONG = 112. Da lay theo DANH SACH CHI TIET.
--
-- CHUA CO (giu nguyen nhu truoc):
--   * bay_length_mm: file khach khong ghi chieu dai khoang. Ban ve co dung 1 block
--     "5 SPACES-4800L" nhung khong biet la block so may -> de tat ca 5000.
--     He qua: bo dem "L < 4.8 M" tren bang LED co total = 0 va bi AN (mau den).
--   * tier_count / column_count: so tang tung model chua xac nhan -> NULL.
--     Luat 2600KG (chi tang duoi) van CHUA CHAY DUOC.
--   * zone cua 6 cum do nen (901-906): VAN LA SUY DOAN, file khach chi noi ve
--     block co khi. Giu nguyen gia tri cu.
--
-- origin_x / origin_y: suy tu vi tri nhan so that tren ban ve qua phep bien doi
-- affine. Chi dung de ve, khong dung de gan zone nua nen sai so 23 px chap nhan duoc.
-- ==============================================================================

USE total_parking;

-- Thu tu xoa theo khoa ngoai: plc_request -> parking_session -> parking_slot
-- -> plc_device -> block. plc_device se duoc nap lai o 15_plc_from_block_no.sql.
DELETE FROM plc_request;
DELETE FROM parking_session;
DELETE FROM parking_slot;
DELETE FROM plc_device;
DELETE FROM block;

INSERT INTO block (zone_id, block_no, kind, slot_count, bay_length_mm, origin_x, origin_y) VALUES
    (4, 1, 'Mechanical', 5, 5000, 399, 113),
    (4, 2, 'Mechanical', 5, 5000, 426, 108),
    (4, 3, 'Mechanical', 5, 5000, 480, 101),
    (4, 4, 'Mechanical', 5, 5000, 508, 96),
    (4, 5, 'Mechanical', 5, 5000, 361, 154),
    (4, 6, 'Mechanical', 10, 5000, 389, 179),
    (4, 7, 'Mechanical', 6, 5000, 489, 144),
    (4, 8, 'Mechanical', 6, 5000, 517, 139),
    (5, 9, 'Mechanical', 5, 5000, 565, 123),
    (5, 10, 'Mechanical', 5, 5000, 603, 117),
    (5, 11, 'Mechanical', 5, 5000, 621, 114),
    (5, 12, 'Mechanical', 5, 5000, 658, 108),
    (5, 13, 'Mechanical', 6, 5000, 686, 125),
    (5, 14, 'Mechanical', 5, 5000, 654, 134),
    (5, 15, 'Mechanical', 5, 5000, 615, 140),
    (5, 16, 'Mechanical', 5, 5000, 597, 143),
    (5, 17, 'Mechanical', 5, 5000, 560, 149),
    (5, 18, 'Mechanical', 3, 5000, 555, 172),
    (5, 19, 'Mechanical', 10, 5000, 592, 171),
    (5, 20, 'Mechanical', 10, 5000, 588, 197),
    (5, 21, 'Mechanical', 10, 5000, 650, 188),
    (5, 22, 'Mechanical', 10, 5000, 678, 184),
    (5, 23, 'Mechanical', 10, 5000, 704, 180),
    (5, 24, 'Mechanical', 10, 5000, 759, 171),
    (6, 25, 'Mechanical', 10, 5000, 693, 240),
    (6, 26, 'Mechanical', 10, 5000, 667, 242),
    (6, 27, 'Mechanical', 6, 5000, 655, 296),
    (6, 28, 'Mechanical', 6, 5000, 624, 301),
    (6, 29, 'Mechanical', 5, 5000, 613, 246),
    (6, 30, 'Mechanical', 5, 5000, 559, 249),
    (6, 31, 'Mechanical', 3, 5000, 535, 252),
    (6, 32, 'Mechanical', 10, 5000, 541, 205),
    (4, 33, 'Mechanical', 10, 5000, 509, 211),
    (4, 34, 'Mechanical', 10, 5000, 485, 215),
    (4, 35, 'Mechanical', 6, 5000, 462, 219),
    (4, 36, 'Mechanical', 6, 5000, 400, 225),
    (4, 37, 'Mechanical', 6, 5000, 377, 228),
    (4, 38, 'Mechanical', 6, 5000, 348, 233),
    (3, 39, 'Mechanical', 5, 5000, 320, 247),
    (3, 40, 'Mechanical', 6, 5000, 262, 248),
    (3, 41, 'Mechanical', 10, 5000, 238, 251),
    (3, 42, 'Mechanical', 10, 5000, 211, 255),
    (3, 43, 'Mechanical', 10, 5000, 183, 260),
    (3, 44, 'Mechanical', 5, 5000, 202, 304),
    (3, 45, 'Mechanical', 10, 5000, 228, 310),
    (3, 46, 'Mechanical', 10, 5000, 256, 305),
    (3, 47, 'Mechanical', 10, 5000, 283, 300),
    (3, 48, 'Mechanical', 6, 5000, 314, 293),
    (3, 49, 'Mechanical', 10, 5000, 338, 289),
    (3, 50, 'Mechanical', 10, 5000, 365, 285),
    (4, 51, 'Mechanical', 6, 5000, 389, 281),
    (4, 52, 'Mechanical', 5, 5000, 449, 266),
    (4, 53, 'Mechanical', 5, 5000, 476, 262),
    (4, 54, 'Mechanical', 3, 5000, 500, 258),
    (4, 55, 'Mechanical', 10, 5000, 444, 294),
    (4, 56, 'Mechanical', 10, 5000, 471, 290),
    (4, 57, 'Mechanical', 6, 5000, 495, 286),
    (4, 58, 'Mechanical', 6, 5000, 532, 280),
    (4, 59, 'Mechanical', 10, 5000, 554, 277),
    (2, 60, 'Mechanical', 6, 5000, 476, 342),
    (2, 61, 'Mechanical', 10, 5000, 544, 332),
    (2, 62, 'Mechanical', 6, 5000, 533, 360),
    (2, 63, 'Mechanical', 5, 5000, 589, 377),
    (2, 64, 'Mechanical', 10, 5000, 645, 362),
    (2, 65, 'Mechanical', 10, 5000, 673, 358),
    (2, 66, 'Mechanical', 5, 5000, 664, 404),
    (2, 67, 'Mechanical', 10, 5000, 635, 417),
    (2, 68, 'Mechanical', 5, 5000, 609, 413),
    (2, 69, 'Mechanical', 10, 5000, 582, 417),
    (2, 70, 'Mechanical', 5, 5000, 534, 385),
    (2, 71, 'Mechanical', 5, 5000, 507, 389),
    (2, 72, 'Mechanical', 5, 5000, 479, 394),
    (2, 73, 'Mechanical', 5, 5000, 452, 397),
    (2, 74, 'Mechanical', 5, 5000, 425, 402),
    (2, 75, 'Mechanical', 5, 5000, 397, 406),
    (2, 76, 'Mechanical', 5, 5000, 344, 406),
    (2, 77, 'Mechanical', 6, 5000, 347, 388),
    (3, 78, 'Mechanical', 3, 5000, 325, 366),
    (3, 79, 'Mechanical', 5, 5000, 298, 392),
    (2, 80, 'Mechanical', 10, 5000, 390, 447),
    (2, 81, 'Mechanical', 5, 5000, 416, 443),
    (2, 82, 'Mechanical', 5, 5000, 472, 438),
    (2, 83, 'Mechanical', 5, 5000, 498, 430),
    (2, 84, 'Mechanical', 5, 5000, 554, 421),
    (2, 85, 'Mechanical', 5, 5000, 552, 439),
    (2, 86, 'Mechanical', 3, 5000, 575, 471),
    (2, 87, 'Mechanical', 3, 5000, 484, 493),
    (2, 88, 'Mechanical', 10, 5000, 457, 495),
    (2, 89, 'Mechanical', 3, 5000, 433, 498),
    (2, 90, 'Mechanical', 3, 5000, 410, 501),
    (2, 91, 'Mechanical', 5, 5000, 413, 460),
    (2, 92, 'Mechanical', 5, 5000, 469, 451),
    (2, 93, 'Mechanical', 5, 5000, 495, 447),
    (2, 94, 'Mechanical', 5, 5000, 487, 520),
    (2, 95, 'Mechanical', 5, 5000, 481, 545),
    (2, 96, 'Mechanical', 3, 5000, 572, 489),
    (1, 97, 'Mechanical', 6, 5000, 537, 540),
    (1, 98, 'Mechanical', 10, 5000, 560, 537),
    (1, 99, 'Mechanical', 10, 5000, 586, 534),
    (1, 100, 'Mechanical', 10, 5000, 614, 529),
    (1, 101, 'Mechanical', 10, 5000, 640, 525),
    (1, 102, 'Mechanical', 6, 5000, 663, 521),
    (1, 103, 'Mechanical', 3, 5000, 590, 486),
    (1, 104, 'Mechanical', 10, 5000, 550, 586),
    (1, 105, 'Mechanical', 10, 5000, 575, 581),
    (1, 106, 'Mechanical', 6, 5000, 601, 579),
    (1, 107, 'Mechanical', 10, 5000, 660, 569),
    (1, 108, 'Mechanical', 10, 5000, 688, 564),
    (1, 109, 'Mechanical', 10, 5000, 714, 561),
    (1, 110, 'Mechanical', 5, 5000, 622, 625),
    (1, 111, 'Mechanical', 5, 5000, 596, 630),
    (1, 112, 'Mechanical', 5, 5000, 569, 633),
    (1, 901, 'Ground', 13, NULL, NULL, NULL),
    (2, 902, 'Ground', 18, NULL, NULL, NULL),
    (3, 903, 'Ground',  2, NULL, NULL, NULL),
    (4, 904, 'Ground', 17, NULL, NULL, NULL),
    (5, 905, 'Ground', 11, NULL, NULL, NULL),
    (6, 906, 'Ground', 19, NULL, NULL, NULL);

-- ------------------------------------------------------------------ doi chieu
SELECT kind, COUNT(*) AS so_block, SUM(slot_count) AS so_o
FROM   block GROUP BY kind;

SELECT z.zone_id, z.name,
       SUM(b.kind = 'Mechanical') AS block_co_khi,
       SUM(IF(b.kind = 'Mechanical', b.slot_count, 0)) AS o_co_khi,
       SUM(IF(b.kind = 'Ground',     b.slot_count, 0)) AS o_do_nen
FROM   zone z LEFT JOIN block b ON b.zone_id = z.zone_id AND b.is_active = 1
GROUP  BY z.zone_id, z.name ORDER BY z.zone_id;

-- Kiem tra so block co day du 1..112 khong
SELECT COUNT(*) AS so_block_co_khi,
       MIN(block_no) AS nho_nhat, MAX(block_no) AS lon_nhat,
       COUNT(DISTINCT block_no) AS gia_tri_duy_nhat
FROM   block WHERE kind = 'Mechanical';
