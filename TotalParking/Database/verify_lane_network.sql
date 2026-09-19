-- Doi chieu mang luoi lan duong sau khi chay 37_lane_network.sql.
-- Chi DOC, khong sua gi. UTF-8 KHONG BOM.
--
-- Cach doc: moi truy van tra ve mot dong co cot ket_qua. Dat tat ca deu PASS
-- thi mang luoi dung. Cho nao FAIL thi cot chi_tiet liet ke luon id vi pham,
-- vi dem khong ma khong biet dong nao hong thi khong sua duoc.

USE total_parking;

-- 1) R1.1 - moi nut phai nam trong khung 1594 x 1300 cua plan_map.jpg.
SELECT 'R1.1 nut trong khung 1594x1300' AS kiem_tra,
       COUNT(*)                          AS so_nut_lech_khung,
       IF(COUNT(*) = 0, 'PASS', 'FAIL')  AS ket_qua,
       IFNULL(GROUP_CONCAT(node_id ORDER BY node_id SEPARATOR ','), '-') AS chi_tiet
FROM   lane_node
WHERE  x < 0 OR x > 1594 OR y < 0 OR y > 1300;

-- 2) R1.2 - dung MOT nut duoc danh dau la dau doc.
SELECT 'R1.2 dung mot nut is_entry' AS kiem_tra,
       COUNT(*)                     AS so_nut_is_entry,
       IF(COUNT(*) = 1, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT(node_id, '@', x, ',', y) ORDER BY node_id SEPARATOR ' '), '-') AS chi_tiet
FROM   lane_node
WHERE  is_entry = 1;

-- 3) R1.3 - moi block co map_x phai co dung mot nut den.
SELECT 'R1.3 block co map_x deu co nut den' AS kiem_tra,
       COUNT(*)                              AS so_block_thieu_nut_den,
       IF(COUNT(*) = 0, 'PASS', 'FAIL')      AS ket_qua,
       IFNULL(GROUP_CONCAT(block_no ORDER BY block_no SEPARATOR ','), '-') AS chi_tiet
FROM   block
WHERE  map_x IS NOT NULL AND lane_node_id IS NULL;

-- 4) R1.3 - nut den phai tro toi mot nut co that. Khoa ngoai da chan roi,
--    nhung van doi chieu de script nay du dung mot minh khi soat loi.
SELECT 'R1.3 nut den deu ton tai' AS kiem_tra,
       COUNT(*)                   AS so_tham_chieu_hong,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(b.block_no ORDER BY b.block_no SEPARATOR ','), '-') AS chi_tiet
FROM   block b
LEFT   JOIN lane_node n ON n.node_id = b.lane_node_id
WHERE  b.lane_node_id IS NOT NULL AND n.node_id IS NULL;

-- 4b) Nut cong vao khong duoc kiem luon nut den cua mot block. Neu kiem, duong
--     di toi block do chi co mot diem va man hinh tai xe phai bo ve theo C1.
SELECT 'Nut cong khong kiem nut den' AS kiem_tra,
       COUNT(*)                      AS so_block_dung_nut_cong,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(b.block_no ORDER BY b.block_no SEPARATOR ','), '-') AS chi_tiet
FROM   block b
JOIN   lane_node n ON n.node_id = b.lane_node_id
WHERE  n.is_entry = 1;

-- 5) Canh phai la vo huong luu mot lan: khong co canh nguoc, khong co tu noi.
SELECT 'Canh vo huong luu mot lan' AS kiem_tra,
       COUNT(*)                    AS so_canh_sai,
       IF(COUNT(*) = 0, 'PASS', 'FAIL') AS ket_qua,
       IFNULL(GROUP_CONCAT(CONCAT(e.from_node, '-', e.to_node) SEPARATOR ' '), '-') AS chi_tiet
FROM   lane_edge e
LEFT   JOIN lane_edge r ON r.from_node = e.to_node AND r.to_node = e.from_node
WHERE  e.from_node = e.to_node OR r.from_node IS NOT NULL;

-- 6) R1.4 - tong so hang. Chay 37_lane_network.sql lan hai roi chay lai file
--    nay: ba con so duoi day phai giong het lan truoc.
SELECT 'R1.4 tong so hang' AS kiem_tra,
       (SELECT COUNT(*) FROM lane_node)                                  AS so_nut,
       (SELECT COUNT(*) FROM lane_edge)                                  AS so_canh,
       (SELECT COUNT(*) FROM block WHERE lane_node_id IS NOT NULL)       AS so_block_co_nut_den,
       (SELECT COUNT(*) FROM block WHERE map_x IS NOT NULL)              AS so_block_co_map_x;
