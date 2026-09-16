-- Trang thai tung o cua block, doc truc tiep tu PLC.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- CAN QUYEN CREATE -> chay bang tai khoan quan tri, khong phai user 'totalparking'.
--
-- ======================= HOP DONG THANH GHI =======================
-- Khach xac nhan: moi block co toi da 10 o, doc theo DUNG thu tu nay:
--
--   o 1  -> D400        o 6  -> D300
--   o 2  -> D202        o 7  -> D302
--   o 3  -> D204        o 8  -> D304
--   o 4  -> D206        o 9  -> D306
--   o 5  -> D208        o 10 -> D308
--
-- Block 4 o doc 4 thanh ghi dau, block 6 o doc 6 dau, block 10 o doc het.
-- Ca 10 thanh ghi CO VAI TRO NHU NHAU: luu ma the cua xe da gui thanh cong vao
-- o do. Doc duoc ma the la biet xe dang nam o block nao, o nao.
--
-- Con dau tien la D400 chu khong phai D200 — khach da xac nhan. Luc dau tuong
-- nham vi day khong deu, nhung do la thuc te cua ladder.
--
-- ======================= DO DUOC TAI THOI DIEM TAO =======================
-- Da doc ca 10 thanh ghi tren 29 PLC ket noi duoc: TAT CA BANG 0.
-- Nhat quan voi viec chua co xe nao duoc gui qua he thong.
--
-- Cac gia tri khac 0 quan sat duoc (D401=0x0301 tren 5 block, D404/D405 khac nhau
-- theo block, D410=0x0101) deu NAM NGOAI danh sach 10 o -> la thanh ghi noi bo cua
-- ladder, khong lien quan. Da kiem bang hai cach doc doc lap.
--
-- ======================= CHUA CHOT =======================
-- MOI O DUNG MAY WORD? Ma the la 32 bit = 2 word (vd a0d22940). Cac dia chi trong
-- khoi D200/D300 cach nhau dung 2 word, phu hop voi 2 word/o. Nhung chua co xe nao
-- de kiem chung, va D400 -> D202 khong theo buoc 2 nen khong suy ra duoc.
-- => de cau hinh 'plc:slotWordCount' trong Web.config, mac dinh 2.
--    Sai gia tri nay thi doc ra ma the rac, KHONG lam hong gi trong PLC (chi doc).
--
-- Chot duoc ngay khi gui thanh cong mot xe vao mot o da biet.

USE total_parking;

CREATE TABLE IF NOT EXISTS plc_slot_state (
    block_id    SMALLINT UNSIGNED NOT NULL,
    -- 1..10, theo dung thu tu thanh ghi o tren
    slot_index  TINYINT  UNSIGNED NOT NULL,
    -- dia chi word dau tien cua o (400, 202, 204, ...). Luu o day chu khong
    -- hard-code trong C#: ladder tung block co the khac nhau, va doi dia chi
    -- khong nen phai build lai ung dung.
    word_addr   SMALLINT UNSIGNED NOT NULL,

    -- NULL = o trong (thanh ghi bang 0). Khac NULL = ma the cua xe dang o day.
    card_code   CHAR(8)           NULL,
    -- Gia tri THO dang hex truoc khi giai ma. Giu lai de con giai ma lai duoc neu
    -- hoa ra bo cuc khac gia dinh ban dau — dung bai hoc tu D100.
    raw_words   VARCHAR(32)       NULL,

    read_at     DATETIME(3)       NULL,
    -- Lan cuoi o nay DOI trang thai. Khac read_at: read_at cap nhat moi vong doc,
    -- changed_at chi doi khi noi dung that su khac -> dung de lan vet xe vao/ra.
    changed_at  DATETIME(3)       NULL,

    PRIMARY KEY (block_id, slot_index),
    -- Tim xe theo ma the: day la duong nong cua chuc nang "tim xe".
    KEY ix_slot_state_card (card_code),
    CONSTRAINT fk_slot_state_block FOREIGN KEY (block_id) REFERENCES block (block_id)
) ENGINE = InnoDB;

-- ------------------------------------------------------------------ seed
-- Moi block sinh dung slot_count dong, danh so 1..N theo thu tu thanh ghi.
-- Chi block co khi: o do nen khong do bang PLC ma bang cam bien PGS/ZCU.
INSERT INTO plc_slot_state (block_id, slot_index, word_addr)
SELECT b.block_id, s.idx, s.addr
FROM   block b
JOIN (
    SELECT 1 AS idx, 400 AS addr UNION ALL SELECT  2, 202 UNION ALL
    SELECT 3, 204 UNION ALL SELECT  4, 206 UNION ALL SELECT  5, 208 UNION ALL
    SELECT 6, 300 UNION ALL SELECT  7, 302 UNION ALL SELECT  8, 304 UNION ALL
    SELECT 9, 306 UNION ALL SELECT 10, 308
) s ON s.idx <= b.slot_count
WHERE  b.kind = 'Mechanical' AND b.is_active = 1
ON DUPLICATE KEY UPDATE word_addr = VALUES(word_addr);

-- ------------------------------------------------------------ v_slot_occupancy
-- Nhin xuyen tu o len zone. Day la thu thay the cach dem cu (dem parking_session):
-- PLC bao truc tiep o nao co xe, khong phai he thong tu suy.
CREATE OR REPLACE VIEW v_slot_occupancy AS
SELECT b.zone_id, b.block_no, b.block_id,
       COUNT(*)                                    AS so_o,
       SUM(s.card_code IS NOT NULL)                AS o_co_xe,
       SUM(s.card_code IS NULL)                    AS o_trong,
       MAX(s.read_at)                              AS doc_luc
FROM   plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
GROUP  BY b.zone_id, b.block_no, b.block_id;

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS so_dong, COUNT(DISTINCT block_id) AS so_block FROM plc_slot_state;

SELECT b.zone_id, COUNT(*) AS so_o
FROM   plc_slot_state s JOIN block b ON b.block_id = s.block_id
GROUP  BY b.zone_id ORDER BY b.zone_id;

SELECT slot_index, word_addr, COUNT(*) AS so_block
FROM   plc_slot_state GROUP BY slot_index, word_addr ORDER BY slot_index;
