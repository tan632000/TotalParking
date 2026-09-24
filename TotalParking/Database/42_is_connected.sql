-- 42_is_connected.sql — ba cot phan chieu trang thai ket noi PLC vao plc_device.
--
-- VI SAO GHI VAO BANG CAU HINH
-- Nguoi dung chot ngay 24/09: muon truy van duoc bang SQL cung cho voi cau hinh.
-- Nhuoc diem da duoc neu (ghi lien tuc vao bang cau hinh, va cot dong bang noi
-- doi khi site chet) va nguoi dung giu quyet dinh, nen ba lop giam nhe duoc cai
-- kem: chi ghi khi doi, co khu rung, va co last_probe_at.
--
-- BA COT, MOI COT MOT CAU HOI
--   is_connected          lan quan sat gan nhat, PLC nay song hay chet
--   connected_changed_at  trang thai do giu nguyen tu bao gio
--   last_probe_at         lan quan sat gan nhat la luc nao
--
-- Cot thu ba la bat buoc chu khong phai trang tri. connected_changed_at KHONG
-- cho biet so lieu cu bao lau: no la thoi diem trang thai DOI, nen mot PLC online
-- on dinh ba ngay va mot site da chet nam phut cho ra du lieu giong het nhau.
-- Chi last_probe_at phan biet duoc.
--
-- is_connected de NULL khi thiet bi khong nam trong vong poll: khong biet khac
-- han voi biet la chet.
--
-- Guarded + idempotent: MySQL 8 khong co ADD COLUMN IF NOT EXISTS (do la cu phap
-- MariaDB), nen phai dem trong information_schema roi PREPARE.

SET @db := DATABASE();

-- ------------------------------------------------------------- is_connected
SET @co := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'plc_device'
               AND COLUMN_NAME = 'is_connected');
SET @sql := IF(@co = 0,
  'ALTER TABLE plc_device ADD COLUMN is_connected TINYINT(1) NULL DEFAULT NULL
     COMMENT "Lan quan sat gan nhat PLC song (1) hay chet (0). NULL = khong nam trong vong poll."',
  'SELECT "is_connected da co"');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ----------------------------------------------------- connected_changed_at
SET @co := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'plc_device'
               AND COLUMN_NAME = 'connected_changed_at');
SET @sql := IF(@co = 0,
  'ALTER TABLE plc_device ADD COLUMN connected_changed_at DATETIME(3) NULL DEFAULT NULL
     COMMENT "Thoi diem is_connected doi gia tri lan gan nhat."',
  'SELECT "connected_changed_at da co"');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ------------------------------------------------------------ last_probe_at
SET @co := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'plc_device'
               AND COLUMN_NAME = 'last_probe_at');
SET @sql := IF(@co = 0,
  'ALTER TABLE plc_device ADD COLUMN last_probe_at DATETIME(3) NULL DEFAULT NULL
     COMMENT "Lan gan nhat trang thai duoc quan sat. Cu nghia la so lieu dang dong bang."',
  'SELECT "last_probe_at da co"');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- Kiem ket qua: phai ra du ba dong.
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'plc_device'
   AND COLUMN_NAME IN ('is_connected', 'connected_changed_at', 'last_probe_at')
 ORDER BY COLUMN_NAME;
