-- Doi nguon "da dung" cua so tren bang LED: parking_session -> plc_slot_state.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi CREATE OR REPLACE VIEW -> can tai khoan co quyen DDL.
--
-- ======================= VI SAO =======================
-- Hai view v_led_capacity / v_led_capacity_zone lay MAU SO tu block.slot_count
-- (that, tu ban ve) nhung lay phan DA DUNG tu parking_session. Bang do dang co
-- 0 dong va khong co gi ghi vao no, nen free = total - 0 = total: bang LED luon
-- bao con trong dung bang suc chua toi da, ke ca khi xe da vao.
--
-- Su that ve cho trong nam o plc_slot_state: vong quet doc thang thanh ghi o cua
-- tung PLC, o nao dang giu ma the tuc la o do dang co xe. Do la thu thiet bi bao,
-- khong phai thu SCADA tu suy ra — nen dung ngay ca khi co nguoi thao tac tay tai
-- HMI ma SCADA khong biet.
--
-- ======================= LOC RAC =======================
-- Thanh ghi o co the khac 0 ma khong phai ma the. Thuc te dang co gia tri
-- '03010000' xuat hien o BAY block cung luc (27,38,39,40,41,64,84) — do la thanh
-- ghi noi bo cua ladder.
--
-- Quy tac loc: MOT MA THE CHI DUOC NAM O DUNG MOT BLOCK. Xe khong o hai noi cung
-- luc. Day dung quy tac ma CarLocatorService dang dung cho luong tim xe, nen so
-- tren bang LED va ket qua tim xe khong the lech nhau.
--
-- Khong loc thi LED se tru nham 7 cho khong co xe.
--
-- ======================= O CHUA DOC DUOC =======================
-- Hien co 425/755 o chua tung doc duoc lan nao vi 58/112 PLC dang tat dien
-- (ca zone 5 tat sach). Nhung o do van tinh la TRONG.
--
-- Day la lua chon co chu dich. Neu loai chung khoi mau so thi so tren bien tut
-- xuong ~330 va tai xe nhin vao se tuong ham gan day — te hon la hoi lac quan.
-- Cach nay giu nguyen muc lac quan nhu hien tai, nhung o nao DOC DUOC thi tru
-- dung. Chi tot len so voi hien trang, khong co mat xau nao.
--
-- ======================= CHO MAT DAT (Ground) =======================
-- used_standard VAN bang 0. Cac block Ground khong co dong nao trong
-- plc_slot_state (file 17 chi seed block Mechanical) — cho dau xe mat dat do cam
-- bien PGS/ZCU bao, va y nghia bit cua chung (co xe / co lap dat) van chua giai
-- duoc. Nen bo dem chuan van la 80/80 cho toi khi giai xong bit PGS.
-- KHONG doan bua o day: doan sai thi bang dau ham hien sai so cho ca bai.

USE total_parking;

-- ------------------------------------------------- v_slot_taken
-- O dang thuc su giu xe, da loc rac. Tach rieng ra de hai view duoi dung chung
-- MOT dinh nghia — de hai noi tu viet lai dieu kien loc thi som muon cung lech.
CREATE OR REPLACE VIEW v_slot_taken AS
SELECT s.block_id, s.slot_index, s.card_code
FROM   plc_slot_state s
WHERE  s.card_code IS NOT NULL
  AND  (SELECT COUNT(DISTINCT s2.block_id)
        FROM   plc_slot_state s2
        WHERE  s2.card_code = s.card_code) = 1;

-- ------------------------------------------------- v_led_capacity (toan bai)
CREATE OR REPLACE VIEW v_led_capacity AS
SELECT
    t.total_l5m, t.total_l48m, t.total_standard,
    u.used_l5m,  u.used_l48m,  u.used_standard, u.used_unassigned,
    GREATEST(CAST(t.total_l5m      AS SIGNED) - u.used_l5m,      0) AS free_l5m,
    GREATEST(CAST(t.total_l48m     AS SIGNED) - u.used_l48m,     0) AS free_l48m,
    GREATEST(CAST(t.total_standard AS SIGNED) - u.used_standard, 0) AS free_standard
