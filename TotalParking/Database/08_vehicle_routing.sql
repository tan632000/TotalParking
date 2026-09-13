-- Quyet dinh dieu huong xe vao zone nao, sau khi Camera AI phat hien xe.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- Thiet ke: docs/parking-session-db-design.md muc 5.5.

USE total_parking;

-- Tach bang thay vi them cot vao vehicle_event, dung ly do da dung cho
-- vehicle_profile: su kien la du lieu tho khong bao gio doi, con quyet dinh
-- dieu huong duoc tinh lai moi khi suc chua thay doi hoac gate_rank duoc chot.
--
-- KHONG tao parking_session o day. Phien gui xe gan voi THE RFID, con o day
-- chua co the nao — xe moi chi di qua camera. Neu tao phien tu day thi khi
-- khach quet the tai HMI se sinh phien thu hai cho cung chiec xe, va cot sinh
-- active_card_id khong the noi hai phien do lai voi nhau (phien tu camera co
-- card_id NULL). Giai doan nay bang duoi day chi phuc vu hien thi.
CREATE TABLE IF NOT EXISTS vehicle_routing (
    event_id   VARCHAR(48)      NOT NULL,
    decided_at DATETIME(3)      NOT NULL,
    -- NULL khi outcome khac ROUTED.
    zone_id    TINYINT UNSIGNED NULL,
    -- ROUTED       chon duoc zone
    -- NO_CAPACITY  khong zone nao con cho phu hop
    -- REJECTED     xe vuot gioi han vat ly cua bai (VehicleClassifier)
    -- MANUAL       thieu du lieu do, can nhan vien quyet dinh
    outcome    VARCHAR(16)      NOT NULL,
    reason     VARCHAR(255)     NULL,
    PRIMARY KEY (event_id),
    KEY ix_vehicle_routing_decided (decided_at),
    KEY ix_vehicle_routing_zone (zone_id, decided_at),
    CONSTRAINT fk_vehicle_routing_event
        FOREIGN KEY (event_id) REFERENCES vehicle_event (event_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_vehicle_routing_zone
        FOREIGN KEY (zone_id) REFERENCES zone (zone_id),
    CONSTRAINT ck_vehicle_routing_outcome
        CHECK (outcome IN ('ROUTED','NO_CAPACITY','REJECTED','MANUAL'))
) ENGINE = InnoDB;

-- Xem nhanh: xe vao, phan loai, va da duoc chi sang zone nao.
CREATE OR REPLACE VIEW v_vehicle_routing AS
SELECT  e.event_id,
        e.received_at,
        e.make, e.model,
        e.length_mm, e.width_mm, e.height_mm, e.weight_kg,
        e.category      AS camera_category,
        p.lane,
        p.weight_class,
        p.rejected,
        p.requires_manual,
        r.decided_at,
        r.outcome,
        r.reason,
        r.zone_id,
        z.code          AS zone_code
FROM    vehicle_event e
LEFT JOIN vehicle_profile p ON p.event_id = e.event_id
LEFT JOIN vehicle_routing r ON r.event_id = e.event_id
LEFT JOIN zone           z ON z.zone_id   = r.zone_id;
