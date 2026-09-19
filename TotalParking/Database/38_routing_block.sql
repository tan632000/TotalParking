-- Them block dich vao quyet dinh dieu huong: vehicle_routing.block_no.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO CAN BANG NAY =======================
-- Truoc bang nay, quyet dinh dieu huong chi noi den ZONE. Block dich duoc
-- trang Routing.cshtml tu chon lai trong JavaScript o moi lan poll. Hau qua:
-- man hinh, trang van hanh va database co the noi ba dieu khac nhau, va block
-- hien thi doi ngay truoc mat tai xe khi xe khac vao bai.
-- Chon o server, luu mot lan, moi noi doc lai cung mot con so.
--
-- ======================= VI SAO DUNG TRIGGER MA KHONG DUNG CHECK =======================
-- C2 mo ta rang buoc nay bang CHECK constraint. Khong lam duoc tren du lieu
-- dang co: bang hien co 533 dong outcome = 'ROUTED' voi block_no NULL, la
-- quyet dinh ghi TRUOC khi co cot nay. MySQL kiem tra CHECK tren toan bo dong
-- cu khi ALTER, nen lenh do that bai ngay:
--
--     ERROR 3819 (HY000): Check constraint 'ck_vr' is violated.
--
-- C2 cung yeu cau "existing rows keep a null block_no". Hai yeu cau do khong
-- the cung dung. Trigger BEFORE INSERT/UPDATE giai duoc ca hai: no chi soi dong
-- MOI, con lich su nam yen. Hanh vi ma R2.4 doi hoi duoc giu nguyen ven.

USE total_parking;

-- ------------------------------------------------------- cot block_no
-- MySQL khong co ADD COLUMN IF NOT EXISTS -> soi information_schema truoc,
-- giong 34_block_map_xy.sql, de file nay chay lai duoc nhieu lan.
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'vehicle_routing'
               AND COLUMN_NAME = 'block_no');
SET @sql := IF(@has = 0,
    'ALTER TABLE vehicle_routing
        ADD COLUMN block_no SMALLINT UNSIGNED NULL
            COMMENT ''block dich, chi khac NULL khi outcome = ROUTED'',
        ADD CONSTRAINT fk_vehicle_routing_block
            FOREIGN KEY (block_no) REFERENCES block (block_no)',
    'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- --------------------------------------------- cot occupancy_verified
-- D6: block khong co ban doc PLC trong 5 phut van duoc chon, nhung phai duoc
-- DAN NHAN. Nhan nay luu cung dong quyet dinh chu khong tinh lai luc doc: neu
-- tinh lai, nhan se lat qua lat lai ngay truoc mat tai xe moi khi PLC tra ve
-- mot ban doc moi, trong khi quyet dinh thi khong he doi.
SET @has := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'vehicle_routing'
               AND COLUMN_NAME = 'occupancy_verified');
SET @sql := IF(@has = 0,
    'ALTER TABLE vehicle_routing
        ADD COLUMN occupancy_verified TINYINT(1) NOT NULL DEFAULT 0
            COMMENT ''1 = block chon duoc PLC bao cao trong 5 phut''',
    'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- --------------------------------------------- chi muc cho phep tru D9
-- D9 tru suc chua theo cac block da phat di trong cua so 90 giay. Truy van do
-- loc theo block_no va decided_at va chay tren duong ingest, nen can chi muc.
SET @has := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'vehicle_routing'
               AND INDEX_NAME = 'ix_vehicle_routing_block');
SET @sql := IF(@has = 0,
    'ALTER TABLE vehicle_routing ADD KEY ix_vehicle_routing_block (block_no, decided_at)',
    'DO 0');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ------------------------------------------------------------ trigger
-- DROP roi CREATE: CREATE TRIGGER khong co dang IF NOT EXISTS, nen day la
-- cach duy nhat de file chay lai duoc ma khong bao trung ten.
DROP TRIGGER IF EXISTS tg_vehicle_routing_block_ins;
DROP TRIGGER IF EXISTS tg_vehicle_routing_block_upd;

DELIMITER $$

CREATE TRIGGER tg_vehicle_routing_block_ins
BEFORE INSERT ON vehicle_routing FOR EACH ROW
BEGIN
    IF NEW.outcome = 'ROUTED' AND NEW.block_no IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'vehicle_routing: outcome ROUTED bat buoc phai co block_no';
    END IF;
    IF NEW.outcome <> 'ROUTED' AND NEW.block_no IS NOT NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'vehicle_routing: outcome khac ROUTED bat buoc block_no phai NULL';
    END IF;
END$$

CREATE TRIGGER tg_vehicle_routing_block_upd
BEFORE UPDATE ON vehicle_routing FOR EACH ROW
BEGIN
    IF NEW.outcome = 'ROUTED' AND NEW.block_no IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'vehicle_routing: outcome ROUTED bat buoc phai co block_no';
    END IF;
    IF NEW.outcome <> 'ROUTED' AND NEW.block_no IS NOT NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'vehicle_routing: outcome khac ROUTED bat buoc block_no phai NULL';
    END IF;
END$$

DELIMITER ;

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS so_cot_moi FROM information_schema.COLUMNS
WHERE  TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'vehicle_routing'
  AND  COLUMN_NAME IN ('block_no', 'occupancy_verified');

SELECT COUNT(*) AS so_trigger FROM information_schema.TRIGGERS
WHERE  TRIGGER_SCHEMA = DATABASE() AND EVENT_OBJECT_TABLE = 'vehicle_routing';

SELECT COUNT(*) AS so_dong_lich_su_giu_nguyen
FROM   vehicle_routing WHERE outcome = 'ROUTED' AND block_no IS NULL;
