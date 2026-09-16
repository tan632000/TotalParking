-- Hop dong thanh ghi MOI cho luong tim xe. Thay the D402 + W75.0.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- CAN QUYEN ALTER -> chay bang tai khoan quan tri.
--
-- ======================= LUONG THAT (khach xac nhan) =======================
--
--   1. Quet RFID luc GUI xe  -> PLC ghi D106.  SCADA KHONG can quan tam.
--
--   2. Gui xe thanh cong     -> PLC ghi ma the vao thanh ghi cua O tuong ung:
--                               o 1 -> D400, o 2..5 -> D202 D204 D206 D208,
--                               o 6..10 -> D300 D302 D304 D306 D308
--                               (da lam o 17_plc_slot_state.sql)
--
--   3. TIM XE: khach quet RFID o MOT BLOCK KHAC
--        SCADA doc  D1002  -> ma the vua quet
--        SCADA tra  D1000  -> SO BLOCK noi xe do dang thuc su dau
--
-- ======================= BO D402 VA W75.0 =======================
-- Hop dong cu (D100 doc the / D402 tra 2200-2600 / W75.0 tra 1-0 dung sai block)
-- KHONG con dung. Giu lai cot trong bang de khong pha du lieu cu, nhung ma nguon
-- da ngung ghi hai thanh ghi do.
--
-- Khac biet ve ban chat: W75.0 chi tra duoc DUNG/SAI (1 bit). D1000 tra ve
-- SO BLOCK — tra loi duoc cau "xe dang o dau", khong chi "co phai o day khong".
--
-- ======================= CHUA CHOT =======================
-- D1002 dai MAY WORD? Ma the la 32 bit = 2 word (vd a0d22940) nen dat mac dinh 2.
-- Chua co luot quet that de kiem chung. Sai gia tri nay chi lam giai ma ra ma the
-- rac -> khong tim thay xe; KHONG ghi nham gi xuong PLC.
--
-- D1000 tra ve gi khi KHONG tim thay xe? Dat 0 = khong tim thay. Can khach xac
-- nhan ladder hieu 0 dung nhu vay, chu khong phai "block 0".

USE total_parking;

-- ------------------------------------------------------------------ cot moi
SET @sql := IF(
  (SELECT COUNT(*) FROM information_schema.columns
   WHERE table_schema = 'total_parking' AND table_name = 'plc_device'
     AND column_name = 'find_card_word') = 0,
  'ALTER TABLE plc_device
     ADD COLUMN find_card_word   SMALLINT UNSIGNED NOT NULL DEFAULT 1002
         COMMENT ''D1002: ma the vua quet de tim xe (SCADA doc)'',
     ADD COLUMN find_card_len    TINYINT  UNSIGNED NOT NULL DEFAULT 2
         COMMENT ''So word cua ma the o D1002. Ma the 32 bit = 2 word.'',
     ADD COLUMN find_answer_word SMALLINT UNSIGNED NOT NULL DEFAULT 1000
         COMMENT ''D1000: so block noi xe dang dau (SCADA ghi). 0 = khong tim thay.''',
  'SELECT ''cac cot da ton tai, bo qua'' AS ghi_chu');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

UPDATE plc_device
SET    find_card_word = 1002, find_card_len = 2, find_answer_word = 1000;

-- ------------------------------------------------------------ v_car_location
-- Xe dang o dau: tra theo ma the, ra so block.
-- Day la nguon tra loi cho D1000.
-- JOIN parking_card (khong phai LEFT JOIN) la CO Y: chi tinh la xe khi ma doc
-- duoc khop MOT THE THAT. Thanh ghi khac 0 nhung khong khop the nao la du lieu la
-- — dem no vao day se tra ve so block cho mot chiec xe khong ton tai.
-- Thuc te dang co 5 o nhu vay: D401 = 0x0301 tren 5 block, giai ma ra '03010000'.
CREATE OR REPLACE VIEW v_car_location AS
SELECT s.card_code,
       c.card_no,
       b.block_no,
       b.zone_id,
       b.block_id,
       s.slot_index,
       s.word_addr,
       s.read_at,
       s.changed_at
FROM   plc_slot_state s
JOIN   block b        ON b.block_id  = s.block_id
JOIN   parking_card c ON c.card_code = s.card_code
WHERE  s.card_code IS NOT NULL;

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS so_plc,
       MIN(find_card_word)   AS d_doc_the,
       MIN(find_answer_word) AS d_tra_block,
       MIN(find_card_len)    AS so_word
FROM   plc_device;

SELECT COUNT(*) AS so_xe_dang_do FROM v_car_location;
