-- Mang luoi lan duong chay xe (lane_node / lane_edge) va nut den cua tung block.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO CAN BANG NAY =======================
-- Trang Dieu huong xe dang ve duong di bang mot doan thang tu cong den block.
-- Duong thang do cat qua cac block dang do xe: tren man hinh cho tai xe, mot
-- net nhu vay la lenh "lai xuyen qua cho do". Muon ve duong di that thi phai co
-- mang luoi lan duong so hoa, roi tim duong ngan nhat tren do.
--
-- ======================= HE TOA DO =======================
-- node.x / node.y nam trong DUNG khung 1594 x 1300 cua Images/plan_map.jpg,
-- tuc la cung he voi block.map_x / block.map_y do 34_block_map_xy.sql ghi.
-- Khong co scale, offset hay floor_plan_id rieng. Doi anh nen thi PHAI sinh lai
-- ca bang nay lan block.map_x/map_y.
--
-- ======================= HINH HOC LAY TU DAU =======================
-- Lan duong la KHOANG TRONG, khong phai net ve, nen khong the loc tu content
-- stream cua ban ve (trang 1 co hon 1 trieu doan thang chi tiet co khi). Da
-- dung duong lui da du trong thiet ke: do lai tim lan tren chinh plan_map.jpg.
--
--   1. Mask vat can = vung mau bao quanh moi block (to mau) sau khi dong vien
--      va to day ruot  -> 112/112 cham block deu nam trong mask.
--   2. Them tuong bao: giu lai cac net xam dam DAI (bounding box >= 300 px),
--      chinh la ranh gioi cheo goc duoi-trai va tuong tren. Bo qua net ngan
--      (kich thuoc, mui ten, chu) de khong lam vun hanh lang.
--   3. Hanh lang = vung cach vat can >= 10 px va nam trong 70 px quanh cum
--      block. Lay thanh phan lien thong lon nhat.
--   4. Skeleton hanh lang -> tim tim lan. Cat thanh doan <= 45 px de duong ve
--      bam theo lan cong.
--   5. Nut den cua block = nut gan cham block nhat (xa nhat 71 px, dung bang
--      nua chieu sau mot block).
--
-- Da kiem: 313 nut, 341 canh, DUNG 1 thanh phan lien thong, 0 canh cat qua
-- vat can, 0/112 block khong toi duoc tu nut cong.
--
-- ======================= NUT CONG VAO =======================
-- D3: dau doc len xuong la MOT nut du lieu co is_entry = 1, khong phai hang so
-- trong code. BlockMapRepository.GateX/GateY van giu nguyen cho marker cong
-- tren trang van hanh; chung khong con quyet dinh diem xuat phat cua route.

USE total_parking;

