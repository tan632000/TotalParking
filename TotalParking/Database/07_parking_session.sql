-- Phien gui xe: nguon su that trung tam ve trang thai gui xe.
-- MySQL 8.0.19+. Chay lai nhieu lan deu an toan. UTF-8 KHONG BOM.
--
-- Thiet ke: docs/parking-session-db-design.md muc 7.

USE total_parking;

-- -------------------------------------------------------------- parking_session
-- Hai cot sinh active_card_id / active_slot_id la co che chong trung: chung chi
-- co gia tri khi phien con mo, nen UNIQUE tren chung dam bao
--   - moi the chi co toi da 1 phien dang mo, TREN TOAN BO BAI;
--   - moi o do chi co toi da 1 phien dang mo.
-- InnoDB cho phep nhieu NULL trung nhau nen phien da dong khong vuong rang buoc.
-- MySQL khong co partial index; day la cach tuong duong.
--
-- Day la QUYEN PHAN QUYET CUOI CUNG. Kiem tra o tang ung dung chi de tra thong
-- bao dep; neu rang buoc nay bi vi pham thi do la loi phan mem.
--
-- Ba cot vi tri duoc dien theo BA THOI DIEM KHAC NHAU:
--   zone_id  luc phan bo, truoc khi xe di chuyen  (SCADA chon)
--   block_id luc khach quet the tai HMI cua block (PLC bao ve)
--   slot_id  luc khach bam chon pallet tren HMI   (PLC bao ve)
-- Vi vay ca ba deu NULL-able, va slot_id NULL khong co nghia la loi.
CREATE TABLE IF NOT EXISTS parking_session (
    session_id        BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,

    card_id           INT UNSIGNED      NULL,
    event_id          VARCHAR(48)       NULL,
    plate             VARCHAR(16)       NULL,

    status            VARCHAR(16)       NOT NULL,

    zone_id           TINYINT UNSIGNED  NULL,
    block_id          SMALLINT UNSIGNED NULL,
    slot_id           INT UNSIGNED      NULL,

    assign_expires_at DATETIME(3)       NULL,
    created_at        DATETIME(3)       NOT NULL,
    updated_at        DATETIME(3)       NOT NULL,
    parked_at         DATETIME(3)       NULL,
    completed_at      DATETIME(3)       NULL,

    error_code        VARCHAR(32)       NULL,
    error_message     VARCHAR(255)      NULL,

    active_card_id INT UNSIGNED GENERATED ALWAYS AS (
        IF(status IN ('ASSIGNED','ENTERING','PARKING','PARKED','RETRIEVING'),
           card_id, NULL)) VIRTUAL,
    active_slot_id INT UNSIGNED GENERATED ALWAYS AS (
        IF(status IN ('ASSIGNED','ENTERING','PARKING','PARKED','RETRIEVING'),
           slot_id, NULL)) VIRTUAL,

    PRIMARY KEY (session_id),
    UNIQUE KEY uq_session_active_card (active_card_id),
    UNIQUE KEY uq_session_active_slot (active_slot_id),
    KEY ix_session_status (status, updated_at),
    KEY ix_session_block  (block_id, status),
    KEY ix_session_zone   (zone_id, status),
    KEY ix_session_card   (card_id, created_at),
    CONSTRAINT fk_session_card  FOREIGN KEY (card_id)  REFERENCES parking_card (card_id),
    CONSTRAINT fk_session_zone  FOREIGN KEY (zone_id)  REFERENCES zone (zone_id),
    CONSTRAINT fk_session_slot  FOREIGN KEY (slot_id)  REFERENCES parking_slot (slot_id),
    CONSTRAINT fk_session_block FOREIGN KEY (block_id) REFERENCES block (block_id),
    CONSTRAINT fk_session_event FOREIGN KEY (event_id) REFERENCES vehicle_event (event_id),
    CONSTRAINT ck_session_status CHECK (status IN
        ('NEW','ASSIGNED','ENTERING','PARKING','PARKED','RETRIEVING',
         'COMPLETED','CANCELLED','ERROR','TIMEOUT'))
) ENGINE = InnoDB;

