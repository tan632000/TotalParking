-- Bo cuc bai xe: mat bang, zone, block, o do.
-- MySQL 8.0.19+. Chay lai nhieu lan deu an toan.
-- File luu UTF-8 KHONG BOM: client mysql bao loi cu phap neu gap BOM.
--
-- Thiet ke: docs/parking-session-db-design.md muc 5.
-- Du lieu that cua 112 block phai lay tu file CAD goc; phan seed o cuoi file
-- nay chi la 2 block thi diem.

USE total_parking;

-- ------------------------------------------------------------------ floor_plan
-- Khai bao he toa do cua mot ban ve. Moi toa do trong zone/block deu thuoc ve
-- mot dong o day, khong bao gio la toa do "chung chung".
--
-- mm_per_unit NULL = chua biet ti le that, toa do chi dung de ve va bat click.
CREATE TABLE IF NOT EXISTS floor_plan (
    floor_plan_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    code          VARCHAR(32)   NOT NULL,
    image_path    VARCHAR(255)  NOT NULL,
    width_units   INT           NOT NULL,
    height_units  INT           NOT NULL,
    mm_per_unit   DECIMAL(10,4) NULL,
    source_note   VARCHAR(255)  NULL,
    PRIMARY KEY (floor_plan_id),
    UNIQUE KEY uq_floor_plan_code (code)
) ENGINE = InnoDB;

INSERT INTO floor_plan (code, image_path, width_units, height_units, mm_per_unit, source_note)
VALUES ('B1', '~/Images/zones_map.jpeg', 1016, 781, NULL,
        'Toa do anh, do vien mau bang script trong scratch/. Chua co ti le mm.') AS new
ON DUPLICATE KEY UPDATE
    image_path = new.image_path, width_units = new.width_units,
    height_units = new.height_units, source_note = new.source_note;

-- ------------------------------------------------------------------------ zone
-- polygon: chuoi diem SVG "x,y x,y ..." trong he toa do cua floor_plan.
-- Giu nguyen dinh dang SVG de FloorPlan.cshtml dung thang, khoi phai chuyen doi.
--
-- gate_rank: thu tu gan -> xa tinh tu cong vao (bai chi co 1 cong). Day la tieu
-- chi xep hang zone khi chua co do thi lan xe. Xem muc 5.5 cua tai lieu thiet ke.
CREATE TABLE IF NOT EXISTS zone (
    zone_id       TINYINT UNSIGNED NOT NULL,
    floor_plan_id TINYINT UNSIGNED NOT NULL,
    code          VARCHAR(8)   NOT NULL,
    name          VARCHAR(64)  NOT NULL,
    polygon       VARCHAR(512) NULL,
    label_x       SMALLINT     NULL,
    label_y       SMALLINT     NULL,
    color_hex     CHAR(7)      NULL,
    gate_rank     TINYINT UNSIGNED NOT NULL DEFAULT 0,
    is_active     TINYINT(1)   NOT NULL DEFAULT 1,
    PRIMARY KEY (zone_id),
    UNIQUE KEY uq_zone_code (code),
    CONSTRAINT fk_zone_floor_plan
        FOREIGN KEY (floor_plan_id) REFERENCES floor_plan (floor_plan_id)
) ENGINE = InnoDB;

-- Polygon lay dung tu Views/Home/FloorPlan.cshtml de giao dien va DB khong lech.
-- gate_rank de 0 het: 6 con so nay chua ai xac nhan, xem muc 12.10 tai lieu thiet ke.
INSERT INTO zone (zone_id, floor_plan_id, code, name, polygon, label_x, label_y, color_hex, gate_rank)
SELECT z.zone_id, f.floor_plan_id, z.code, z.name, z.polygon, z.label_x, z.label_y, z.color_hex, z.gate_rank
FROM (
    SELECT 1 AS zone_id, 'Z1' AS code, 'Zone 1' AS name,
           '602,410 923,410 923,582 688,748 573,630 573,510 602,510' AS polygon,
           730 AS label_x, 510 AS label_y, '#eab308' AS color_hex, 0 AS gate_rank
    UNION ALL SELECT 2, 'Z2', 'Zone 2', '308,390 602,390 602,510 573,510 573,630', 420, 470, '#94a3b8', 0
    UNION ALL SELECT 3, 'Z3', 'Zone 3', '57,152 185,46 343,45 308,248 278,293 308,390', 210, 240, '#f97316', 0
    UNION ALL SELECT 4, 'Z4', 'Zone 4', '343,45 526,45 526,390 412,390 308,390 278,293 308,248', 430, 230, '#a855f7', 0
    UNION ALL SELECT 5, 'Z5', 'Zone 5', '526,45 923,46 923,235 718,223 632,198 526,211', 700, 120, '#06b6d4', 0
    UNION ALL SELECT 6, 'Z6', 'Zone 6', '526,211 632,198 718,223 923,235 923,410 602,410 602,380 573,380', 730, 290, '#22c55e', 0
) z
JOIN floor_plan f ON f.code = 'B1'
ON DUPLICATE KEY UPDATE
    polygon = z.polygon, label_x = z.label_x, label_y = z.label_y,
    color_hex = z.color_hex, name = z.name;

