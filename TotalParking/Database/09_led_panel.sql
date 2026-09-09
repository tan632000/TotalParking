-- Bang LED chi huong va so cho trong.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- Giao thuc: led-control/docs/system-architecture.md muc 4, da kiem chung
-- 9/9 frame theo spec va ACK tren board that (testing-guide.md muc 6).

USE total_parking;

-- ------------------------------------------------------------------- led_panel
-- code = octet cuoi cua IP, cung la so bang ghi tren so do hien truong
-- (docs/led_position.jpg). Giu trung nhau de nhan vien va he thong goi cung ten.
--
-- kind:
--   ENTRANCE    bang dau ham. Spec: luon noi P1 va hien TONG so cho trong,
--               khong can biet mui ten chi dau -> chay duoc ma khong can do
--               thi dan duong.
--   DIRECTIONAL bang trong ham, mui ten chi ve zone cu the.
--
-- hub_type quyet dinh so cong toi da: HUB12 = 4 cong (P1..P4),
-- HUB75 = 3 cong (P1..P3, P4 khong dung duoc).
CREATE TABLE IF NOT EXISTS led_panel (
    panel_id      SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    code          VARCHAR(32)       NOT NULL,
    ip_address    VARCHAR(45)       NOT NULL,
    port          SMALLINT UNSIGNED NOT NULL DEFAULT 2022,
    hub_type      VARCHAR(8)        NOT NULL DEFAULT 'HUB12',
    kind          VARCHAR(16)       NOT NULL,
    floor_plan_id TINYINT UNSIGNED  NULL,
    pos_x         SMALLINT          NULL,
    pos_y         SMALLINT          NULL,
    note          VARCHAR(255)      NULL,
    is_active     TINYINT(1)        NOT NULL DEFAULT 1,
    created_at    DATETIME          NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (panel_id),
    UNIQUE KEY uq_led_panel_code (code),
    UNIQUE KEY uq_led_panel_endpoint (ip_address, port),
    CONSTRAINT fk_led_panel_floor_plan
        FOREIGN KEY (floor_plan_id) REFERENCES floor_plan (floor_plan_id),
    CONSTRAINT ck_led_panel_kind CHECK (kind IN ('ENTRANCE','DIRECTIONAL')),
    CONSTRAINT ck_led_panel_hub  CHECK (hub_type IN ('HUB12','HUB75'))
) ENGINE = InnoDB;

-- -------------------------------------------------------------- led_panel_port
-- Mot dong = mot cong hien thi = MOT FRAME. Bang 3 huong can 3 dong voi
-- port_index 0,1,2 va se nhan 3 frame rieng.
--
-- arrow_direction: 0 Up, 1 Right, 2 Down, 3 Left   (X2 trong frame)
-- arrow_color    : 0 Black, 1 Red, 2 Green, 3 Yellow (X3). Black = an mui ten,
--                  dung cho bang chi dem so khong co mui ten.
-- arrow_state    : 0 dung yen, 1 chay                (X4)
--
-- scope:
--   TOTAL  dem toan bai — dung cho bang dau ham
--   ZONES  dem cac zone liet ke trong zone_list ('1,2,6')
--
-- zone_list de NULL o giai doan 1: do thi dan duong chua khao sat, chua biet
-- mui ten nao dan toi zone nao.
CREATE TABLE IF NOT EXISTS led_panel_port (
    panel_id        SMALLINT UNSIGNED NOT NULL,
    port_index      TINYINT UNSIGNED  NOT NULL,
    arrow_direction TINYINT UNSIGNED  NOT NULL DEFAULT 0,
    arrow_color     TINYINT UNSIGNED  NOT NULL DEFAULT 2,
    arrow_state     TINYINT UNSIGNED  NOT NULL DEFAULT 0,
    scope           VARCHAR(8)        NOT NULL DEFAULT 'TOTAL',
    zone_list       VARCHAR(64)       NULL,
    note            VARCHAR(255)      NULL,
    is_active       TINYINT(1)        NOT NULL DEFAULT 1,
    PRIMARY KEY (panel_id, port_index),
    CONSTRAINT fk_led_panel_port_panel
        FOREIGN KEY (panel_id) REFERENCES led_panel (panel_id) ON DELETE CASCADE,
    CONSTRAINT ck_led_port_index CHECK (port_index BETWEEN 0 AND 3),
    CONSTRAINT ck_led_arrow_dir  CHECK (arrow_direction BETWEEN 0 AND 3),
    CONSTRAINT ck_led_arrow_col  CHECK (arrow_color BETWEEN 0 AND 3),
    CONSTRAINT ck_led_arrow_st   CHECK (arrow_state BETWEEN 0 AND 1),
    CONSTRAINT ck_led_scope      CHECK (scope IN ('TOTAL','ZONES'))
) ENGINE = InnoDB;

