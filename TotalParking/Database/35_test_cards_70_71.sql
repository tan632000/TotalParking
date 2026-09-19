-- Them cac the dang duoc dung de nghiem thu tai hien truong (19/09).
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi INSERT/UPDATE -> tai khoan ung dung chay duoc.
--
-- ======================= LUU Y QUAN TRONG =======================
-- Them the vao danh ba KHONG sua duoc loi "khong tim thay xe" ma khach dang gap.
--
-- plc:requireRegisteredCard dang la false, nghia la he thong KHONG doi the phai
-- dang ky moi tim duoc xe. Nguyen nhan that la cac ma nay dang nam trong thanh
-- ghi o cua NHIEU BLOCK cung luc:
--
--   62bae060   9 block:  3, 4, 41, 58, 83, 85, 86, 96, 103
--   62a84980   6 block:  73, 74, 81, 93, 94, 109
--   62b2fee0   5 block:  62, 73, 82, 109, 112
--   62b8c1f0   5 block:  1, 2, 74, 81, 93
--
-- Quy tac "mot the chi o mot block" loai chung -- dung, vi mot chiec xe khong
-- the o chin noi.
--
-- Vi sao thanh nhu vay: ladder hieu MOI LUOT QUET tai mot block la mot luot gui
-- xe thanh cong tai block do, va ghi ma the vao mot o cua block ay (dung hop dong
-- khach mo ta: "gui xe thanh cong thi ghi xuong D400...D308"). Nguoi thu quet
-- cung mot the o nhieu block de kiem tra tim xe, va moi lan quet lai them mot
-- ban ghi "xe dang do o day".
--
-- Nen gia tri cua file nay chi la: moi luot quet hien ra TEN THE thay vi ma tho,
-- de doi chieu nhat ky. Muon tim xe chay duoc thi phai xoa thanh ghi o o cac
-- block khac -- viec do nam o phia PLC, khong phai o day.
--
-- source_label = 'THE TEST' de xoa sach sau khi nghiem thu xong:
--     DELETE FROM parking_card WHERE source_label = 'THE TEST';

USE total_parking;

INSERT INTO parking_card (card_code, card_no, customer_type_id, weight_class_id, source_label, is_active)
VALUES
    ('62b2fee0', 'TEST.004', 1, 1, 'THE TEST', 1),   -- the khach dang quet 19/09
    ('62a84980', 'TEST.005', 1, 1, 'THE TEST', 1),
    ('62b8c1f0', 'TEST.006', 1, 1, 'THE TEST', 1),
    ('62a53a20', 'TEST.007', 1, 1, 'THE TEST', 1),
    ('6292c030', 'TEST.008', 1, 1, 'THE TEST', 1),   -- dang sach, chi o block 87
    ('62b6c1c0', 'TEST.009', 1, 1, 'THE TEST', 1)    -- dang sach, chi o block 96
ON DUPLICATE KEY UPDATE
    is_active    = 1,
    source_label = 'THE TEST';

-- ------------------------------------------------------------------ doi chieu
SELECT card_id, card_code, card_no, source_label, is_active
FROM   parking_card WHERE source_label = 'THE TEST' ORDER BY card_no;

-- The test nao dang nam o may block, va co duoc tinh la xe khong
SELECT c.card_no, s.card_code,
       COUNT(DISTINCT s.block_id) AS so_block,
       GROUP_CONCAT(DISTINCT b.block_no ORDER BY b.block_no) AS cac_block,
       IF(COUNT(DISTINCT s.block_id) = 1, 'tim duoc', 'BI LOAI (nhieu block)') AS ket_qua
FROM   parking_card c
JOIN   plc_slot_state s ON s.card_code = c.card_code
JOIN   block b ON b.block_id = s.block_id
WHERE  c.source_label = 'THE TEST'
GROUP  BY c.card_no, s.card_code
ORDER  BY so_block DESC;
