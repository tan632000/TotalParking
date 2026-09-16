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
-- ======================= LUU Y VE BO CUC =======================
-- Ma tren la giai ma theo Binary32Lo (word thap truoc). CHUA XAC NHAN day la thu
-- tu dung — chi biet no nhat quan giua vong quet o va vong poll.
--
-- Neu hoa ra thu tu nguoc (Binary32Hi) thi ma that se la '3250 62b7' va 'cac0 6296'.
-- Da them CA HAI chieu de bai test khong bi chan boi cau hoi con dang bo ngo.
-- Khi chot duoc bo cuc, XOA cac dong con lai.
--
-- source_label = 'THE TEST' de loc va xoa sach sau khi nghiem thu xong:
--     DELETE FROM parking_card WHERE source_label = 'THE TEST';

USE total_parking;

INSERT INTO parking_card (card_code, card_no, customer_type_id, weight_class_id, source_label, is_active)
VALUES
    -- doc theo Binary32Lo — bo cuc dang dung
    ('62b73250', 'TEST.001', 1, 1, 'THE TEST', 1),
    ('6296cac0', 'TEST.002', 1, 1, 'THE TEST', 1),
    -- doc theo Binary32Hi — phong truong hop thu tu word nguoc lai
    ('325062b7', 'TEST.001H', 1, 1, 'THE TEST', 1),
    ('cac06296', 'TEST.002H', 1, 1, 'THE TEST', 1)
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
