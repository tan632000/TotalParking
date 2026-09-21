-- Doi chieu: mot quyet dinh dieu huong se ra trang thai nao tren man hinh tai xe.
-- Gieo du lieu roi ROLLBACK — khong dong nao con lai sau khi chay. UTF-8 KHONG BOM.
--
-- LUAT DA DOI: chi dan KHONG tu het han nua.
--
-- Truoc day quyet dinh chi hien trong 90 giay, qua han thi man hinh ve trang thai
-- cho. Nguoi van hanh muon chi dan o lai cho toi khi co xe ke tiep duoc quet. Nen
-- luat phan giai bay gio la:
--
--   khong co dong nao trong bang                 -> WAITING
--   dong MOI NHAT: ROUTED, co block, ve duoc     -> ROUTE   (du cu bao lau)
--   moi truong hop con lai                       -> MESSAGE
--
-- Cua so 90 giay VAN CON o mot cho khac: BlockAllocator dung no de tru cac suat
-- vua phat di. Dung nham hai thu do voi nhau.

USE total_parking;

SET @e1 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 0);
SET @e2 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 1);
SET @e3 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 2);
SET @e4 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 3);
SET @e5 := (SELECT event_id FROM vehicle_routing ORDER BY event_id LIMIT 1 OFFSET 4);

SET @co_nut    := 112;   -- block co nut den va toi duoc
SET @khong_nut := 901;   -- bai do nen, khong co nut den

START TRANSACTION;

-- Gieo hai dong ROUTED o hai ben moc 90 giay cu. Ca hai bay gio deu phai ra ROUTE:
-- do chinh la thay doi can kiem.
UPDATE vehicle_routing SET outcome='ROUTED', block_no=@co_nut,
       decided_at = NOW(3) - INTERVAL 89 SECOND   WHERE event_id = @e1;
UPDATE vehicle_routing SET outcome='ROUTED', block_no=@co_nut,
       decided_at = NOW(3) - INTERVAL 1 HOUR      WHERE event_id = @e2;
UPDATE vehicle_routing SET outcome='NO_CAPACITY', block_no=NULL,
       decided_at = NOW(3) - INTERVAL 10 SECOND   WHERE event_id = @e3;
UPDATE vehicle_routing SET outcome='REJECTED', block_no=NULL,
       decided_at = NOW(3) - INTERVAL 10 SECOND   WHERE event_id = @e4;
UPDATE vehicle_routing SET outcome='ROUTED', block_no=@khong_nut,
       decided_at = NOW(3) - INTERVAL 10 SECOND   WHERE event_id = @e5;

WITH RECURSIVE hai_chieu AS (
    SELECT from_node AS a, to_node AS b FROM lane_edge
    UNION ALL
    SELECT to_node,   from_node        FROM lane_edge),
toi_duoc AS (
    SELECT node_id FROM lane_node WHERE is_entry = 1
    UNION
    SELECT u.b FROM toi_duoc t JOIN hai_chieu u ON u.a = t.node_id),
gieo AS (
    SELECT r.event_id, r.outcome, r.block_no,
           ROUND(TIMESTAMPDIFF(MICROSECOND, r.decided_at, NOW(3)) / 1000000) AS tuoi_giay,
           (b.lane_node_id IS NOT NULL
            AND b.lane_node_id IN (SELECT node_id FROM toi_duoc)) AS ve_duoc
    FROM   vehicle_routing r
    LEFT   JOIN block b ON b.block_no = r.block_no
    WHERE  r.event_id IN (@e1, @e2, @e3, @e4, @e5))
SELECT event_id, outcome, block_no, tuoi_giay,
       CASE WHEN outcome = 'ROUTED' AND block_no IS NOT NULL AND ve_duoc
            THEN 'ROUTE' ELSE 'MESSAGE' END AS trang_thai,
       CASE
         WHEN event_id IN (@e1, @e2) THEN IF(outcome='ROUTED' AND ve_duoc, 'PASS', 'FAIL')
         WHEN event_id IN (@e3, @e4) THEN IF(outcome <> 'ROUTED', 'PASS', 'FAIL')
         WHEN event_id = @e5         THEN IF(ve_duoc = 0, 'PASS', 'FAIL')
       END AS ket_qua,
       CASE
         WHEN event_id = @e1 THEN 'gieo  89s ROUTED block co nut -> mong doi ROUTE'
         WHEN event_id = @e2 THEN 'gieo 1 GIO ROUTED block co nut -> mong doi ROUTE (truoc day la WAITING)'
         WHEN event_id = @e3 THEN 'gieo  10s NO_CAPACITY          -> mong doi MESSAGE'
         WHEN event_id = @e4 THEN 'gieo  10s REJECTED             -> mong doi MESSAGE'
         WHEN event_id = @e5 THEN 'gieo  10s ROUTED block 901     -> mong doi MESSAGE'
       END AS mong_doi
FROM   gieo
ORDER  BY FIELD(event_id, @e1, @e2, @e3, @e4, @e5);

-- Dong duoc endpoint chon la dong MOI NHAT, bat ke no cu bao nhieu.
SELECT 'Endpoint se chon dong nao' AS kiem_tra, event_id, outcome, block_no,
       ROUND(TIMESTAMPDIFF(MICROSECOND, decided_at, NOW(3)) / 1000000) AS tuoi_giay
FROM   vehicle_routing
ORDER  BY decided_at DESC, event_id DESC
LIMIT  1;

-- Chi dan cu KHONG bi het han: dong cu nhat trong bang van la mot ung vien hop le,
-- neu tinh co no la dong moi nhat. Kiem bang cach dem so dong qua 90 giay ma van
-- co the hien duoc — truoc day con so nay luon phai la 0.
SELECT 'Dong qua 90 giay van hien duoc' AS kiem_tra,
       COUNT(*) AS so_dong,
       IF(COUNT(*) > 0, 'PASS', 'FAIL') AS ket_qua,
       'luat moi: tuoi khong con la dieu kien' AS ghi_chu
FROM   vehicle_routing
WHERE  TIMESTAMPDIFF(MICROSECOND, decided_at, NOW(3)) > 90000000;

ROLLBACK;

SELECT 'Sau ROLLBACK' AS kiem_tra,
       (SELECT COUNT(*) FROM vehicle_routing) AS tong_dong,
       (SELECT COUNT(*) FROM vehicle_routing WHERE block_no IS NOT NULL) AS dong_co_block;