-- ----------------------------------------------------------------------- block
-- kind: Mechanical = block pallet co khi (nhan "BLOCK n SPACES-xxxxL" tren ban ve),
--       Ground     = cum cho do nen (nhan "P 1 LOTS").
--
-- block_no la SO IN TREN BAN VE (1..112), khong phai ma tu dat. Dung no lam ma
-- doi ngoai: nhan vien, HMI va ban ve deu goi block bang so nay.
--
-- bay_length_mm lay tu hau to nhan: "-5000L" -> 5000. Day la chieu dai khoang,
-- tuc gioi han chieu dai xe.
--
-- Luoi co cau: slot_count = tier_count * column_count voi block co khi.
-- tier 0 la tang duoi cung; xe hang 2600KG chi vao duoc tier 0.
CREATE TABLE IF NOT EXISTS block (
    block_id      SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    zone_id       TINYINT UNSIGNED  NOT NULL,
    block_no      SMALLINT UNSIGNED NOT NULL,
    kind          VARCHAR(16)       NOT NULL,
    slot_count    TINYINT UNSIGNED  NOT NULL,
    bay_length_mm INT               NULL,
    max_width_mm  INT               NULL,
    max_height_mm INT               NULL,
    tier_count    TINYINT UNSIGNED  NULL,
    column_count  TINYINT UNSIGNED  NULL,
    origin_x      SMALLINT          NULL,
    origin_y      SMALLINT          NULL,
    width_units   SMALLINT          NULL,
    height_units  SMALLINT          NULL,
    led_panel_id  VARCHAR(32)       NULL,
    is_active     TINYINT(1)        NOT NULL DEFAULT 1,
    created_at    DATETIME          NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (block_id),
    UNIQUE KEY uq_block_no (block_no),
    KEY ix_block_zone (zone_id, is_active),
    KEY ix_block_bay  (kind, bay_length_mm, is_active),
    CONSTRAINT fk_block_zone FOREIGN KEY (zone_id) REFERENCES zone (zone_id),
    CONSTRAINT ck_block_kind CHECK (kind IN ('Mechanical','Ground'))
) ENGINE = InnoDB;

-- ---------------------------------------------------------------- parking_slot
-- Mot o do (pallet). tier = 0 la tang duoi cung, col 0 la cot ngoai cung ben
-- trai theo huong nhin cua ban ve.
--
-- KHONG co cot tai trong: hang the 2200KG / 2600KG chi noi xe do duoc o tang
-- tren hay tang duoi, nen tier da la can cu day du.
CREATE TABLE IF NOT EXISTS parking_slot (
    slot_id         INT UNSIGNED      NOT NULL AUTO_INCREMENT,
    block_id        SMALLINT UNSIGNED NOT NULL,
    slot_index      TINYINT UNSIGNED  NOT NULL,
    label           VARCHAR(8)        NOT NULL,
    tier            TINYINT UNSIGNED  NOT NULL DEFAULT 0,
    col_index       TINYINT UNSIGNED  NOT NULL DEFAULT 0,
    max_length_mm   INT               NULL,
    max_width_mm    INT               NULL,
    max_height_mm   INT               NULL,
    -- Trang thai co hoc cua o, doc lap voi viec o co xe hay khong.
    condition_state VARCHAR(16)       NOT NULL DEFAULT 'OK',
    cycle_count     INT UNSIGNED      NOT NULL DEFAULT 0,
    PRIMARY KEY (slot_id),
    UNIQUE KEY uq_slot_block_index (block_id, slot_index),
    UNIQUE KEY uq_slot_block_grid  (block_id, tier, col_index),
    KEY ix_slot_capacity (block_id, condition_state, tier),
    CONSTRAINT fk_slot_block FOREIGN KEY (block_id) REFERENCES block (block_id),
    CONSTRAINT ck_slot_condition
        CHECK (condition_state IN ('OK','MAINTENANCE','FAULT','DISABLED'))
) ENGINE = InnoDB;

-- ------------------------------------------------------------- seed thi diem
-- HAI block cho hai PLC dang co. Day KHONG phai du lieu that cua bai:
-- 112 block that phai sinh tu file CAD, xem muc 12.1 cua tai lieu thiet ke.
-- Block 1 va 2 chon kieu 10 SPACES-5000L (2 tang x 5 cot) vi day la kieu
-- pho bien nhat doc duoc tren ban ve.
INSERT INTO block (zone_id, block_no, kind, slot_count, bay_length_mm, tier_count, column_count)
VALUES
    (1, 1, 'Mechanical', 10, 5000, 2, 5),
    (1, 2, 'Mechanical', 10, 5000, 2, 5) AS new
ON DUPLICATE KEY UPDATE
    zone_id = new.zone_id, kind = new.kind, slot_count = new.slot_count,
    bay_length_mm = new.bay_length_mm, tier_count = new.tier_count,
    column_count = new.column_count;

-- Sinh o cho hai block tren: tier 0..1 x col 0..4.
-- label dang P01..P10, danh so theo tang duoi truoc.
INSERT INTO parking_slot (block_id, slot_index, label, tier, col_index)
SELECT b.block_id,
       t.n * b.column_count + c.n                              AS slot_index,
       CONCAT('P', LPAD(t.n * b.column_count + c.n + 1, 2, '0')) AS label,
       t.n, c.n
FROM   block b
JOIN   (SELECT 0 AS n UNION ALL SELECT 1) t
JOIN   (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2
        UNION ALL SELECT 3 UNION ALL SELECT 4) c
WHERE  b.block_no IN (1, 2)
  AND  t.n < b.tier_count
  AND  c.n < b.column_count
-- Chay lai khong doi gi: o da co thi giu nguyen ke ca cycle_count va
-- condition_state. Khong dung VALUES(...) vi ham do da deprecated tu 8.0.20.
ON DUPLICATE KEY UPDATE slot_id = slot_id;