-- ---------------------------------------------------------------- parking_event
-- Nhat ky moi lan doi trang thai. Append-only, khong bao gio UPDATE.
CREATE TABLE IF NOT EXISTS parking_event (
    parking_event_id BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
    session_id       BIGINT UNSIGNED   NULL,
    block_id         SMALLINT UNSIGNED NULL,
    -- CAMERA | HMI | PLC | SCADA | OPERATOR | SYSTEM
    actor            VARCHAR(16)       NOT NULL,
    actor_ref        VARCHAR(64)       NULL,
    event_type       VARCHAR(32)       NOT NULL,
    from_status      VARCHAR(16)       NULL,
    to_status        VARCHAR(16)       NULL,
    detail           JSON              NULL,
    occurred_at      DATETIME(3)       NOT NULL,
    PRIMARY KEY (parking_event_id),
    KEY ix_parking_event_session  (session_id, occurred_at),
    KEY ix_parking_event_occurred (occurred_at),
    KEY ix_parking_event_block    (block_id, occurred_at),
    CONSTRAINT fk_parking_event_session
        FOREIGN KEY (session_id) REFERENCES parking_session (session_id)
) ENGINE = InnoDB;

-- Tra vi tri xe theo the: quet the o BAT KY block nao cung ra duoc
-- Zone / Block / Pallet.
CREATE OR REPLACE VIEW v_active_session AS
SELECT  s.session_id, s.status,
        c.card_code, c.card_no,
        z.zone_id, z.code AS zone_code,
        b.block_id, b.block_no,
        sl.slot_id, sl.label AS slot_label, sl.tier, sl.col_index,
        s.plate, s.created_at, s.parked_at
FROM    parking_session s
JOIN    parking_card  c  ON c.card_id  = s.card_id
LEFT JOIN parking_slot sl ON sl.slot_id = s.slot_id
LEFT JOIN block       b  ON b.block_id = COALESCE(s.block_id, sl.block_id)
LEFT JOIN zone        z  ON z.zone_id  = COALESCE(s.zone_id, b.zone_id)
WHERE   s.active_card_id IS NOT NULL;

-- O trong va dung duoc. Dau vao cua thuat toan chon o.
CREATE OR REPLACE VIEW v_slot_available AS
SELECT  sl.slot_id, sl.block_id, b.block_no, b.zone_id, b.kind,
        sl.label, sl.tier, sl.col_index,
        COALESCE(sl.max_length_mm, b.bay_length_mm) AS max_length_mm,
        COALESCE(sl.max_width_mm,  b.max_width_mm)  AS max_width_mm,
        COALESCE(sl.max_height_mm, b.max_height_mm) AS max_height_mm,
        z.gate_rank
FROM    parking_slot sl
JOIN    block b ON b.block_id = sl.block_id
JOIN    zone  z ON z.zone_id  = b.zone_id
WHERE   sl.condition_state = 'OK'
  AND   b.is_active = 1
  AND   z.is_active = 1
  AND   sl.slot_id NOT IN (SELECT active_slot_id FROM parking_session
                           WHERE active_slot_id IS NOT NULL);

-- Suc chua theo zone. Chi can du lieu muc BLOCK, khong can bang o da seed xong,
-- nen phan dieu huong toi zone chay duoc truoc khi co du lieu o chi tiet.
CREATE OR REPLACE VIEW v_zone_capacity AS
SELECT  z.zone_id, z.code, z.gate_rank,
        SUM(CASE WHEN b.kind = 'Mechanical' THEN b.slot_count   ELSE 0 END) AS total_mech,
        SUM(CASE WHEN b.kind = 'Mechanical' THEN b.column_count ELSE 0 END) AS total_tier0,
        SUM(CASE WHEN b.kind = 'Ground'     THEN b.slot_count   ELSE 0 END) AS total_ground,
        (SELECT COUNT(*) FROM parking_session s
          WHERE s.active_card_id IS NOT NULL AND s.zone_id = z.zone_id)     AS in_use
FROM    zone z
LEFT JOIN block b ON b.zone_id = z.zone_id AND b.is_active = 1
WHERE   z.is_active = 1
GROUP BY z.zone_id, z.code, z.gate_rank;
