-- Them cau hinh thanh ghi cho luot quet the: D106 doc ma the, D1004 tra bang tai trong.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= YEU CAU =======================
-- Khi quet the RFID, PLC ghi ma the vao D106. SCADA doc ma do, tra trong bang
-- the de biet xe thuoc hang tai nao, roi ghi mot con so 1..3 xuong D1004:
--
--     1  duoi 2200 kg
--     2  tu 2200 den 2600 kg
--     3  tren 2600 kg
--     0  khong co the o D106, hoac khong phan loai duoc
--
-- ======================= VI SAO KHONG HARD-CODE =======================
-- Cung ly do voi card_word / find_card_word: ladder tung block co the khac nhau,
-- va doi dia chi thanh ghi khong nen phai build lai ung dung. 13_plc_real_range.sql
-- da ghi lai mot lan khach doi bo thanh ghi giua chung.
--
-- ======================= VI SAO DDL CO GUARD =======================
-- MySQL 8 KHONG ho tro 'ALTER TABLE ... ADD COLUMN IF NOT EXISTS' (cu phap
-- MariaDB). Chay lan hai se bao ERROR 1060 va dung ca script. Dem truoc trong
-- information_schema roi sinh cau lenh la cach duy nhat lam duoc bang SQL thuan.
--
-- Guard dem ca 3 cot va chi chay khi thieu ca 3, vi day la MOT lenh ALTER duy
-- nhat: DDL trong MySQL 8 la nguyen tu, khong the dung lai o giua.

USE total_parking;

SET @missing = (
    SELECT 3 - COUNT(*)
    FROM   information_schema.columns
    WHERE  table_schema = DATABASE()
      AND  table_name   = 'plc_device'
      AND  column_name IN ('scan_card_word', 'scan_card_len', 'weight_band_word')
);

SET @ddl = IF(@missing = 3,
    'ALTER TABLE plc_device
        ADD COLUMN scan_card_word   SMALLINT UNSIGNED NOT NULL DEFAULT 106
            COMMENT "PLC ghi ma the vua quet vao day (D106)"                AFTER find_answer_word,
        ADD COLUMN scan_card_len    TINYINT  UNSIGNED NOT NULL DEFAULT 2
            COMMENT "So word cua ma the o scan_card_word. 32 bit = 2 word"  AFTER scan_card_word,
        ADD COLUMN weight_band_word SMALLINT UNSIGNED NOT NULL DEFAULT 1004
            COMMENT "SCADA ghi bang tai trong 1/2/3 vao day. 0 = khong co"  AFTER scan_card_len',
    'SELECT "plc_device da co du 3 cot bang tai trong, khong ALTER" AS ket_qua');

PREPARE s FROM @ddl;
EXECUTE s;
DEALLOCATE PREPARE s;

-- Doi chieu: 112 PLC phai cung mot bo dia chi, giong het cac cot thanh ghi khac.
SELECT scan_card_word, scan_card_len, weight_band_word, COUNT(*) AS so_plc
FROM   plc_device
GROUP  BY scan_card_word, scan_card_len, weight_band_word;

-- Doi chieu: bang tai trong suy ra tu weight_class.max_weight_kg, khong hard-code
-- theo ten. NULL = qua tai, khong len duoc pallet nao -> bang 3.
SELECT wc.weight_class_id, wc.code, wc.max_weight_kg,
       CASE WHEN wc.max_weight_kg IS NULL  THEN 3
            WHEN wc.max_weight_kg <= 2200  THEN 1
            WHEN wc.max_weight_kg <= 2600  THEN 2
            ELSE 3 END AS bang_ghi_xuong_D1004,
       (SELECT COUNT(*) FROM parking_card c WHERE c.weight_class_id = wc.weight_class_id) AS so_the
FROM   weight_class wc
ORDER  BY wc.weight_class_id;
