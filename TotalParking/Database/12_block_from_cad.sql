-- Bang nang luc block, trich tu ban ve CAD vector.
-- Nguon: docs/IP_led_guide.pdf  (LM-CL1-BAS-NTC-CP-SHD-0003-01.1)
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ========================== DO TIN CAY ==========================
--
-- DOC TRUC TIEP TU NHAN TREN BAN VE (tin cay cao):
--   * so o + chieu dai khoang tung block   nhan "BLOCK n SPACES-xxxxL", 111 nhan
--   * cho do nen                           nhan "1 LOTS", 80 nhan
--
-- LAY THEO BANG THONG KE CUA BAN VE (112 bo / 764 o):
--   Mat bang chi co 111 nhan = 761 o. Bang thong ke in tren chinh ban ve ghi
--   112 bo / 764 o va liet ke 1 x "BLOCK 5 SPACES-4800L" — nhan nay KHONG
--   xuat hien o dau tren mat bang.
--   Da THEM MOT block 3 o de tong khop dung 112 bo / 764 o theo bang thong ke.
--   Block them nay:
--     - dat o zone 2 vi zone do dang co nhieu block 3-o nhat (4 bo). LA SUY DOAN.
--     - ghi 5000L chu khong phai 4800L: khong biet block 4800L nam o dau, ma
--       ghi bua mot block thanh 4800 se lam block 4800 THAT van mang nhan 5000.
--   => bo dem "L < 4.8 M" tren bang LED se co total = 0 va bi AN (mau den),
--      thay vi hien so 0 do — xem LedFrameBuilder.ColorFor.
--   CAN HOI DON VI THIET KE: block 5 SPACES-4800L nam o dau?
--
-- SUY RA, CHUA KIEM CHUNG (do chinh xac ~3%):
--   * zone cua tung block va tung cho do nen. Ban ve KHONG VE ranh gioi zone,
--     chi co 6 nhan chu ZONE 1..6 (da kiem tra ca 139 lop cua file). Zone duoc
--     gan bang phep bien doi affine tu 6 nhan do sang he toa do
--     Images/zones_map.jpeg, roi kiem tra diem trong polygon.
--     Sai so 2-34 px tren khung 1016 px. 13 block + 3 cho do nen roi ngoai moi
--     polygon va duoc gan zone gan nhat.
--
-- CHI LA SO THU TU TAM:
--   * block_no. Ghep nhan voi so block in tren ban ve khong on dinh giua cac
--     lan chay nen khong dung duoc. PHAI SUA LAI THEO BAN VE TRUOC KHI DAU PLC,
--     vi plc_device sinh IP tu block_no theo cong thuc N+100.
--
-- CHUA CO:
--   * tier_count / column_count. So tang cua tung model chua xac nhan, de NULL.
--     He qua: luat 2600KG (chi tang duoi) CHUA CHAY DUOC, total_tier0 = 0.
--     Khong doan, vi doan sai o day la dua xe nang len pallet tang tren.
-- ================================================================

USE total_parking;

DELETE FROM parking_slot;
DELETE FROM block;

