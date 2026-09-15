-- Xoa toan bo du lieu block gia.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- LY DO: bang LED dau ham ngoai hien truong da hien "36 cho trong" cho tai xe
-- that. Con so do den tu 99_demo_seed.sql — du lieu bia de mo phong. Mot tam
-- bang chi duong noi doi thi te hon mot tam bang toi.
--
-- Moi dong trong bang `block` deu la du lieu gia, khong chi rieng demo seed:
--   block_no 901-906, 951-956   99_demo_seed.sql, ghi ro la bia
--   block_no 1, 2               05_parking_topology.sql, "chon kieu 10 SPACES
--                               vi day la kieu pho bien nhat doc duoc tren ban ve"
-- Ca hai deu la phong doan. Xoa het.
--
-- Du lieu that phai lay tu file CAD goc: 112 block, so o 3/5/6/10 khac nhau
-- tung block. Xem docs/parking-session-db-design.md muc 12.1.
--
-- KHONG xoa `zone`: 6 polygon do la toa do that, do vien mau tu ban ve trong
-- Images/zones_map.jpeg. Chi xoa gate_rank vi con so do la bia.

USE total_parking;

-- parking_slot truoc: fk_slot_block khong co ON DELETE CASCADE.
DELETE FROM parking_slot;
DELETE FROM block;

-- gate_rank 1..6 do 99_demo_seed.sql gan bua. Ve 0 = chua khao sat.
-- ZoneRouter se roi ve xep hang theo ti le trong, thay vi theo mot thu tu
-- gan/xa khong ai xac nhan.
UPDATE zone SET gate_rank = 0;

-- --------------------------------------------------- them outcome NO_DATA
-- Truoc day khi khong chon duoc zone thi luon tra NO_CAPACITY = "het cho".
-- Nhung "het cho" va "chua co du lieu suc chua" la hai chuyen khac han:
-- cai dau la su that ve bai xe, cai sau la su that ve HE THONG.
--
-- Gop hai cai lam mot chinh la co che da dan toi su co vua roi: he thong
-- khong biet gi ma van tra ve mot con so nhu the no biet.
SET @has_ck := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                WHERE CONSTRAINT_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'vehicle_routing'
                  AND CONSTRAINT_NAME = 'ck_vehicle_routing_outcome');
SET @sql := IF(@has_ck > 0,
    'ALTER TABLE vehicle_routing DROP CHECK ck_vehicle_routing_outcome',
    'DO 0');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

ALTER TABLE vehicle_routing
    ADD CONSTRAINT ck_vehicle_routing_outcome
    CHECK (outcome IN ('ROUTED','NO_CAPACITY','NO_DATA','REJECTED','MANUAL'));

-- Kiem tra: ca ba phai ve 0. Neu khong, con du lieu gia o dau do.
SELECT (SELECT COUNT(*) FROM block)        AS blocks_con_lai,
       (SELECT COUNT(*) FROM parking_slot) AS o_con_lai,
       (SELECT COALESCE(SUM(gate_rank),0) FROM zone) AS tong_gate_rank;
