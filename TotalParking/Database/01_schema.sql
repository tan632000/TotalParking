-- Schema danh sach the xe. MySQL 8.0.19+ (can cu phap row alias o ON DUPLICATE KEY).
-- Nguon du lieu: docs/dsthe.xls. Chay lai nhieu lan deu an toan.
-- File luu UTF-8 KHONG BOM: client mysql bao loi cu phap neu gap BOM.

CREATE DATABASE IF NOT EXISTS total_parking
    CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;

USE total_parking;

-- ---------------------------------------------------------------- loai khach
CREATE TABLE IF NOT EXISTS customer_type (
    customer_type_id TINYINT UNSIGNED NOT NULL,
    code             VARCHAR(8)  NOT NULL,
    name             VARCHAR(64) NOT NULL,
    PRIMARY KEY (customer_type_id),
    UNIQUE KEY uq_customer_type_code (code)
) ENGINE = InnoDB;

INSERT INTO customer_type (customer_type_id, code, name) VALUES
    (1, 'VANG', 'Xe vãng lai'),
    (2, 'XT',   'Xe tháng'),
    (3, 'GHI',  'Nhóm OTO GHI') AS new
ON DUPLICATE KEY UPDATE code = new.code, name = new.name;

-- ----------------------------------------------------------------- hang tai
-- max_weight_kg NULL = khong dung duoc pallet co khi, phai do nen ("do thuong").
-- Day chinh la con so PLC can de biet the nao thuoc 2200 kg, the nao 2600 kg.
CREATE TABLE IF NOT EXISTS weight_class (
    weight_class_id TINYINT UNSIGNED NOT NULL,
    code            VARCHAR(8)  NOT NULL,
    max_weight_kg   INT         NULL,
    name            VARCHAR(64) NOT NULL,
    PRIMARY KEY (weight_class_id),
    UNIQUE KEY uq_weight_class_code (code)
) ENGINE = InnoDB;

INSERT INTO weight_class (weight_class_id, code, max_weight_kg, name) VALUES
    (0, 'THUONG', NULL, 'Quá tải — đỗ thường, không dùng pallet cơ khí'),
    (1, '2200KG', 2200, 'Pallet tải trọng 2200 kg'),
    (2, '2600KG', 2600, 'Pallet tải trọng 2600 kg') AS new
ON DUPLICATE KEY UPDATE
    code = new.code, max_weight_kg = new.max_weight_kg, name = new.name;

-- -------------------------------------------------------------------- the xe
-- card_code la UID RFID 4 byte, luu duoi dang 8 ky tu hex. Collation
-- utf8mb4_0900_ai_ci khong phan biet hoa thuong, nen dau doc tra ve
-- 'A0D22940' van khop 'a0d22940'.
CREATE TABLE IF NOT EXISTS parking_card (
    card_id          INT UNSIGNED     NOT NULL AUTO_INCREMENT,
    card_code        CHAR(8)          NOT NULL,
    card_no          VARCHAR(16)      NOT NULL,
    customer_type_id TINYINT UNSIGNED NOT NULL,
    weight_class_id  TINYINT UNSIGNED NOT NULL,
    source_label     VARCHAR(64)      NOT NULL,
    is_active        TINYINT(1)       NOT NULL DEFAULT 1,
    created_at       DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (card_id),
    UNIQUE KEY uq_parking_card_code (card_code),
    UNIQUE KEY uq_parking_card_no   (card_no),
    KEY ix_parking_card_type (customer_type_id, weight_class_id),
    CONSTRAINT fk_parking_card_customer_type
        FOREIGN KEY (customer_type_id) REFERENCES customer_type (customer_type_id),
    CONSTRAINT fk_parking_card_weight_class
        FOREIGN KEY (weight_class_id)  REFERENCES weight_class  (weight_class_id)
) ENGINE = InnoDB;

-- View phang cho tang PLC: khong phai nho ma so.
CREATE OR REPLACE VIEW v_parking_card AS
SELECT  c.card_id,
        c.card_code,
        c.card_no,
        ct.code AS customer_type,
        ct.name AS customer_type_name,
        wc.code AS weight_class,
        wc.max_weight_kg,
        c.is_active
FROM    parking_card c
JOIN    customer_type ct ON ct.customer_type_id = c.customer_type_id
JOIN    weight_class  wc ON wc.weight_class_id  = c.weight_class_id;