-- ------------------------------------------------------------------ lane_node
-- CHECK bat buoc moi nut nam trong khung. MySQL 8.0.16+ thuc thi CHECK that,
-- nen mot nut lech khung se lam INSERT that bai ngay, khong am tham luu vao.
-- entry_uk la cot sinh: chi co gia tri 1 khi is_entry = 1, con lai la NULL.
-- UNIQUE tren no => KHONG THE co hai nut cung danh dau cong vao.
CREATE TABLE IF NOT EXISTS lane_node (
    node_id  SMALLINT UNSIGNED NOT NULL,
    x        SMALLINT NOT NULL COMMENT 'toa do X tren plan_map.jpg (khung 1594x1300)',
    y        SMALLINT NOT NULL COMMENT 'toa do Y tren plan_map.jpg (khung 1594x1300)',
    is_entry TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1 = dau doc, diem xuat phat route',
    entry_uk TINYINT(1) AS (IF(is_entry = 1, 1, NULL)) STORED,
    PRIMARY KEY (node_id),
    UNIQUE KEY uk_lane_node_entry (entry_uk),
    CONSTRAINT ck_lane_node_frame CHECK (x >= 0 AND x <= 1594 AND y >= 0 AND y <= 1300)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
  COMMENT='Nut cua mang luoi lan duong, cung he toa do voi block.map_x/map_y';

-- ------------------------------------------------------------------ lane_edge
-- Canh la VO HUONG va chi luu MOT lan cho moi cap khong thu tu (from < to).
-- Route service tu mo ra hai chieu khi doc. Chi phi canh KHONG luu: tinh tu
-- toa do hai dau, de doi cho mot nut khong de lai chi phi cu sai.
CREATE TABLE IF NOT EXISTS lane_edge (
    from_node SMALLINT UNSIGNED NOT NULL,
    to_node   SMALLINT UNSIGNED NOT NULL,
    PRIMARY KEY (from_node, to_node),
    KEY ix_lane_edge_to (to_node),
    CONSTRAINT fk_lane_edge_from FOREIGN KEY (from_node) REFERENCES lane_node (node_id),
    CONSTRAINT fk_lane_edge_to   FOREIGN KEY (to_node)   REFERENCES lane_node (node_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
  COMMENT='Canh vo huong noi hai nut lan duong';

-- --------------------------------------------------------- block.lane_node_id
-- MySQL khong co ADD COLUMN IF NOT EXISTS -> kiem tra information_schema roi
-- moi ALTER, giong het cach 34_block_map_xy.sql lam, de file chay lai duoc.
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'block'
               AND COLUMN_NAME = 'lane_node_id');
SET @sql := IF(@has = 0,
    'ALTER TABLE block
        ADD COLUMN lane_node_id SMALLINT UNSIGNED NULL
            COMMENT ''nut lan duong xe dung lai khi den block nay'',
        ADD CONSTRAINT fk_block_lane_node
            FOREIGN KEY (lane_node_id) REFERENCES lane_node (node_id)',
    'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- --------------------------------------------- dong bo lai rang buoc khung
-- CREATE TABLE IF NOT EXISTS chi tao rang buoc o LAN DAU. Khi khung toa do doi
-- (cat lai anh nen), bang da ton tai nen CHECK cu nam lai voi so cu va khong ai
-- biet. Vi vay o day luon go rang buoc cu roi dat lai theo so hien tai.
--
-- Dat TRUOC cac lenh INSERT, de chinh cac dong sap chen cung duoc rang buoc dung
-- soi. Noi rong khung thi du lieu cu van hop le; thu hep khung thi ALTER nay
-- that bai ngay, va do chinh la dieu ta muon.
SET @has := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
             WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'lane_node'
               AND CONSTRAINT_NAME = 'ck_lane_node_frame');
SET @sql := IF(@has = 1, 'ALTER TABLE lane_node DROP CHECK ck_lane_node_frame', 'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

ALTER TABLE lane_node ADD CONSTRAINT ck_lane_node_frame
    CHECK (x >= 0 AND x <= 1594 AND y >= 0 AND y <= 1300);

-- ------------------------------------------------------------------ nut
-- ON DUPLICATE KEY UPDATE: chay lan hai ghi de dung gia tri cu, khong bao loi
-- trung khoa va khong doi so dong.
INSERT INTO lane_node (node_id, x, y, is_entry) VALUES
    (1,646,71,0),(2,645,95,0),(3,645,119,0),(4,905,75,0),(5,877,85,0),(6,846,84,0),
    (7,819,79,0),(8,943,75,0),(9,977,77,0),(10,1012,75,0),(11,1050,75,0),(12,1086,75,0),
    (13,1105,97,0),(14,906,120,0),(15,906,164,0),(16,906,208,0),(17,904,251,0),(18,897,292,0),
    (19,899,335,0),(20,1013,115,0),(21,1013,154,0),(22,1013,192,0),(23,1018,229,0),(24,790,95,0),
    (25,756,97,0),(26,826,116,0),(27,826,156,0),(28,825,195,0),(29,805,218,0),(30,709,97,0),
    (31,674,120,0),(32,755,138,0),(33,755,178,0),(34,756,217,0),(35,1080,126,0),(36,1079,163,0),
    (37,1078,200,0),(38,1148,98,0),(39,1164,126,0),(40,1164,167,0),(41,1164,209,0),(42,530,101,0),
    (43,562,118,0),(44,602,120,0),(45,603,164,0),(46,603,207,0),(47,674,165,0),(48,672,209,0),
    (49,409,146,0),(50,408,178,0),(51,420,205,0),(52,457,200,0),(53,492,208,0),(54,518,236,0),
    (55,399,226,0),(56,580,230,0),(57,551,237,0),(58,646,213,0),(59,712,218,0),(60,1131,229,0),
    (61,1094,234,0),(62,1054,234,0),(63,1201,210,0),(64,1227,221,0),(65,645,246,0),(66,645,280,0),
    (67,645,314,0),(68,780,218,0),(69,819,256,0),(70,819,299,0),(71,825,340,0),(72,1208,257,0),
    (73,1209,300,0),(74,1217,339,0),(75,1271,222,0),(76,1313,226,0),(77,1325,264,0),(78,1325,307,0),
    (79,1312,345,0),(80,1271,349,0),(81,1228,349,0),(82,364,209,0),(83,324,209,0),(84,283,209,0),
    (85,242,209,0),(86,202,209,0),(87,165,217,0),(88,144,249,0),(89,144,290,0),(90,146,329,0),
    (91,180,343,0),(92,378,252,0),(93,378,285,0),(94,378,319,0),(95,1018,270,0),(96,516,269,0),
    (97,516,303,0),(98,516,336,0),(99,1017,296,0),(100,1018,321,0),(101,1022,342,0),(102,637,336,0),
    (103,673,339,0),(104,711,340,0),(105,749,340,0),(106,786,340,0),(107,343,337,0),(108,302,337,0),
    (109,261,337,0),(110,220,337,0),(111,395,335,0),(112,418,337,0),(113,864,340,0),(114,928,341,0),
    (115,951,355,0),(116,452,337,0),(117,483,337,0),(118,557,337,0),(119,596,337,0),(120,419,373,0),
    (121,419,408,0),(122,419,444,0),(123,419,479,0),(124,637,381,0),(125,637,424,0),(126,633,465,0),
    (127,1178,340,0),(128,1140,340,0),(129,1102,340,0),(130,1065,344,0),(131,826,382,0),(132,826,421,0),
    (133,826,461,0),(134,827,500,0),(135,987,348,0),(136,189,364,0),(137,196,384,0),(138,1067,375,0),
    (139,1067,407,0),(140,1063,437,0),(141,1227,386,0),(142,1227,423,0),(143,1227,459,0),(144,1199,470,0),
    (145,1163,470,0),(146,952,396,0),(147,954,436,0),(148,981,465,0),(149,213,400,0),(150,244,418,0),
    (151,269,441,0),(152,306,452,0),(153,342,464,0),(154,381,466,0),(155,414,486,0),(156,1032,438,0),
    (157,1004,444,0),(158,1079,448,0),(159,1110,447,0),(160,1140,450,0),(161,1079,486,0),(162,1079,524,0),
    (163,1078,561,0),(164,600,466,0),(165,569,466,0),(166,540,473,0),(167,982,483,0),(168,634,504,0),
    (169,1162,509,0),(170,1162,548,0),(171,499,473,0),(172,458,473,0),(173,547,501,0),(174,547,531,0),
    (175,942,484,0),(176,903,484,0),(177,865,486,0),(178,837,511,0),(179,982,527,0),(180,982,570,0),
    (181,400,518,0),(182,408,555,0),(183,421,590,0),(184,792,485,0),(185,751,485,0),(186,720,507,0),
    (187,616,532,0),(188,677,507,0),(189,720,544,0),(190,720,580,0),(191,825,547,0),(192,825,587,0),
    (193,516,562,0),(194,582,532,0),(195,617,557,0),(196,620,580,0),(197,1123,562,0),(198,1201,549,0),
    (199,1227,562,0),(200,1227,600,0),(201,1227,638,0),(202,1227,676,0),(203,1045,580,0),(204,963,589,0),
    (205,956,611,0),(206,1012,580,0),(207,1060,605,0),(208,1060,635,0),(209,1060,666,0),(210,601,608,0),
    (211,603,643,0),(212,603,676,0),(213,654,581,0),(214,686,581,0),(215,751,597,0),(216,790,597,0),
    (217,852,611,0),(218,956,640,0),(219,956,667,0),(220,1026,677,0),(221,989,677,0),(222,1099,677,0),
    (223,1141,677,0),(224,1184,677,0),(225,930,676,0),(226,905,683,0),(227,640,676,0),(228,677,675,0),
    (229,714,675,0),(230,748,683,0),(231,602,706,0),(232,602,736,0),(233,612,763,0),(234,1228,714,0),
    (235,1228,752,0),(236,1207,770,0),(237,1175,777,0),(238,1167,810,0),(239,1140,837,0),(240,787,675,0),
    (241,827,675,0),(242,866,675,0),(243,749,729,0),(244,749,774,0),(245,904,718,0),(246,905,752,0),
    (247,904,786,0),(248,708,784,0),(249,667,789,0),(250,785,785,0),(251,824,789,0),(252,864,789,0),
    (253,625,776,0),(254,645,782,0),(255,1059,782,0),(256,1020,784,0),(257,983,787,0),(258,955,812,0),
    (259,1081,802,0),(260,1113,812,0),(261,923,812,0),(262,679,824,0),(263,903,841,0),(264,903,879,0),
    (265,903,916,0),(266,903,953,0),(267,899,989,0),(268,966,842,0),(269,967,876,0),(270,1110,866,0),
    (271,1080,891,0),(272,1041,892,0),(273,1001,892,0),(274,1167,865,0),(275,1203,870,0),(276,1241,870,0),
    (277,1264,887,0),(278,1264,925,0),(279,1264,963,0),(280,942,901,0),(281,1310,963,0),(282,1288,964,0),
    (283,1232,991,0),(284,1190,992,0),(285,1157,1016,0),(286,841,972,0),(287,870,980,0),(288,904,1011,0),
    (289,914,1030,0),(290,959,1006,0),(291,996,991,0),(292,1038,991,0),(293,1081,991,0),(294,1123,991,0),
    (295,960,1049,0),(296,975,1085,0),(297,1012,1098,0),(298,1156,1049,0),(299,1156,1082,0),(300,1123,1096,0),
    (301,1086,1096,0),(302,1049,1096,0),(303,1193,1096,0),(304,1228,1114,0),(305,1019,1119,0),(306,1026,1139,0),
    (307,1310,1113,0),(308,1270,1114,0),(309,1214,1151,0),(310,1178,1167,0),(311,1136,1167,0),(312,1094,1167,0),
    (313,1052,1165,1)
ON DUPLICATE KEY UPDATE x = VALUES(x), y = VALUES(y), is_entry = VALUES(is_entry);

-- ------------------------------------------------------------------ canh
INSERT INTO lane_edge (from_node, to_node) VALUES
    (1,2),(2,3),(3,31),(3,44),(4,5),(4,8),(4,14),(5,6),
    (6,7),(7,24),(7,26),(8,9),(9,10),(10,11),(10,20),(11,12),
    (12,13),(13,35),(13,38),(14,15),(15,16),(16,17),(17,18),(18,19),
    (19,113),(19,114),(20,21),(21,22),(22,23),(23,62),(23,95),(24,25),
    (25,30),(25,32),(26,27),(27,28),(28,29),(29,68),(29,69),(30,31),
    (31,47),(32,33),(33,34),(34,59),(34,68),(35,36),(36,37),(38,39),
    (39,40),(40,41),(41,60),(41,63),(42,43),(43,44),(44,45),(45,46),
    (46,56),(46,58),(47,48),(48,58),(48,59),(49,50),(50,51),(51,52),
    (51,55),(52,53),(53,54),(54,57),(54,96),(55,82),(55,92),(56,57),
    (58,65),(60,61),(61,62),(63,64),(64,72),(64,75),(65,66),(66,67),
    (67,102),(67,103),(69,70),(70,71),(71,106),(71,113),(71,131),(72,73),
    (73,74),(74,81),(74,127),(75,76),(76,77),(77,78),(78,79),(79,80),
    (80,81),(81,141),(82,83),(83,84),(84,85),(85,86),(86,87),(87,88),
    (88,89),(89,90),(90,91),(91,110),(91,136),(92,93),(93,94),(94,107),
    (94,111),(95,99),(96,97),(97,98),(98,117),(98,118),(99,100),(100,101),
    (101,130),(101,135),(102,119),(102,124),(103,104),(104,105),(105,106),(107,108),
    (108,109),(109,110),(111,112),(112,116),(112,120),(114,115),(115,135),(115,146),
    (116,117),(118,119),(120,121),(121,122),(122,123),(123,155),(123,172),(124,125),
    (125,126),(126,164),(126,168),(127,128),(128,129),(129,130),(130,138),(131,132),
    (132,133),(133,134),(134,178),(134,184),(136,137),(138,139),(139,140),(140,156),
    (140,158),(141,142),(142,143),(143,144),(144,145),(145,160),(145,169),(146,147),
    (147,148),(148,157),(148,167),(149,150),(150,151),(151,152),(152,153),(153,154),
    (154,155),(155,181),(156,157),(158,159),(158,161),(159,160),(161,162),(162,163),
    (163,197),(163,203),(164,165),(165,166),(166,171),(166,173),(167,175),(167,179),
    (168,187),(168,188),(169,170),(170,197),(170,198),(171,172),(173,174),(174,193),
    (174,194),(175,176),(176,177),(177,178),(178,191),(179,180),(180,204),(180,206),
    (181,182),(182,183),(184,185),(185,186),(186,188),(186,189),(187,194),(187,195),
    (189,190),(190,214),(190,215),(191,192),(192,216),(192,217),(195,196),(196,210),
    (196,213),(198,199),(199,200),(200,201),(201,202),(202,224),(202,234),(203,206),
    (203,207),(204,205),(205,218),(207,208),(208,209),(209,220),(209,222),(210,211),
    (211,212),(212,227),(212,231),(213,214),(215,216),(218,219),(219,221),(219,225),
    (220,221),(222,223),(223,224),(225,226),(226,242),(226,245),(227,228),(228,229),
    (229,230),(230,240),(230,243),(231,232),(232,233),(234,235),(235,236),(236,237),
    (237,238),(238,239),(239,260),(239,270),(239,274),(240,241),(241,242),(243,244),
    (244,248),(244,250),(245,246),(246,247),(247,252),(247,261),(248,249),(249,254),
    (249,262),(250,251),(251,252),(253,254),(255,256),(255,259),(256,257),(257,258),
    (258,261),(258,268),(259,260),(261,263),(263,264),(264,265),(265,266),(266,267),
    (267,287),(267,288),(268,269),(269,273),(269,280),(270,271),(271,272),(272,273),
    (274,275),(275,276),(276,277),(277,278),(278,279),(279,282),(279,283),(281,282),
    (283,284),(284,285),(285,294),(285,298),(286,287),(288,289),(290,291),(290,295),
    (291,292),(292,293),(293,294),(295,296),(296,297),(297,302),(297,305),(298,299),
    (299,300),(299,303),(300,301),(301,302),(303,304),(304,308),(304,309),(305,306),
    (307,308),(309,310),(310,311),(311,312),(312,313)
ON DUPLICATE KEY UPDATE to_node = VALUES(to_node);

-- ------------------------------------------------------- nut den cua block
UPDATE block b
JOIN (
    SELECT 1 AS block_no, 42 AS nid
    UNION ALL SELECT 2, 1
    UNION ALL SELECT 3, 30
    UNION ALL SELECT 4, 25
    UNION ALL SELECT 5, 42
    UNION ALL SELECT 6, 57
    UNION ALL SELECT 7, 32
    UNION ALL SELECT 8, 32
    UNION ALL SELECT 9, 14
    UNION ALL SELECT 10, 14
    UNION ALL SELECT 11, 20
    UNION ALL SELECT 12, 20
    UNION ALL SELECT 13, 36
    UNION ALL SELECT 14, 22
    UNION ALL SELECT 15, 22
    UNION ALL SELECT 16, 15
    UNION ALL SELECT 17, 28
    UNION ALL SELECT 18, 17
    UNION ALL SELECT 19, 17
    UNION ALL SELECT 20, 18
    UNION ALL SELECT 21, 99
    UNION ALL SELECT 22, 129
    UNION ALL SELECT 23, 73
    UNION ALL SELECT 24, 73
    UNION ALL SELECT 25, 160
    UNION ALL SELECT 26, 139
    UNION ALL SELECT 27, 161
    UNION ALL SELECT 28, 161
    UNION ALL SELECT 29, 135
    UNION ALL SELECT 30, 19
    UNION ALL SELECT 31, 131
    UNION ALL SELECT 32, 70
    UNION ALL SELECT 33, 70
    UNION ALL SELECT 34, 105
    UNION ALL SELECT 35, 104
    UNION ALL SELECT 36, 119
    UNION ALL SELECT 37, 96
    UNION ALL SELECT 38, 96
    UNION ALL SELECT 39, 112
    UNION ALL SELECT 40, 108
    UNION ALL SELECT 41, 109
    UNION ALL SELECT 42, 110
    UNION ALL SELECT 43, 89
    UNION ALL SELECT 44, 149
    UNION ALL SELECT 45, 150
    UNION ALL SELECT 46, 152
    UNION ALL SELECT 47, 121
    UNION ALL SELECT 48, 121
    UNION ALL SELECT 49, 117
    UNION ALL SELECT 50, 118
    UNION ALL SELECT 51, 124
    UNION ALL SELECT 52, 104
    UNION ALL SELECT 53, 105
    UNION ALL SELECT 54, 131
    UNION ALL SELECT 55, 125
    UNION ALL SELECT 56, 185
    UNION ALL SELECT 57, 132
    UNION ALL SELECT 58, 132
    UNION ALL SELECT 59, 176
    UNION ALL SELECT 60, 189
    UNION ALL SELECT 61, 176
    UNION ALL SELECT 62, 217
    UNION ALL SELECT 63, 221
    UNION ALL SELECT 64, 208
    UNION ALL SELECT 65, 223
    UNION ALL SELECT 66, 223
    UNION ALL SELECT 67, 222
    UNION ALL SELECT 68, 220
    UNION ALL SELECT 69, 221
    UNION ALL SELECT 70, 226
    UNION ALL SELECT 71, 217
    UNION ALL SELECT 72, 240
    UNION ALL SELECT 73, 230
    UNION ALL SELECT 74, 228
    UNION ALL SELECT 75, 227
    UNION ALL SELECT 76, 210
    UNION ALL SELECT 77, 193
    UNION ALL SELECT 78, 193
    UNION ALL SELECT 79, 183
    UNION ALL SELECT 80, 231
    UNION ALL SELECT 81, 228
    UNION ALL SELECT 82, 240
    UNION ALL SELECT 83, 241
    UNION ALL SELECT 84, 245
    UNION ALL SELECT 85, 257
    UNION ALL SELECT 86, 256
    UNION ALL SELECT 87, 251
    UNION ALL SELECT 88, 250
    UNION ALL SELECT 89, 244
    UNION ALL SELECT 90, 262
    UNION ALL SELECT 91, 248
    UNION ALL SELECT 92, 250
    UNION ALL SELECT 93, 252
    UNION ALL SELECT 94, 264
    UNION ALL SELECT 95, 286
    UNION ALL SELECT 96, 273
    UNION ALL SELECT 97, 280
    UNION ALL SELECT 98, 291
    UNION ALL SELECT 99, 292
    UNION ALL SELECT 100, 294
    UNION ALL SELECT 101, 294
    UNION ALL SELECT 102, 284
    UNION ALL SELECT 103, 272
    UNION ALL SELECT 104, 295
    UNION ALL SELECT 105, 292
    UNION ALL SELECT 106, 301
    UNION ALL SELECT 107, 284
    UNION ALL SELECT 108, 283
    UNION ALL SELECT 109, 307
    UNION ALL SELECT 110, 311
    UNION ALL SELECT 111, 312
    UNION ALL SELECT 112, 306) t ON t.block_no = b.block_no
SET b.lane_node_id = t.nid;

-- Nut cong vao KHONG duoc dong thoi la nut den cua mot block. Neu no kiem hai
-- vai, duong di toi block do chi con dung mot diem, va man hinh tai xe theo hop
-- dong C1 phai coi day la "khong co duong" roi bo ve. Block 112 nam ngay dau doc
-- nen dung vao loi nay: nut den cua no duoc doi sang nut ke tiep tren lan.
SET @cong_kiem := (SELECT COUNT(*) FROM block b JOIN lane_node n ON n.node_id = b.lane_node_id
                   WHERE n.is_entry = 1);
SET @sql := IF(@cong_kiem = 0, 'DO 0',
    'SELECT * FROM LOI_nut_cong_vao_dang_kiem_luon_nut_den_cua_block');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ------------------------------------------------------------------ chan loi
-- Ba dieu kien duoi day phai dung, neu khong migration coi nhu that bai.
-- MySQL khong cho SIGNAL trong PREPARE, nen cach dung lai chac chan nhat la
-- co tinh doc mot bang khong ton tai, ten bang chinh la thong bao loi.
SET @so_cong := (SELECT COUNT(*) FROM lane_node WHERE is_entry = 1);
SET @sql := IF(@so_cong = 1, 'DO 0',
    'SELECT * FROM LOI_lane_node_phai_co_dung_mot_nut_is_entry');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

SET @thieu := (SELECT COUNT(*) FROM block WHERE map_x IS NOT NULL AND lane_node_id IS NULL);
SET @sql := IF(@thieu = 0, 'DO 0',
    'SELECT * FROM LOI_con_block_co_map_x_ma_chua_co_lane_node_id');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

SET @lech := (SELECT COUNT(*) FROM lane_node
              WHERE x < 0 OR x > 1594 OR y < 0 OR y > 1300);
SET @sql := IF(@lech = 0, 'DO 0',
    'SELECT * FROM LOI_co_lane_node_nam_ngoai_khung_1594x1300');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS so_nut FROM lane_node;
SELECT COUNT(*) AS so_canh FROM lane_edge;
SELECT COUNT(*) AS so_block_co_nut_den FROM block WHERE lane_node_id IS NOT NULL;
