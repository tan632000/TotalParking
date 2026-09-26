-- Dem o co xe theo THANH GHI, ke ca khi mot ma the nam o nhieu block.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO DOI =======================
-- File 24 dat luat "mot ma the chi duoc nam o dung mot block" de loc rac, va
-- file 27 giu nguyen luat do. Luat ay bat duoc rac that, nhung no loc theo
-- chieu NGUY HIEM ma luc dat ra khong ai nhin thay:
--
--   the trung 2 block  ->  bi loai khoi v_slot_taken
--                      ->  CA HAI o duoc tinh la TRONG
--                      ->  BlockAllocator.PickSql cong them 2 vao free_capacity
--                      ->  he thong co the xep xe vao block da day
--
-- Do la va cham vat ly. Doi lai, neu tin thanh ghi thi ca hai o bi chan: mat
-- tam mot suat suc chua cho toi khi giai quyet xong, nhung khong bao gio xep
-- chong len xe dang do. Mat mot cho la phien; xep chong la tai nan.
--
-- ======================= NHUNG KHONG BO TRANG LUAT CU =======================
-- Luat "mot block" sinh ra de bat mot dang rac co that: gia tri '03010000'
-- tung xuat hien o 6-7 block cung luc (xem file 24). Luat gia tri toi thieu o
-- file 27 KHONG bat duoc no vi 0x03010000 = 50.462.720 > 65535.
--
-- Nen thay vi bo trang, luat cu duoc THU HEP lai dung pham vi no co ich:
--
--   ma CO trong parking_card     -> the that, tin thanh ghi du trung may block
--   ma KHONG co trong danh muc   -> chi tin khi no nam o dung mot block
--
-- Danh muc the la trong tai. Cung trong tai ma SlotOccupancyReader dung de
-- phan biet o "co xe" voi o "the la", nen ba noi noi cung mot thu tieng.
--
-- Doi chieu du lieu luc viet file nay:
--   a0ace420  2 block  co trong danh muc  (the TEST.015, khach dang test)
--   03010000  --       khong co           (rac sau khi ladder khoi tao lai)
--
-- ======================= CHO NAY KHONG DOI =======================
-- CarLocatorService.cs VAN loai thang ma nam o nhieu block. Do la co y, khong
-- phai bo sot: dem suc chua va tra loi "xe toi o dau" la hai muc dich khac
-- nhau. Dem thi chan ca hai o moi an toan. Tra loi thi phai chon MOT block, va
-- chon sai nghia la chi tai xe di nham tang. Tha noi "khong biet".
-- Ghi chu o CarLocatorService.cs da duoc sua cho khop voi quyet dinh nay.

USE total_parking;

CREATE OR REPLACE VIEW v_slot_taken AS
SELECT s.block_id, s.slot_index, s.card_code
FROM   plc_slot_state s
WHERE  s.card_code IS NOT NULL
  -- (1) the co trong danh muc thi tin thanh ghi; the la thi van doi duy nhat
  AND  (EXISTS (SELECT 1 FROM parking_card c WHERE c.card_code = s.card_code)
        OR (SELECT COUNT(DISTINCT s2.block_id)
            FROM   plc_slot_state s2
            WHERE  s2.card_code = s.card_code) = 1)
  -- (2) gia tri phai du lon de la ma the (giu nguyen tu file 27)
  AND  (LENGTH(s.card_code) < 8 OR CONV(s.card_code, 16, 10) > 65535);

-- ------------------------------------------------------------------ doi chieu
-- Cot `vi_sao` noi ro tung dong duoc tinh hay bi loai VI LY DO GI, de nguoi
-- chay migration khong phai suy dien tu con so tong.
SELECT CASE
         WHEN t.block_id IS NOT NULL THEN 'DUOC TINH'
         ELSE 'LOAI'
       END                                             AS ket_qua,
       b.block_no, s.slot_index AS o, s.card_code,
       CASE
         WHEN c.card_id IS NOT NULL THEN 'the co trong danh muc'
         WHEN n.so_block > 1        THEN CONCAT('the la, nam o ', n.so_block, ' block')
         ELSE 'the la, nam o mot block'
       END                                             AS vi_sao
FROM   plc_slot_state s
JOIN   block b        ON b.block_id  = s.block_id
LEFT   JOIN parking_card c ON c.card_code = s.card_code
LEFT   JOIN v_slot_taken t ON t.block_id  = s.block_id AND t.slot_index = s.slot_index
LEFT   JOIN (SELECT card_code, COUNT(DISTINCT block_id) AS so_block
             FROM   plc_slot_state WHERE card_code IS NOT NULL
             GROUP  BY card_code) n ON n.card_code = s.card_code
WHERE  s.card_code IS NOT NULL
ORDER  BY ket_qua, b.block_no, s.slot_index;

-- Ma the nam o nhieu hon mot block: day chinh la thu dich vu canh bao se bao.
SELECT s.card_code,
       COUNT(DISTINCT s.block_id)                      AS so_block,
       GROUP_CONCAT(DISTINCT b.block_no ORDER BY b.block_no) AS cac_block,
       (c.card_id IS NOT NULL)                         AS co_trong_danh_muc
FROM   plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
LEFT   JOIN parking_card c ON c.card_code = s.card_code
WHERE  s.card_code IS NOT NULL
GROUP  BY s.card_code, co_trong_danh_muc
HAVING so_block > 1
ORDER  BY so_block DESC;

SELECT * FROM v_led_capacity;
