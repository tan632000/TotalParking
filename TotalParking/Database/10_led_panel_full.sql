-- Bo sung du 12 bang LED theo docs/LUMI_IP_Range_CL1.xlsx (ban da dien du IP).
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- Chay SAU 09_led_panel.sql.

USE total_parking;

-- ---------------------------------------------------- HAI CACH DANH SO BANG
-- Ngoai hien truong ton tai song song hai cach goi mot tam bang, va chung
-- KHONG trung nhau:
--
--   code  = octet cuoi cua IP        -> '56'  (dung tren so do led_position.jpg)
--   name  = ten trong bang IP        -> 'Bang led 6'
--
-- Day khong phai lech ngau nhien: day IP nhay tu .58 sang .65, nen
--   Bang led 1..8  -> .51 .52 .53 .54 .55 .56 .57 .58
--   Bang led 9..11 -> .65 .66 .67
-- Tuc '.65' la "Bang led 9", KHONG phai "bang 65".
--
-- Giu ca hai thay vi ep ve mot: ben thi cong noi "bang led 9", so do ky thuat
-- ghi "65". Bo mot trong hai la moi lan trao doi lai phai quy doi bang dau.
--
-- code van la khoa doi ngoai (dung trong /LedStatus/Send?panel=...) vi no gan
-- chat voi IP nen khong bao gio mo ho.
SET @has_name := (SELECT COUNT(*) FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'led_panel' AND COLUMN_NAME = 'name');
SET @sql := IF(@has_name = 0,
    'ALTER TABLE led_panel ADD COLUMN name VARCHAR(64) NULL AFTER code',
    'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

-- ------------------------------------------------------------------- seed
-- 12 bang, tat ca deu da xac nhan mo TCP 2022 ngay 2026-09-14.
--
-- is_active:
--   .50 BAT  — bang dau ham, hien TONG so cho trong, khong can biet huong.
--   con lai TAT — bang chi huong, chua biet mui ten nao dan toi zone nao.
--                 Bat len ma khong biet huong thi bang chi bua, te hon la
--                 khong chi gi.
INSERT INTO led_panel (code, name, ip_address, port, kind, is_active, note) VALUES
    ('50', 'Bang led dau ham', '192.169.1.50', 2022, 'ENTRANCE',    1, 'Hien tong so cho trong toan bai'),
    ('51', 'Bang led 1',       '192.169.1.51', 2022, 'DIRECTIONAL', 0, NULL),
    ('52', 'Bang led 2',       '192.169.1.52', 2022, 'DIRECTIONAL', 0, NULL),
    ('53', 'Bang led 3',       '192.169.1.53', 2022, 'DIRECTIONAL', 0, NULL),
    ('54', 'Bang led 4',       '192.169.1.54', 2022, 'DIRECTIONAL', 0, NULL),
    ('55', 'Bang led 5',       '192.169.1.55', 2022, 'DIRECTIONAL', 0, NULL),
    ('56', 'Bang led 6',       '192.169.1.56', 2022, 'DIRECTIONAL', 0, '1 huong, ranh Zone 3/4'),
    ('57', 'Bang led 7',       '192.169.1.57', 2022, 'DIRECTIONAL', 0, '3 huong, Zone 2'),
    ('58', 'Bang led 8',       '192.169.1.58', 2022, 'DIRECTIONAL', 0, NULL),
    ('65', 'Bang led 9',       '192.169.1.65', 2022, 'DIRECTIONAL', 0, '3 huong, Zone 6/2'),
    ('66', 'Bang led 10',      '192.169.1.66', 2022, 'DIRECTIONAL', 0, NULL),
    ('67', 'Bang led 11',      '192.169.1.67', 2022, 'DIRECTIONAL', 0, NULL) AS new
ON DUPLICATE KEY UPDATE
    name = new.name, ip_address = new.ip_address, port = new.port,
    kind = new.kind, note = COALESCE(new.note, led_panel.note);

-- ------------------------------------------------------- cong hien thi
-- CO Y khong tao dong cong cho 8 bang chua biet so huong.
--
-- So huong chi biet duoc voi 3 bang co chu thich tren docs/led_position.jpg
-- (.56 = 1 huong, .57 = 3 huong, .65 = 3 huong) — 09_led_panel.sql da tao roi.
--
-- Doan so huong cua 8 bang con lai roi tao san dong la tao ra du lieu trong
-- veo nhin nhu da khao sat. Bang nao chua co dong cong thi /LedStatus hien
-- "ports: []" — nhin la biet ngay con thieu khao sat, khong phai doan xem
-- con so kia that hay bia.
