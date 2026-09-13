-- Cau hinh PLC Omron cua tung block va nhat ky tung luot quet the.
-- MySQL 8.0.19+. Chay lai nhieu lan deu an toan. UTF-8 KHONG BOM.
--
-- Thiet ke: docs/parking-session-db-design.md muc 6.

USE total_parking;

-- ------------------------------------------------------------------ plc_device
-- Moi block dung 1 PLC. KHONG luu trang thai ket noi o day: 112 PLC poll 500ms
-- se thanh hang chuc UPDATE moi giay vao mot bang cau hinh. Trang thai song giu
-- trong bo nho o PlcConnectionManager; chi ghi DB khi doi trang thai.
--
-- Ban do thanh ghi khai bao bang du lieu thay vi hard-code trong C#, de mot
-- block co ladder khac van cau hinh duoc ma khong phai build lai.
-- Gia tri mac dinh la dia chi da chot voi ben lap trinh PLC: D100 / W75.0 / D402.
CREATE TABLE IF NOT EXISTS plc_device (
    plc_id       SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    block_id     SMALLINT UNSIGNED NOT NULL,
    ip_address   VARCHAR(45)       NOT NULL,
    port         SMALLINT UNSIGNED NOT NULL DEFAULT 9600,
    -- FINS routing: DA1 (node cua PLC) va SA1 (node cua may SCADA).
    -- Bat tay FINS/TCP tra ve node duoc cap phat va client ghi de lai, nen day
    -- chi la gia tri de nghi.
    plc_node     TINYINT UNSIGNED  NOT NULL DEFAULT 10,
    pc_node      TINYINT UNSIGNED  NOT NULL DEFAULT 1,
    timeout_ms   SMALLINT UNSIGNED NOT NULL DEFAULT 3000,
    poll_ms      SMALLINT UNSIGNED NOT NULL DEFAULT 500,

    -- PLC -> SCADA
    card_word        SMALLINT UNSIGNED NOT NULL DEFAULT 100,    -- D100
    -- So word ma ma the chiem. 0 = chua biet, cho phep tang doc tu do bo cuc
    -- bang cach doi chieu voi bang the (CardCodeDecoder). Xac dinh xong thi
    -- ghi con so that vao day va khoa lai: de tu do vinh vien co rui ro mot bo
    -- cuc sai van trung vao ma the khac.
    card_word_len    TINYINT UNSIGNED  NOT NULL DEFAULT 0,
    -- Ten bo cuc da chot: Binary32Lo | Binary32Hi | Ascii2PerWord | Ascii1PerWord.
    -- NULL = chua chot, dang tu do.
    card_layout      VARCHAR(16)       NULL,
    -- Bit PLC bat khi co luot quet moi, SCADA tat sau khi tra loi.
    -- NULL = chua co, he thong roi ve che do so sanh gia tri D100 (kem hon,
    -- khong phat hien duoc hai luot quet cung mot the).
    request_bit      VARCHAR(8)        NULL,
    request_bit_area VARCHAR(4)        NOT NULL DEFAULT 'WR',

    -- SCADA -> PLC/HMI
    permit_bit       VARCHAR(8)        NOT NULL DEFAULT '75.0', -- W75.0
    permit_bit_area  VARCHAR(4)        NOT NULL DEFAULT 'WR',
    class_word       SMALLINT UNSIGNED NOT NULL DEFAULT 402,    -- D402, chua 2200/2600

    is_active    TINYINT(1)        NOT NULL DEFAULT 1,
    created_at   DATETIME          NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (plc_id),
    UNIQUE KEY uq_plc_block (block_id),
    UNIQUE KEY uq_plc_endpoint (ip_address, port),
    CONSTRAINT fk_plc_block FOREIGN KEY (block_id) REFERENCES block (block_id),
    CONSTRAINT ck_plc_bit_area
        CHECK (request_bit_area IN ('DM','CIO','WR','HR','AR')
           AND permit_bit_area  IN ('DM','CIO','WR','HR','AR'))
) ENGINE = InnoDB;

-- ----------------------------------------------------------------- plc_request
-- Nhat ky tung luot quet the: SCADA doc duoc gi va da tra loi HMI ra sao.
-- Day la bang doi chieu khi co tranh cai "luc do he thong biet gi".
CREATE TABLE IF NOT EXISTS plc_request (
    plc_request_id BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
    block_id       SMALLINT UNSIGNED NOT NULL,
    -- Du lieu tho doc tu D100, dang hex, TRUOC khi giai ma. Giu lai de con
    -- giai ma lai duoc neu bo cuc D100 hoa ra khac gia dinh ban dau.
    raw_words      VARCHAR(128)      NOT NULL,
    card_code      CHAR(8)           NULL,
    received_at    DATETIME(3)       NOT NULL,
    -- Dung hai gia tri DA GHI xuong W75.0 va D402, khong suy lai tu trang thai
    -- phien: cai can biet khi truy vet la SCADA da noi gi voi HMI.
    result_permit  TINYINT(1)        NULL,
    result_class   SMALLINT UNSIGNED NULL,
    reject_reason  VARCHAR(32)       NULL,
    session_id     BIGINT UNSIGNED   NULL,
    answered_at    DATETIME(3)       NULL,
    PRIMARY KEY (plc_request_id),
    KEY ix_plc_request_received (received_at),
    KEY ix_plc_request_block (block_id, received_at),
    KEY ix_plc_request_card (card_code, received_at),
    CONSTRAINT fk_plc_request_block FOREIGN KEY (block_id) REFERENCES block (block_id)
) ENGINE = InnoDB;

-- ------------------------------------------------------------- seed thi diem
-- Dai IP that lay tu docs/LUMI IP Range CL1.xlsx:
--
--   192.169.1.2   ~ .49    May tinh tram (.4), Camera (.2), TV (.13)
--   192.169.1.50  ~ .69    Bang LED
--   192.169.1.70  ~ .79    PGS: CCU + ZCU 1..5
--   192.169.1.100 ~ .254   PLC   ->  Block N = 192.169.1.(N + 100)
--
-- Quy tac N+100 la CO HE THONG, khong phai gan tay tung con: sinh thang tu
-- block_no de khong bao gio lech giua bang block va bang PLC.
--
-- LUU Y: 192.169.1.0/24 KHONG phai dai private. Chi 192.168.0.0/16 moi private;
-- 192.169.x.x thuoc khong gian dia chi cong cong, dang trung voi IP that cua
-- mot to chuc khac tren Internet. Khong gay loi trong mang kin, nhung neu may
-- tram co duong ra Internet thi mot ngay nao do se co dia chi khong the truy
-- cap duoc. Nen doi sang 192.168.x.x hoac 10.x.x.x luc con de.
--
-- plc_node de = block_no: DE NGHI, chua xac nhan voi ben lap trinh PLC.
-- Bat tay FINS/TCP tra ve node duoc cap phat va client ghi de lai, nen sai o
-- day khong lam hong ket noi.
INSERT INTO plc_device (block_id, ip_address, port, plc_node, pc_node)
SELECT b.block_id,
       CONCAT('192.169.1.', b.block_no + 100),
       9600,
       LEAST(b.block_no, 254),
       1
FROM   block b
WHERE  b.kind = 'Mechanical'
  AND  b.block_no BETWEEN 1 AND 154   -- .101 ~ .254
ON DUPLICATE KEY UPDATE
    ip_address = CONCAT('192.169.1.', b.block_no + 100),
    port = 9600;
