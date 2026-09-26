-- v_zone_capacity.in_use doc tu THANH GHI thay vi tu bang parking_session.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO =======================
-- Dinh nghia cu dem so phien gui xe dang mo:
--
--   (SELECT COUNT(*) FROM parking_session s
--     WHERE s.active_card_id IS NOT NULL AND s.zone_id = z.zone_id) AS in_use
--
-- Bang parking_session co 0 dong, va no rong khong phai vi bai vang. No rong vi
-- ham duy nhat tao phien -- CardScanService.TryOpenSession -- khong con noi goi
-- nao trong ca ma nguon. Luong hien tai chi hoi "xe dang o block nao" qua
-- CarLocatorService, khong mo phien. Xem ghi chu tai PlcConnection.cs:55.
--
-- Bang chung trong du lieu: 560 luot quet the da xu ly, 0 luot gan voi phien.
--
-- Hau qua: in_use = 0 o moi zone, nen
--   * trang mat bang luon hien "Mat do 0" du thanh ghi co xe
--   * FreeMechanical = TotalMechanical - 0 = toan bo suc chua
--   * ZoneRouter khong bao gio thay zone nao day, va tieu chi phu
--     "hoa nhau thi chon zone rong hon" khong bao gio hoat dong vi
--     UsedRatio bang 0 o tat ca cac zone
--
-- ======================= CHI DEM DUOC O CO KHI =======================
-- plc_slot_state co dung 755 dong, bang tong so o cua 112 block Mechanical.
-- 80 o do nen (6 block Ground) KHONG co thanh ghi nao -- khong co cam bien, nen
-- he thong khong the biet chung co xe hay khong.
--
-- Vi vay in_use o day la "so O CO KHI dang co xe", khong phai "so xe trong
-- zone". Phia ung dung phai tru in_use khoi TotalMechanical, TUYET DOI khong
-- tru khoi TotalGround: zone 3 chi co 2 o do nen, neu tru so o co khi dang co
-- xe vao do thi xe hang THUONG bi tu choi ngay du ca 2 o deu trong.
-- VehicleRouting.cs da duoc sua cho dung dieu nay.
--
-- ======================= DUNG CHUNG MOT TRONG TAI =======================
-- Dem qua v_slot_taken chu khong dem thang plc_slot_state, de cung mot luat loc
-- rac voi ban do khoi, bang LED va BlockAllocator. Sau migration 46, luat do la:
-- the co trong danh muc thi tin thanh ghi du trung may block; ma la thi van doi
-- duy nhat mot block.

USE total_parking;

CREATE OR REPLACE VIEW v_zone_capacity AS
SELECT  z.zone_id, z.code, z.gate_rank,
        SUM(CASE WHEN b.kind = 'Mechanical' THEN b.slot_count   ELSE 0 END) AS total_mech,
        SUM(CASE WHEN b.kind = 'Mechanical' THEN b.column_count ELSE 0 END) AS total_tier0,
        SUM(CASE WHEN b.kind = 'Ground'     THEN b.slot_count   ELSE 0 END) AS total_ground,
        -- Loc b2.is_active = 1 cho khop voi phep tinh total_* o tren: mot block
        -- bi tat phai bien mat khoi CA suc chua LAN so o dang dung, khong thi
        -- zone do se bao am so cho trong.
        (SELECT COUNT(*)
         FROM   v_slot_taken t
         JOIN   block b2 ON b2.block_id = t.block_id
         WHERE  b2.zone_id = z.zone_id AND b2.is_active = 1)                 AS in_use
FROM    zone z
LEFT JOIN block b ON b.zone_id = z.zone_id AND b.is_active = 1
WHERE   z.is_active = 1
GROUP BY z.zone_id, z.code, z.gate_rank;

-- ------------------------------------------------------------------ doi chieu
SELECT zone_id, code, total_mech, total_tier0, total_ground, in_use,
       GREATEST(CAST(total_mech AS SIGNED) - in_use, 0) AS con_trong_co_khi,
       total_ground                                     AS con_trong_do_nen
FROM   v_zone_capacity
ORDER  BY zone_id;

-- Doi chung: tong in_use cua moi zone phai bang tong so o trong v_slot_taken.
-- Lech nhau nghia la co block khong thuoc zone nao, hoac bi tat nhung van co xe.
SELECT (SELECT SUM(in_use) FROM v_zone_capacity)                AS tong_theo_zone,
       (SELECT COUNT(*)    FROM v_slot_taken)                   AS tong_v_slot_taken,
       (SELECT COUNT(*) FROM v_slot_taken t
          JOIN block b ON b.block_id = t.block_id
         WHERE b.is_active = 0)                                 AS o_thuoc_block_da_tat;
