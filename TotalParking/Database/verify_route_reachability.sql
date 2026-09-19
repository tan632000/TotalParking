-- Doi chieu: moi block deu co duong lan noi tu dau doc toi noi.
-- Chi DOC, khong sua gi. UTF-8 KHONG BOM.
--
-- Bien dich: mot block "toi duoc" khi co mot day canh noi nut is_entry toi nut
-- den cua no. Canh trong lane_edge la VO HUONG va chi luu mot lan cho moi cap,
-- nen phai mo ra hai chieu truoc khi lan, dung nhu LaneNetwork.cs lam. Quen
-- buoc do thi mot nua so huong di se bien mat va ket qua se bao sai.

USE total_parking;

-- 1) Nut dau doc phai co, va chi mot.
SELECT 'Co dung mot nut dau doc' AS kiem_tra,
       COUNT(*) AS so_nut_is_entry,
       IF(COUNT(*) = 1, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(node_id), '-') AS chi_tiet
FROM   lane_node WHERE is_entry = 1;

-- 2) Block co toa do ma khong co nut den thi khong bao gio ve duoc duong.
SELECT 'Block co map_x deu co nut den' AS kiem_tra,
       COUNT(*) AS so_block_thieu_nut_den,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(block_no ORDER BY block_no SEPARATOR ','), '-') AS chi_tiet
FROM   block WHERE map_x IS NOT NULL AND lane_node_id IS NULL;

-- 3) R3.3 - lan de quy tu nut dau doc, liet ke block KHONG toi duoc.
WITH RECURSIVE hai_chieu AS (
    SELECT from_node AS a, to_node AS b FROM lane_edge
    UNION ALL
    SELECT to_node,   from_node        FROM lane_edge),
toi_duoc AS (
    SELECT node_id FROM lane_node WHERE is_entry = 1
    UNION
    SELECT e.b FROM toi_duoc t JOIN hai_chieu e ON e.a = t.node_id)
SELECT 'R3.3 moi block deu toi duoc tu dau doc' AS kiem_tra,
       COUNT(*) AS so_block_khong_toi_duoc,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT(b.block_no, '@node', b.lane_node_id)
                           ORDER BY b.block_no SEPARATOR ' '), '-') AS chi_tiet
FROM   block b
WHERE  b.lane_node_id IS NOT NULL
  AND  b.lane_node_id NOT IN (SELECT node_id FROM toi_duoc);

-- 4) Nut den khong duoc trung nut dau doc: duong di khi do chi co MOT diem, va
--    hop dong C1 doi it nhat hai diem moi duoc ve.
SELECT 'Nut den khong trung nut dau doc' AS kiem_tra,
       COUNT(*) AS so_block_trung,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(b.block_no ORDER BY b.block_no SEPARATOR ','), '-') AS chi_tiet
FROM   block b JOIN lane_node n ON n.node_id = b.lane_node_id
WHERE  n.is_entry = 1;

-- 5) Doi chieu: bao nhieu nut trong tong so lan toi duoc.
WITH RECURSIVE hai_chieu AS (
    SELECT from_node AS a, to_node AS b FROM lane_edge
    UNION ALL
    SELECT to_node,   from_node        FROM lane_edge),
toi_duoc AS (
    SELECT node_id FROM lane_node WHERE is_entry = 1
    UNION
    SELECT e.b FROM toi_duoc t JOIN hai_chieu e ON e.a = t.node_id)
SELECT 'Doi chieu' AS kiem_tra,
       (SELECT COUNT(*) FROM toi_duoc)   AS nut_toi_duoc,
       (SELECT COUNT(*) FROM lane_node)  AS tong_nut,
       (SELECT COUNT(*) FROM lane_edge)  AS tong_canh,
       (SELECT COUNT(*) FROM block WHERE lane_node_id IS NOT NULL) AS block_co_nut_den;