-- --------------------------------------------------------------------- seed
-- Bon bang da xac nhan song tren mang ngay 2026-09-09: ARP tra ve MAC that
-- (cung OUI 00-80-E1) va TCP 2022 mo tren ca bon.
--
-- Chi bang dau ham .50 duoc bat o giai doan 1. Ba bang chi huong de is_active
-- = 0 vi chua biet mui ten nao dan toi zone nao — bat len ma khong biet huong
-- thi bang se chi bua, te hon la khong chi gi.
--
-- So huong lay tu chu thich tren docs/led_position.jpg.
INSERT INTO led_panel (code, ip_address, port, kind, is_active, note) VALUES
    ('50', '192.169.1.50', 2022, 'ENTRANCE',    1, 'Bang dau ham, hien tong so cho trong'),
    ('56', '192.169.1.56', 2022, 'DIRECTIONAL', 0, '1 huong, ranh Zone 3/4'),
    ('57', '192.169.1.57', 2022, 'DIRECTIONAL', 0, '3 huong, Zone 2'),
    ('65', '192.169.1.65', 2022, 'DIRECTIONAL', 0, '3 huong, Zone 6/2') AS new
ON DUPLICATE KEY UPDATE
    ip_address = new.ip_address, port = new.port,
    kind = new.kind, note = new.note;

-- Bang dau ham: mot cong P1, dem toan bai.
-- arrow_color = 0 (Black) — an mui ten, vi day la bang dem so, chua xac nhan
-- no co mui ten hay khong.
INSERT INTO led_panel_port (panel_id, port_index, arrow_direction, arrow_color, scope, is_active, note)
SELECT p.panel_id, 0, 0, 0, 'TOTAL', 1, 'P1, tong so cho trong toan bai'
FROM   led_panel p WHERE p.code = '50'
ON DUPLICATE KEY UPDATE scope = 'TOTAL', is_active = 1;

-- Cong cua ba bang chi huong: tao san khung, TAT, va zone_list de trong.
-- Dien zone_list roi bat len la xong giai doan 2.
INSERT INTO led_panel_port (panel_id, port_index, scope, zone_list, is_active, note)
SELECT p.panel_id, x.n, 'ZONES', NULL, 0, 'Chua biet mui ten dan toi zone nao'
FROM   led_panel p
JOIN   (SELECT 0 AS n UNION ALL SELECT 1 UNION ALL SELECT 2) x
WHERE  p.code IN ('57','65')
ON DUPLICATE KEY UPDATE note = 'Chua biet mui ten dan toi zone nao';

INSERT INTO led_panel_port (panel_id, port_index, scope, zone_list, is_active, note)
SELECT p.panel_id, 0, 'ZONES', NULL, 0, 'Chua biet mui ten dan toi zone nao'
FROM   led_panel p WHERE p.code = '56'
ON DUPLICATE KEY UPDATE note = 'Chua biet mui ten dan toi zone nao';

-- --------------------------------------------------------- v_led_capacity
-- So cho trong chia theo CHIEU DAI khoang — dung truc khac voi v_zone_capacity
-- (chia theo tang, phuc vu luat pallet 2200/2600).
--
-- Ba bo dem khop dung ba truong cua frame LED:
--   free_l5m       -> X5.X6  Mechanical L < 5 M
--   free_l48m      -> X7.X8  Mechanical L < 4.8 M
--   free_standard  -> X9.X10 Standard
--
-- LUU Y ve thu tu: frame la L5M truoc, L48M sau — NGUOC voi thu tu enum
-- LedLane (L48M, L5M, Normal). Day la cho de nham nhat trong ca tang LED.
--
-- used_unassigned: phien dang mo nhung chua biet block (moi duoc phan zone,
-- khach chua quet the tai HMI). Khong tru vao bo dem nao vi khong biet no se
-- chiem loai khoang nao. Phoi ra de nhin thay, khong am tham bo qua.
CREATE OR REPLACE VIEW v_led_capacity AS
SELECT
    t.total_l5m, t.total_l48m, t.total_standard,
    u.used_l5m,  u.used_l48m,  u.used_standard, u.used_unassigned,
    GREATEST(CAST(t.total_l5m      AS SIGNED) - u.used_l5m,      0) AS free_l5m,
    GREATEST(CAST(t.total_l48m     AS SIGNED) - u.used_l48m,     0) AS free_l48m,
    GREATEST(CAST(t.total_standard AS SIGNED) - u.used_standard, 0) AS free_standard
FROM (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm > 4800 THEN b.slot_count END), 0) AS total_l5m,
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm <= 4800 THEN b.slot_count END), 0) AS total_l48m,
        COALESCE(SUM(CASE WHEN b.kind = 'Ground'      THEN b.slot_count END), 0) AS total_standard
    FROM block b WHERE b.is_active = 1
) t
CROSS JOIN (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm > 4800 THEN 1 END), 0) AS used_l5m,
        COALESCE(SUM(CASE WHEN b.kind = 'Mechanical'
                           AND b.bay_length_mm <= 4800 THEN 1 END), 0) AS used_l48m,
        COALESCE(SUM(CASE WHEN b.kind = 'Ground'      THEN 1 END), 0) AS used_standard,
        COALESCE(SUM(CASE WHEN s.block_id IS NULL     THEN 1 END), 0) AS used_unassigned
    FROM parking_session s
    LEFT JOIN block b ON b.block_id = s.block_id
    WHERE s.active_card_id IS NOT NULL
) u;