FROM (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm >  4800 THEN b.slot_count END), 0) AS total_l5m,
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm <= 4800 THEN b.slot_count END), 0) AS total_l48m,
        COALESCE(SUM(CASE WHEN b.kind = 'Ground'       THEN b.slot_count END), 0) AS total_standard
    FROM block b WHERE b.is_active = 1
) t
CROSS JOIN (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm >  4800 THEN 1 END), 0) AS used_l5m,
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm <= 4800 THEN 1 END), 0) AS used_l48m,
        COALESCE(SUM(CASE WHEN b.kind = 'Ground'       THEN 1 END), 0) AS used_standard,
        -- Giu nguyen y nghia cu: xe da quet the ma chua duoc gan cho. Cot nay
        -- chi hien o /LedStatus de chan doan, khong tham gia tinh so tren bang.
        (SELECT COUNT(*) FROM parking_session ps
         WHERE ps.active_card_id IS NOT NULL AND ps.block_id IS NULL) AS used_unassigned
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.is_active = 1
) u;

-- ------------------------------------------------- v_led_capacity_zone
-- Suc chua theo TUNG ZONE, cung ba bo dem voi v_led_capacity.
-- Bang chi huong phai hien so cua zone ma mui ten dan toi, khong phai so toan bai.
CREATE OR REPLACE VIEW v_led_capacity_zone AS
SELECT z.zone_id,
       t.total_l5m, t.total_l48m, t.total_standard,
       u.used_l5m,  u.used_l48m,  u.used_standard,
       GREATEST(CAST(t.total_l5m      AS SIGNED) - u.used_l5m,      0) AS free_l5m,
       GREATEST(CAST(t.total_l48m     AS SIGNED) - u.used_l48m,     0) AS free_l48m,
       GREATEST(CAST(t.total_standard AS SIGNED) - u.used_standard, 0) AS free_standard
FROM   zone z
JOIN LATERAL (
    SELECT
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm >  4800 THEN b.slot_count END),0) AS total_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm <= 4800 THEN b.slot_count END),0) AS total_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                 THEN b.slot_count END),0) AS total_standard
    FROM block b WHERE b.zone_id = z.zone_id AND b.is_active = 1
) t ON TRUE
JOIN LATERAL (
    SELECT
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm >  4800 THEN 1 END),0) AS used_l5m,
      COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm <= 4800 THEN 1 END),0) AS used_l48m,
      COALESCE(SUM(CASE WHEN b.kind='Ground'                                 THEN 1 END),0) AS used_standard
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.zone_id = z.zone_id AND b.is_active = 1
) u ON TRUE
WHERE  z.is_active = 1;

-- ------------------------------------------------------------------ doi chieu

-- 1. O nao dang bi tinh la co xe, va ma nao bi loai vi nam o nhieu block
SELECT 'DUOC TINH' AS trang_thai, s.card_code, b.block_no, s.slot_index AS o
FROM   v_slot_taken s JOIN block b ON b.block_id = s.block_id
UNION ALL
SELECT 'LOAI (rac)', s.card_code, b.block_no, s.slot_index
FROM   plc_slot_state s JOIN block b ON b.block_id = s.block_id
WHERE  s.card_code IS NOT NULL
  AND  s.card_code NOT IN (SELECT card_code FROM v_slot_taken)
ORDER  BY trang_thai, block_no, o;

-- 2. So se len bang LED toan bai
SELECT * FROM v_led_capacity;

-- 3. So se len tung bang chi huong
SELECT z.zone_id, c.total_l5m, c.used_l5m, c.free_l5m, c.free_standard
FROM   v_led_capacity_zone c JOIN zone z ON z.zone_id = c.zone_id
ORDER  BY z.zone_id;

-- 4. Bao nhieu o chua doc duoc lan nao (dang duoc tinh la TRONG)
SELECT COUNT(*) AS tong_o,
       SUM(read_at IS NULL) AS chua_doc_lan_nao,
       SUM(read_at IS NOT NULL) AS da_doc_duoc
FROM   plc_slot_state;
