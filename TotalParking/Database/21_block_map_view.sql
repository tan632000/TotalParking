-- Ban do hop nhat: zone -> block -> PLC -> o do.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi tao VIEW, khong doi du lieu -> user ung dung chay duoc.
--
-- ======================= VI SAO =======================
-- Du lieu da co du nhung nam o BON bang, moi cau hoi don gian deu phai join:
--
--   zone            6 dong    ranh gioi va ten zone
--   block         118 dong    block_no, zone_id, so o, vi tri tren ban ve
--   plc_device    112 dong    block_id -> IP + dia chi thanh ghi
--   plc_slot_state 755 dong   block_id + o -> thanh ghi + ma the dang giu
--
-- Tach nhu vay la DUNG ve chuan hoa (moi su that mot cho, khong lap), nhung
-- tra cuu hang ngay thi cuc. View nay gop lai de doc; ghi van vao bang goc.

USE total_parking;

-- --------------------------------------------------------------- v_block_map
-- Mot dong = mot block. Tra loi duoc gan het cau hoi thuong gap:
--   block nay thuoc zone nao, PLC o IP nao, con song khong, co may o, may o co xe.
CREATE OR REPLACE VIEW v_block_map AS
SELECT
    b.block_id,
    b.block_no,
    b.kind,
    z.zone_id,
    z.code            AS zone_code,
    z.name            AS zone_name,
    b.slot_count,
    b.bay_length_mm,

    -- PLC. NULL o day nghia la block chua khai bao PLC (vd 6 cum do nen: chung
    -- do bang cam bien PGS/ZCU chu khong qua PLC).
    d.ip_address      AS plc_ip,
    d.port            AS plc_port,
    d.is_active       AS plc_bat,
    d.find_card_word  AS d_doc_the,
    d.find_answer_word AS d_tra_block,

    -- O do, dem tu plc_slot_state.
    COALESCE(s.so_o, 0)       AS so_o_khai_bao,
    COALESCE(s.o_co_xe, 0)    AS o_co_xe,
    COALESCE(s.so_o, 0) - COALESCE(s.o_co_xe, 0) AS o_trong,
    s.doc_luc                 AS lan_doc_cuoi,

    -- Canh bao lech: so o khai bao trong plc_slot_state phai bang slot_count.
    -- Lech nghia la 17_plc_slot_state.sql chua chay lai sau khi doi slot_count.
    (COALESCE(s.so_o, 0) <> b.slot_count AND b.kind = 'Mechanical') AS lech_so_o
FROM   block b
JOIN   zone z ON z.zone_id = b.zone_id
LEFT   JOIN plc_device d ON d.block_id = b.block_id
LEFT   JOIN (
    SELECT st.block_id,
           COUNT(*) AS so_o,
           -- Chi dem la CO XE khi ma doc duoc khop THE THAT. Thanh ghi khac 0 ma
           -- khong khop the nao la du lieu la, khong phai xe.
           SUM(c.card_id IS NOT NULL) AS o_co_xe,
           MAX(st.read_at) AS doc_luc
    FROM   plc_slot_state st
    LEFT   JOIN parking_card c ON c.card_code = st.card_code
    GROUP  BY st.block_id
) s ON s.block_id = b.block_id
WHERE  b.is_active = 1;

-- --------------------------------------------------------------- v_zone_map
-- Mot dong = mot zone. Dung cho bang LED va dieu huong.
CREATE OR REPLACE VIEW v_zone_map AS
SELECT
    m.zone_id, m.zone_code, m.zone_name,
    SUM(m.kind = 'Mechanical')                              AS so_block_co_khi,
    SUM(IF(m.kind = 'Mechanical', m.slot_count, 0))         AS o_co_khi,
    SUM(IF(m.kind = 'Ground',     m.slot_count, 0))         AS o_do_nen,
    SUM(m.slot_count)                                       AS tong_o,
    SUM(m.o_co_xe)                                          AS o_co_xe,
    SUM(m.slot_count) - SUM(m.o_co_xe)                      AS o_trong,
    SUM(m.plc_bat = 1)                                      AS plc_dang_len,
    SUM(m.plc_ip IS NOT NULL)                               AS plc_khai_bao,
    MIN(m.plc_ip)                                           AS ip_dau,
    MAX(m.plc_ip)                                           AS ip_cuoi
FROM   v_block_map m
GROUP  BY m.zone_id, m.zone_code, m.zone_name;

-- ------------------------------------------------------------------ doi chieu
SELECT * FROM v_zone_map ORDER BY zone_id;

SELECT 'block co so o khai bao LECH voi slot_count' AS kiem_tra, COUNT(*) AS so_block
FROM   v_block_map WHERE lech_so_o = 1;

SELECT block_no, zone_id, slot_count, plc_ip, plc_bat, so_o_khai_bao, o_co_xe
FROM   v_block_map WHERE block_no IN (1, 27, 65, 95, 103) ORDER BY block_no;