-- --- block co khi: 112 block, 764 o ---
INSERT INTO block (zone_id, block_no, kind, slot_count, bay_length_mm, origin_x, origin_y) VALUES
    (1, 1, 'Mechanical', 5, 5000, 580, 624),
    (1, 2, 'Mechanical', 5, 5000, 607, 620),
    (1, 3, 'Mechanical', 5, 5000, 635, 616),
    (1, 4, 'Mechanical', 6, 5000, 595, 592),
    (1, 5, 'Mechanical', 10, 5000, 654, 585),
    (1, 6, 'Mechanical', 10, 5000, 681, 580),
    (1, 7, 'Mechanical', 10, 5000, 709, 576),
    (1, 8, 'Mechanical', 10, 5000, 600, 514),
    (1, 9, 'Mechanical', 10, 5000, 628, 510),
    (1, 10, 'Mechanical', 10, 5000, 655, 505),
    (1, 11, 'Mechanical', 6, 5000, 678, 502),
    (2, 12, 'Mechanical', 10, 5000, -54, 846),
    (2, 13, 'Mechanical', 10, 5000, -39, 835),
    (2, 14, 'Mechanical', 10, 5000, -37, 835),
    (2, 15, 'Mechanical', 10, 5000, -43, 831),
    (2, 16, 'Mechanical', 10, 5000, -43, 830),
    (2, 17, 'Mechanical', 10, 5000, 544, 601),
    (2, 18, 'Mechanical', 10, 5000, 572, 597),
    (2, 19, 'Mechanical', 5, 5000, 491, 549),
    (2, 20, 'Mechanical', 5, 5000, 496, 522),
    (2, 21, 'Mechanical', 6, 5000, 549, 522),
    (2, 22, 'Mechanical', 10, 5000, 573, 518),
    (2, 23, 'Mechanical', 3, 5000, 567, 496),
    (2, 24, 'Mechanical', 10, 5000, 502, 495),
    (2, 25, 'Mechanical', 3, 5000, 422, 487),
    (2, 26, 'Mechanical', 3, 5000, 468, 484),
    (2, 27, 'Mechanical', 10, 5000, 446, 483),
    (2, 28, 'Mechanical', 5, 5000, 409, 466),
    (2, 29, 'Mechanical', 3, 5000, 587, 462),
    (2, 30, 'Mechanical', 5, 5000, 464, 457),
    (2, 31, 'Mechanical', 5, 5000, 492, 453),
    (2, 32, 'Mechanical', 5, 5000, 546, 445),
    (2, 33, 'Mechanical', 5, 5000, 428, 432),
    (2, 34, 'Mechanical', 5, 5000, 483, 424),
    (2, 35, 'Mechanical', 5, 5000, 510, 419),
    (2, 36, 'Mechanical', 5, 5000, 339, 412),
    (2, 37, 'Mechanical', 5, 5000, 392, 412),
    (2, 38, 'Mechanical', 5, 5000, 565, 411),
    (2, 39, 'Mechanical', 5, 5000, 420, 407),
    (2, 40, 'Mechanical', 10, 5000, 593, 406),
    (2, 41, 'Mechanical', 5, 5000, 447, 403),
    (2, 42, 'Mechanical', 5, 5000, 475, 399),
    (2, 43, 'Mechanical', 5, 5000, 307, 395),
    (2, 44, 'Mechanical', 5, 5000, 502, 395),
    (2, 45, 'Mechanical', 5, 5000, 530, 390),
    (2, 46, 'Mechanical', 3, 5000, NULL, NULL) /* THEM de khop bang thong ke, vi tri chua biet */,
    (3, 47, 'Mechanical', 5, 5000, 213, 293),
    (3, 48, 'Mechanical', 10, 5000, 241, 287),
    (3, 49, 'Mechanical', 10, 5000, 269, 283),
    (3, 50, 'Mechanical', 10, 5000, 177, 275),
    (3, 51, 'Mechanical', 10, 5000, 204, 271),
    (3, 52, 'Mechanical', 10, 5000, 231, 267),
    (3, 53, 'Mechanical', 6, 5000, 255, 262),
    (4, 54, 'Mechanical', 6, 5000, 336, 382),
    (4, 55, 'Mechanical', 3, 5000, 320, 372),
    (4, 56, 'Mechanical', 6, 5000, 488, 325),
    (4, 57, 'Mechanical', 10, 5000, 437, 310),
    (4, 58, 'Mechanical', 10, 5000, 464, 305),
    (4, 59, 'Mechanical', 6, 5000, 488, 301),
    (4, 60, 'Mechanical', 6, 5000, 523, 295),
    (4, 61, 'Mechanical', 10, 5000, 296, 279),
    (4, 62, 'Mechanical', 6, 5000, 328, 275),
    (4, 63, 'Mechanical', 10, 5000, 351, 270),
    (4, 64, 'Mechanical', 10, 5000, 378, 266),
    (4, 65, 'Mechanical', 6, 5000, 402, 263),
    (4, 66, 'Mechanical', 5, 5000, 460, 256),
    (4, 67, 'Mechanical', 5, 5000, 315, 252),
    (4, 68, 'Mechanical', 5, 5000, 487, 252),
    (4, 69, 'Mechanical', 6, 5000, 341, 249),
    (4, 70, 'Mechanical', 3, 5000, 511, 249),
    (4, 71, 'Mechanical', 6, 5000, 370, 244),
    (4, 72, 'Mechanical', 6, 5000, 392, 241),
    (4, 73, 'Mechanical', 6, 5000, 455, 233),
    (4, 74, 'Mechanical', 10, 5000, 478, 230),
    (4, 75, 'Mechanical', 6, 5000, 502, 225),
    (4, 76, 'Mechanical', 6, 5000, 401, 162),
    (4, 77, 'Mechanical', 10, 5000, 378, 156),
    (4, 78, 'Mechanical', 6, 5000, 500, 126),
    (4, 79, 'Mechanical', 5, 5000, 392, 119),
    (4, 80, 'Mechanical', 5, 5000, 420, 115),
    (4, 81, 'Mechanical', 5, 5000, 474, 106),
    (4, 82, 'Mechanical', 5, 5000, 502, 102),
    (5, 83, 'Mechanical', 10, 5000, 670, 199),
    (5, 84, 'Mechanical', 10, 5000, 698, 195),
    (5, 85, 'Mechanical', 3, 5000, 564, 176),
    (5, 86, 'Mechanical', 10, 5000, 746, 165),
    (5, 87, 'Mechanical', 5, 5000, 568, 152),
    (5, 88, 'Mechanical', 5, 5000, 624, 144),
    (5, 89, 'Mechanical', 6, 5000, 679, 140),
    (5, 90, 'Mechanical', 5, 5000, 595, 136),
    (5, 91, 'Mechanical', 5, 5000, 651, 127),
    (5, 92, 'Mechanical', 5, 5000, 573, 125),
    (5, 93, 'Mechanical', 6, 5000, 529, 121),
    (5, 94, 'Mechanical', 5, 5000, 629, 117),
    (5, 95, 'Mechanical', 5, 5000, 600, 109),
    (5, 96, 'Mechanical', 5, 5000, 656, 100),
    (6, 97, 'Mechanical', 5, 5000, 620, 402),
    (6, 98, 'Mechanical', 10, 5000, 648, 397),
    (6, 99, 'Mechanical', 5, 5000, 675, 394),
    (6, 100, 'Mechanical', 5, 5000, 584, 384),
    (6, 101, 'Mechanical', 10, 5000, 638, 376),
    (6, 102, 'Mechanical', 10, 5000, 666, 372),
    (6, 103, 'Mechanical', 6, 5000, 550, 363),
    (6, 104, 'Mechanical', 10, 5000, 557, 313),
    (6, 105, 'Mechanical', 10, 5000, 547, 292),
    (6, 106, 'Mechanical', 6, 5000, 638, 283),
    (6, 107, 'Mechanical', 6, 5000, 667, 278),
    (6, 108, 'Mechanical', 3, 5000, 546, 243),
    (6, 109, 'Mechanical', 5, 5000, 569, 240),
    (6, 110, 'Mechanical', 10, 5000, 602, 240),
    (6, 111, 'Mechanical', 10, 5000, 533, 220),
    (6, 112, 'Mechanical', 10, 5000, 643, 203) AS new
