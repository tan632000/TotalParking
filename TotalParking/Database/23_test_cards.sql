-- The TEST dung cho nghiem thu tai hien truong.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi INSERT/UPDATE -> tai khoan ung dung chay duoc.
--
-- ======================= VI SAO =======================
-- Hai the nay dang nam trong thanh ghi o cua block 103 (o 2 va o 3), va mot trong
-- hai da duoc quet o block 96 de tim xe. Chung KHONG co trong danh sach 467 the
-- dang ky, nen moi buoc tra cuu deu tra "khong tim thay".
--
-- Ma the doc duoc tu PLC:
--   62b73250   block 103 o 2  (D202, tho '3250 62B7')
--   6296cac0   block 103 o 3  (D204, tho 'CAC0 6296')
--
-- ======================= BO CUC DA CHOT =======================
-- Binary32Lo (word thap truoc) — DA XAC NHAN ngay 16/09.
--
-- Bang chung: khach cung cap ma the '62bae060', va thanh ghi D400 cua block 96
-- doc ra dung chuoi do theo Binary32Lo, khong can dao byte. Truoc do hai the
-- TEST.001/002 cung nhat quan giua vong quet o va vong poll.
--
-- Da xoa cac dong '...H' (chieu byte nguoc) vi khong con can.
-- Da chot vao plc_device: card_layout = 'Binary32Lo', card_word_len = 2.
--
-- source_label = 'THE TEST' de loc va xoa sach sau khi nghiem thu xong:
--     DELETE FROM parking_card WHERE source_label = 'THE TEST';

USE total_parking;

INSERT INTO parking_card (card_code, card_no, customer_type_id, weight_class_id, source_label, is_active)
VALUES
    ('62b73250', 'TEST.001', 1, 1, 'THE TEST', 1),
    ('6296cac0', 'TEST.002', 1, 1, 'THE TEST', 1),
    ('62bae060', 'TEST.003', 1, 1, 'THE TEST', 1)
ON DUPLICATE KEY UPDATE
    is_active    = 1,
    source_label = 'THE TEST';

-- ------------------------------------------------------------------ doi chieu
SELECT card_id, card_code, card_no, customer_type_id AS loai_khach,
       weight_class_id AS hang_tai, source_label, is_active
FROM   parking_card WHERE source_label = 'THE TEST' ORDER BY card_no;

-- The test nao dang thuc su nam trong thanh ghi PLC
SELECT s.card_code, b.block_no, s.slot_index AS o, CONCAT('D', s.word_addr) AS thanh_ghi,
       (c.card_id IS NOT NULL) AS da_dang_ky
FROM   plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
LEFT   JOIN parking_card c ON c.card_code = s.card_code
WHERE  s.card_code IS NOT NULL
ORDER  BY da_dang_ky DESC, b.block_no, s.slot_index;

-- ------------------------------------------------- chot bo cuc ma the
-- Chot cung thay vi de NULL: de NULL thi moi luot quet phai do lai bo cuc va truy
-- van bang the, va mot bo cuc sai van co xac suat nho cho ra ma trung the khac.
UPDATE plc_device SET card_layout = 'Binary32Lo', card_word_len = 2;

SELECT COUNT(*) AS so_plc, card_layout, card_word_len FROM plc_device
GROUP BY card_layout, card_word_len;
