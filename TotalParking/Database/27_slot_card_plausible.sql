-- Loc them mot dang rac trong thanh ghi o: gia tri QUA NHO de la ma the.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO CAN THEM =======================
-- File 24 loc rac bang quy tac "mot ma the chi nam o dung mot block". Quy tac do
-- bat duoc '03010000' (xuat hien o 6-7 block cung luc), nhung co mot lo hong:
-- rac chi xuat hien o MOT block thi lot.
--
-- Da xay ra that. Sau khi co dien lai luc 04:00 ngay 17/09, thanh ghi D400 cua
-- block 27 doi thanh '000003e8' luc 04:24 -- ghi nhan trong plc_audit.log:
--
--   04:24:28  READ  192.169.1.127  block 27  D400 = 03E8 0000  | o 1: 03010000 -> 000003e8
--
-- 0x3E8 = 1000. Do la ladder khoi tao lai thanh ghi sau reboot, khong phai xe.
-- Nhung no chi o block 27 nen quy tac duy nhat khong bat duoc, va bang LED tru
-- nham mot cho (753 thay vi 754).
--
-- ======================= QUY TAC THEM =======================
-- Ma the 32 bit that cua he thong nay deu co word CAO khac 0:
--   62b73250   6296cac0   62bae060
-- Gia tri nho hon 65536 (word cao = 0) khong phai ma the -- do la so dem, ma
-- trang thai, hoac thanh ghi noi bo cua ladder.
--
-- Chi ap dung cho ma DAI 8 KY TU (bo cuc 2 word). Neu sau nay doi sang
-- card_word_len = 1 thi ma chi dai 4 ky tu va luon < 65536; dieu kien
-- LENGTH < 8 giu cho no khong bi loai sach.
--
-- ======================= CAI GIA NEU SAI =======================
-- Mot the that co ma < 65536 se khong tim thay xe. Doi lai, khong loc thi bang
-- LED dem thieu cho va luong tim xe co the chi nguoi ta toi block khong co xe ho.
-- Ca ba the dang dung deu bat dau 0x62, nen rui ro thuc te rat nho -- nhung day
-- LA mot gia dinh, can bo neu khach cap the co ma thap.

USE total_parking;

CREATE OR REPLACE VIEW v_slot_taken AS
SELECT s.block_id, s.slot_index, s.card_code
FROM   plc_slot_state s
WHERE  s.card_code IS NOT NULL
  -- (1) mot ma the chi duoc nam o dung mot block
  AND  (SELECT COUNT(DISTINCT s2.block_id)
        FROM   plc_slot_state s2
        WHERE  s2.card_code = s.card_code) = 1
  -- (2) gia tri phai du lon de la ma the
  AND  (LENGTH(s.card_code) < 8 OR CONV(s.card_code, 16, 10) > 65535);

-- ------------------------------------------------------------------ doi chieu
SELECT 'DUOC TINH' AS ket_qua, s.card_code, b.block_no, s.slot_index AS o
FROM   v_slot_taken s JOIN block b ON b.block_id = s.block_id
UNION ALL
SELECT 'LOAI', s.card_code, b.block_no, s.slot_index
FROM   plc_slot_state s JOIN block b ON b.block_id = s.block_id
WHERE  s.card_code IS NOT NULL
  AND  s.card_code NOT IN (SELECT card_code FROM v_slot_taken)
ORDER  BY ket_qua, block_no, o;

SELECT * FROM v_led_capacity;