ON DUPLICATE KEY UPDATE
    zone_id = new.zone_id, kind = new.kind, slot_count = new.slot_count,
    bay_length_mm = new.bay_length_mm, origin_x = new.origin_x, origin_y = new.origin_y;

-- --- cho do nen: gop theo zone ---
-- Gop thay vi tach tung cho vi cho do nen khong co PLC va khong co cam bien
-- rieng trong he nay; SCADA chi can biet moi zone con bao nhieu cho.
INSERT INTO block (zone_id, block_no, kind, slot_count) VALUES
    (1, 901, 'Ground', 13),
    (2, 902, 'Ground', 18),
    (3, 903, 'Ground', 2),
    (4, 904, 'Ground', 17),
    (5, 905, 'Ground', 11),
    (6, 906, 'Ground', 19) AS new
ON DUPLICATE KEY UPDATE
    zone_id = new.zone_id, kind = new.kind, slot_count = new.slot_count;

-- Kiem tra: co khi phai ra 112 block / 764 o, do nen 6 cum / 80 cho.
SELECT kind, COUNT(*) AS so_block, SUM(slot_count) AS tong_o FROM block GROUP BY kind;
SELECT zone_id,
       SUM(CASE WHEN kind='Mechanical' THEN slot_count ELSE 0 END) AS co_khi,
       SUM(CASE WHEN kind='Ground'     THEN slot_count ELSE 0 END) AS do_nen
FROM   block GROUP BY zone_id ORDER BY zone_id;
--
-- Ky vong:
--   zone 1: co khi  87 o, do nen 13 cho
--   zone 2: co khi 221 o, do nen 18 cho
--   zone 3: co khi  61 o, do nen  2 cho
--   zone 4: co khi 189 o, do nen 17 cho
--   zone 5: co khi  85 o, do nen 11 cho
--   zone 6: co khi 121 o, do nen 19 cho
--   TONG  : co khi 764 o (112 block), do nen 80 cho
