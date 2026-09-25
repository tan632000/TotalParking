-- 45_canh_bao.sql — noi luu canh bao va su co thiet bi.
--
-- ===================== VI SAO KHONG TAI DUNG parking_event =====================
-- Bang do co session_id, from_status, to_status — danh cho vong doi mot phien gui
-- xe. PLC block 21 chet luc 3 gio sang khong thuoc phien cua ai. Nhet vao do thi
-- session_id phai NULL cho moi dong va hai cot status mat nghia.
--
-- ===================== TU VUNG PHAI KHOP BO LOC SAN CO =====================
-- Views/Home/Alarms.cshtml:236-254 loc bang data-type va data-severity voi cac
-- gia tri da ton tai trong markup. Neu bang nay dung tu khac (PLC, CAO...) thi
-- nguoi van hanh chon "Nghiem trong" se thay BANG TRONG giua luc co su co that.
--
--   nguon  : hardware | operation | maintenance
--   muc_do : critical | high | medium | low
--
-- Dung ENUM de CSDL tu chan gia tri la, thay vi phat hien khi giao dien da hong.
--
-- ===================== KHOA CHONG TRUNG =====================
-- khoa_chong_trung co rang buoc UNIQUE, dat gia tri khi canh bao MO va ve NULL
-- khi dong. InnoDB cho phep NHIEU dong NULL trong UNIQUE index, nen mot khoi co
-- the co nhieu canh bao da dong nhung chi mot canh bao dang mo.
--
-- Dung rang buoc CSDL chu khong kiem trong ma: hai tien trinh cung chay thi
-- kiem-roi-ghi van lot. Task 03 dua vao dung co che nay de khong sinh 8.640 dong
-- cho mot su co keo dai ba ngay.
--
-- ===================== KHONG XOA DONG KHI SU CO HET =====================
-- het_luc khac NULL nghia la da qua. Xoa dong la mat lich su, va lich su su co
-- chinh la thu can nhat khi dieu tra ve sau.

SET @db := DATABASE();

SET @co := (SELECT COUNT(*) FROM information_schema.TABLES
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'canh_bao');

SET @sql := IF(@co = 0, '
CREATE TABLE canh_bao (
  canh_bao_id      BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

  nguon            ENUM("hardware","operation","maintenance") NOT NULL
                   COMMENT "Khop data-type cua bo loc trong Alarms.cshtml",
  muc_do           ENUM("critical","high","medium","low") NOT NULL
                   COMMENT "Khop data-severity cua bo loc trong Alarms.cshtml",

  ma_loi           VARCHAR(32)  NOT NULL COMMENT "Ma tra cuu, vi du PLC-CONN-01",
  block_no         SMALLINT UNSIGNED NULL COMMENT "So khoi hien cho nguoi doc, KHONG phai block_id",
  zone_id          SMALLINT UNSIGNED NULL,
  thiet_bi         VARCHAR(64)  NULL,
  mo_ta            VARCHAR(255) NOT NULL,

  xay_ra_luc       DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  het_luc          DATETIME(3)  NULL COMMENT "Khac NULL = su co da qua. Khong xoa dong.",

  xac_nhan_boi     VARCHAR(64)  NULL COMMENT "Nhan nguoi dung TU KHAI — he thong khong co xac thuc",
  xac_nhan_ip      VARCHAR(45)  NULL COMMENT "Dia chi may khach luc bam xac nhan",
  xac_nhan_luc     DATETIME(3)  NULL,

  khoa_chong_trung VARCHAR(64)  NULL
                   COMMENT "Chi dat khi canh bao dang MO. NULL khi dong, de lan sau sinh duoc canh bao moi.",

  PRIMARY KEY (canh_bao_id),
  UNIQUE KEY uq_canh_bao_khoa_chong_trung (khoa_chong_trung),
  KEY ix_canh_bao_chua_xac_nhan (xac_nhan_luc, xay_ra_luc),
  KEY ix_canh_bao_xay_ra (xay_ra_luc),
  KEY ix_canh_bao_block (block_no)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
  COMMENT="Canh bao va su co thiet bi. Khac parking_event: khong gan voi phien gui xe."
', 'SELECT "canh_bao da co"');

PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- Kiem ket qua: phai thay bang, rang buoc UNIQUE, va hai cot ENUM.
SELECT TABLE_NAME, TABLE_COMMENT FROM information_schema.TABLES
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'canh_bao';

SELECT INDEX_NAME, COLUMN_NAME, NON_UNIQUE
  FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'canh_bao'
   AND INDEX_NAME = 'uq_canh_bao_khoa_chong_trung';

SELECT COLUMN_NAME, COLUMN_TYPE FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'canh_bao'
   AND COLUMN_NAME IN ('nguon', 'muc_do') ORDER BY COLUMN_NAME;
