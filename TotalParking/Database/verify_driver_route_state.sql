-- Doi chieu: mot quyet dinh dieu huong se ra trang thai nao tren man hinh tai xe.
-- Gieo du lieu roi ROLLBACK — khong dong nao con lai sau khi chay. UTF-8 KHONG BOM.
--
-- Luat phan giai o day phai GIONG HET /Monitor/DriverRoute:
--
--   ngoai cua so 90 giay                         -> WAITING
--   ROUTED, co block, block co nut den, toi duoc -> ROUTE
--   moi truong hop con lai                       -> MESSAGE
--
-- Script khong tu dat ra luat moi; no kiem tra rang du lieu roi vao dung o nao.
-- Cua so do bang TIMESTAMPDIFF theo MICROSECOND, giong endpoint, de moc 90.000
-- giay van con duoc tinh la con hieu luc.

USE total_parking;

-- Muon nam dong that de gieo. vehicle_routing.event_id co khoa ngoai toi
-- vehicle_event nen khong bia ra event_id moi duoc; lay dong san co roi tra lai
-- nguyen trang bang ROLLBACK.
SET @e1 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 0);
SET @e2 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 1);
SET @e3 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 2);
SET @e4 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 3);
SET @e5 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 4);

-- Block 112 co nut den va toi duoc; block 901 la bai do nen, khong co nut den.
SET @co_nut     := 112;
SET @khong_nut  := 901;

START TRANSACTION;

UPDATE vehicle_routing SET outcome='ROUTED', block_no=@co_nut,
       decided_at = NOW(3) - INTERVAL 89 SECOND  WHERE event_id = @e1;
UPDATE vehicle_routing SET outcome='ROUTED', block_no=@co_nut,
       decided_at = NOW(3) - INTERVAL 91 SECOND  WHERE event_id = @e2;
UPDATE vehicle_routing SET outcome='NO_CAPACITY', block_no=NULL,
       decided_at = NOW(3) - INTERVAL 10 SECOND  WHERE event_id = @e3;
UPDATE vehicle_routing SET outcome='REJECTED', block_no=NULL,
       decided_at = NOW(3) - INTERVAL 10 SECOND  WHERE event_id = @e4;
UPDATE vehicle_routing SET outcome='ROUTED', block_no=@khong_nut,
       decided_at = NOW(3) - INTERVAL 10 SECOND  WHERE event_id = @e5;

WITH RECURSIVE hai_chieu AS (
    SELECT from_node AS a, to_node AS b FROM lane_edge
    UNION ALL
    SELECT to_node,   from_node        FROM lane_edge),
toi_duoc AS (
    SELECT node_id FROM lane_node WHERE is_entry = 1
    UNION
    SELECT e.b FROM toi_duoc t JOIN hai_chieu e ON e.a = t.node_id),
gieo AS (
    SELECT r.event_id, r.outcome, r.block_no,
           ROUND(TIMESTAMPDIFF(MICROSECOND, r.decided_at, NOW(3)) / 1000000) AS tuoi_giay,
           TIMESTAMPDIFF(MICROSECOND, r.decided_at, NOW(3)) <= 90000000 AS con_hieu_luc,
           b.lane_node_id,
           (b.lane_node_id IS NOT NULL
            AND b.lane_node_id IN (SELECT node_id FROM toi_duoc)) AS ve_duoc
    FROM   vehicle_routing r
    LEFT   JOIN block b ON b.block_no = r.block_no
    WHERE  r.event_id IN (@e1, @e2, @e3, @e4, @e5))
SELECT event_id, outcome, block_no, tuoi_giay,
       CASE WHEN con_hieu_luc = 0                                        THEN 'WAITING'
            WHEN outcome = 'ROUTED' AND block_no IS NOT NULL AND ve_duoc THEN 'ROUTE'
            ELSE 'MESSAGE' END AS trang_thai,
       CASE
         WHEN event_id = @e1 THEN IF(con_hieu_luc = 1 AND outcome='ROUTED' AND ve_duoc, 'PASS', 'FAIL')
         WHEN event_id = @e2 THEN IF(con_hieu_luc = 0, 'PASS', 'FAIL')
         WHEN event_id = @e3 THEN IF(con_hieu_luc = 1 AND outcome <> 'ROUTED', 'PASS', 'FAIL')
         WHEN event_id = @e4 THEN IF(con_hieu_luc = 1 AND outcome <> 'ROUTED', 'PASS', 'FAIL')
         WHEN event_id = @e5 THEN IF(con_hieu_luc = 1 AND ve_duoc = 0, 'PASS', 'FAIL')
       END AS ket_qua,
       CASE
         WHEN event_id = @e1 THEN 'gieo 89s ROUTED block co nut  -> mong doi ROUTE'
         WHEN event_id = @e2 THEN 'gieo 91s ROUTED block co nut  -> mong doi WAITING'
         WHEN event_id = @e3 THEN 'gieo 10s NO_CAPACITY          -> mong doi MESSAGE'
         WHEN event_id = @e4 THEN 'gieo 10s REJECTED             -> mong doi MESSAGE'
         WHEN event_id = @e5 THEN 'gieo 10s ROUTED block 901     -> mong doi MESSAGE'
       END AS mong_doi
FROM   gieo
ORDER  BY FIELD(event_id, @e1, @e2, @e3, @e4, @e5);

-- Dong duoc endpoint chon la dong MOI NHAT con hieu luc, khong phai dong ROUTED
-- moi nhat. Kiem luon dieu do tren chinh du lieu vua gieo.
SELECT 'Endpoint se chon dong nao' AS kiem_tra, event_id, outcome, block_no,
       ROUND(TIMESTAMPDIFF(MICROSECOND, decided_at, NOW(3)) / 1000000) AS tuoi_giay
FROM   vehicle_routing
WHERE  TIMESTAMPDIFF(MICROSECOND, decided_at, NOW(3)) <= 90000000
ORDER  BY decided_at DESC, event_id DESC
LIMIT  1;

ROLLBACK;

-- Sau ROLLBACK moi thu tro lai nhu cu.
SELECT 'Sau ROLLBACK' AS kiem_tra,
       (SELECT COUNT(*) FROM vehicle_routing) AS tong_dong,
       (SELECT COUNT(*) FROM vehicle_routing WHERE block_no IS NOT NULL) AS dong_co_block;
