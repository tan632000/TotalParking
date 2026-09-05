-- Su kien xe do Camera AI day sang (POST /vehicle).
-- MySQL 8.0.19+. Chay lai nhieu lan an toan.
-- File luu UTF-8 KHONG BOM.

USE total_parking;

-- event_id lam khoa chinh: vua la khoa cua camera, vua tu dong chong trung.
-- Camera gui lai khi khong nhan duoc 2xx, va tai lieu bi mat han phan mo ta
-- chinh sach gui lai, nen chong trung phai nam o tang luu tru chu khong the
-- dua vao gia dinh nao ve so lan thu.
--
-- Cac truong do luong de NULL khi khong xac dinh. Camera gui 0 va "Unknown",
-- ca hai deu la "khong co thong tin" chu khong phai gia tri that; chuyen thanh
-- NULL ngay luc ghi de moi truy van ve sau khong phai nho luat do.
CREATE TABLE IF NOT EXISTS vehicle_event (
    event_id    VARCHAR(48)  NOT NULL,
    received_at DATETIME(3)  NOT NULL,
    camera_ts   DATETIME     NULL,
    make        VARCHAR(64)  NULL,
    model       VARCHAR(64)  NULL,
    year_range  VARCHAR(16)  NULL,
    length_mm   INT          NULL,
    width_mm    INT          NULL,
    height_mm   INT          NULL,
    weight_kg   INT          NULL,
    category    VARCHAR(32)  NULL,
    image_path  VARCHAR(512) NULL,
    raw_body    JSON         NOT NULL,
    PRIMARY KEY (event_id),
    KEY ix_vehicle_event_received (received_at)
) ENGINE = InnoDB;
