-- Doi chieu block dich sau khi chay 38_routing_block.sql.
-- Chi DOC, khong sua gi. UTF-8 KHONG BOM.
--
-- MOC THOI GIAN: bang nay da co 533 dong outcome = 'ROUTED' voi block_no NULL,
-- ghi TRUOC khi cot block_no ton tai. Chung hop le va duoc giu nguyen. Nen moi
-- kiem tra "ROUTED phai co block" chi soi cac dong quyet dinh TU LUC trigger
-- duoc tao tro di. Moc do lay tu information_schema chu khong go tay, de chay
-- lai luc nao cung dung.

USE total_parking;

SET @moc := (SELECT MIN(CREATED) FROM information_schema.TRIGGERS
             WHERE TRIGGER_SCHEMA = DATABASE()
               AND EVENT_OBJECT_TABLE = 'vehicle_routing');

-- 0) Trigger phai con do. Mat trigger la mat toan bo rang buoc, va khong co
--    kiem tra nao ben duoi phat hien ra dieu do.
SELECT 'Trigger con day du' AS kiem_tra,
       COUNT(*) AS so_trigger,
       IF(COUNT(*) = 2, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(TRIGGER_NAME ORDER BY TRIGGER_NAME SEPARATOR ' '), '-') AS chi_tiet
FROM   information_schema.TRIGGERS
WHERE  TRIGGER_SCHEMA = DATABASE() AND EVENT_OBJECT_TABLE = 'vehicle_routing';

-- 1) R2.1 - ROUTED ghi sau moc phai co block_no.
SELECT 'R2.1 ROUTED moi deu co block_no' AS kiem_tra,
       COUNT(*) AS so_dong_sai,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(event_id SEPARATOR ','), '-') AS chi_tiet
FROM   vehicle_routing
WHERE  outcome = 'ROUTED' AND block_no IS NULL AND decided_at >= @moc;

-- 2) R2.1 - block dich phai nam dung zone da ghi tren cung dong.
SELECT 'R2.1 block dich dung zone' AS kiem_tra,
       COUNT(*) AS so_dong_lech_zone,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT(r.event_id, ':block', r.block_no) SEPARATOR ' '), '-') AS chi_tiet
FROM   vehicle_routing r
JOIN   block b ON b.block_no = r.block_no
WHERE  r.block_no IS NOT NULL AND r.zone_id IS NOT NULL AND b.zone_id <> r.zone_id;

-- 3) R2.4 - outcome khac ROUTED thi block_no phai NULL.
SELECT 'R2.4 outcome khac ROUTED khong co block' AS kiem_tra,
       COUNT(*) AS so_dong_sai,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT(event_id, ':', outcome) SEPARATOR ' '), '-') AS chi_tiet
FROM   vehicle_routing
WHERE  outcome <> 'ROUTED' AND block_no IS NOT NULL;

-- 4) R2.1 - khong duoc chi mot chiec xe toi block da day.
SELECT 'R2.1 block dich chua day' AS kiem_tra,
       COUNT(*) AS so_dong_tro_toi_block_day,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT(r.event_id, ':block', r.block_no) SEPARATOR ' '), '-') AS chi_tiet
FROM   vehicle_routing r
JOIN   block b ON b.block_no = r.block_no
WHERE  r.block_no IS NOT NULL AND r.decided_at >= @moc
  AND  (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id = b.block_id) >= b.slot_count;

-- 5) D6 - khong duoc chon block PLC bo quen trong khi con block duoc bao cao
--    gan day co chong it nhat bang. Kiem tra nay soi LUAT tren du lieu hien tai,
--    khong soi lich su: no tra loi cau "neu chon lai bay gio thi co sai khong".
WITH cand AS (
    SELECT b.zone_id, b.block_no,
           b.slot_count
             - (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id = b.block_id)
             - (SELECT COUNT(*) FROM vehicle_routing r
                  WHERE r.block_no = b.block_no AND r.outcome = 'ROUTED'
                    AND r.decided_at > NOW(3) - INTERVAL 90 SECOND) AS free_capacity,
           (SELECT COUNT(*) FROM plc_slot_state s WHERE s.block_id = b.block_id
              AND s.read_at >= NOW() - INTERVAL 5 MINUTE) > 0 AS fresh
    FROM   block b WHERE b.is_active = 1),
pick AS (
    SELECT zone_id, block_no, free_capacity, fresh,
           ROW_NUMBER() OVER (PARTITION BY zone_id
                              ORDER BY fresh DESC, free_capacity DESC, block_no ASC) AS rn
    FROM   cand WHERE free_capacity > 0)
SELECT 'D6 khong chon block cu khi con block moi' AS kiem_tra,
       COUNT(*) AS so_zone_xep_sai,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT('zone', p.zone_id, ':block', p.block_no) SEPARATOR ' '), '-') AS chi_tiet
FROM   pick p
WHERE  p.rn = 1 AND p.fresh = 0
  AND  EXISTS (SELECT 1 FROM cand c
               WHERE c.zone_id = p.zone_id AND c.fresh = 1
                 AND c.free_capacity >= p.free_capacity);

-- 6) R2.3 - doc lai cung mot su kien phai ra cung mot block. Hai lan doc rieng
--    biet, so sanh bang <=> de NULL cung so duoc voi NULL.
SET @e  := (SELECT event_id FROM vehicle_routing
            WHERE outcome = 'ROUTED' AND block_no IS NOT NULL
            ORDER BY decided_at DESC LIMIT 1);
SET @b1 := (SELECT block_no FROM vehicle_routing WHERE event_id = @e);
SET @b2 := (SELECT block_no FROM vehicle_routing WHERE event_id = @e);
SELECT 'R2.3 doc lai ra cung block' AS kiem_tra,
       @e AS su_kien,
       CASE WHEN @e IS NULL THEN 'FAIL'
            WHEN @b1 <=> @b2 THEN 'PASS' ELSE 'FAIL' END AS ket_qua,
       CONCAT('lan 1 = ', IFNULL(@b1,'NULL'), ', lan 2 = ', IFNULL(@b2,'NULL')) AS chi_tiet;

-- 7) Doi chieu: lich su phai con nguyen ven, khong bi migration cham vao.
SELECT 'Doi chieu' AS kiem_tra,
       @moc AS moc_trigger,
       (SELECT COUNT(*) FROM vehicle_routing WHERE outcome='ROUTED' AND block_no IS NULL AND decided_at < @moc) AS lich_su_giu_nguyen,
       (SELECT COUNT(*) FROM vehicle_routing WHERE block_no IS NOT NULL) AS dong_co_block,
       (SELECT COUNT(*) FROM vehicle_routing) AS tong_dong;
