-- Ket qua phan loai cua VehicleClassifier, tach khoi vehicle_event.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.

USE total_parking;

-- Tach bang thay vi them cot vao vehicle_event, vi hai thu co vong doi khac nhau:
-- su kien la du lieu tho khong bao gio doi, con ho so phan loai duoc tinh lai moi
-- khi nguong trong Web.config thay doi hoac so lieu khoang thi cong duoc xac nhan.
CREATE TABLE IF NOT EXISTS vehicle_profile (
    event_id           VARCHAR(48) NOT NULL,
    classified_at      DATETIME(3) NOT NULL,

    rejected           TINYINT(1)  NOT NULL DEFAULT 0,
    reject_reason      VARCHAR(255) NULL,
    requires_manual    TINYINT(1)  NOT NULL DEFAULT 0,
    manual_reason      VARCHAR(255) NULL,

    -- MechanicalL48M | MechanicalL5M | Normal
    lane               VARCHAR(16) NOT NULL,
    -- THUONG | 2200KG | 2600KG, cung bo gia tri voi parking_card.weight_class
    weight_class       VARCHAR(8)  NOT NULL,
    required_pallet_kg INT         NULL,

    estimated_loaded_kg INT        NULL,
    effective_width_mm  INT        NULL,

    PRIMARY KEY (event_id),
    KEY ix_vehicle_profile_class (weight_class, lane),
    CONSTRAINT fk_vehicle_profile_event
        FOREIGN KEY (event_id) REFERENCES vehicle_event (event_id)
        ON DELETE CASCADE
) ENGINE = InnoDB;

-- Xem nhanh: xe vao, ket qua phan loai.
CREATE OR REPLACE VIEW v_vehicle_intake AS
SELECT  e.event_id,
        e.received_at,
        e.make,
        e.model,
        e.length_mm, e.width_mm, e.height_mm, e.weight_kg,
        e.category            AS camera_category,
        p.lane,
        p.weight_class,
        p.required_pallet_kg,
        p.estimated_loaded_kg,
        p.effective_width_mm,
        p.rejected,
        p.reject_reason,
        p.requires_manual,
        p.manual_reason
FROM    vehicle_event e
LEFT JOIN vehicle_profile p ON p.event_id = e.event_id;
