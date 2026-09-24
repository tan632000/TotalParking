-- 43_tu_ha_bat.sql — cot phuc vu viec tu ha / tu bat cong van hanh cua khoi.
--
-- VI SAO CAN GHI NHO AI DA HA
-- Neu dich vu bat lai MOI khoi dang is_active = 0, thi mot khoi bi NGUOI khoa de
-- sua chua se bi may mo lai sau vai phut. Do la kieu hong nguy hiem: tho dang lam
-- viec trong khoi ma he thong lai xep xe vao.
--
-- Nen tu_dong_ha_luc la dau van tay cua dich vu: chi khoi nao CO dau nay moi duoc
-- tu bat lai. Khoi nguoi ha tay khong co dau nay, va may khong duoc dung toi.
--
-- VI SAO CAN DEM SO LAN DOI
-- Mot khoi dao dong (PLC chap chon) se bi ha roi bat lien tuc, va tai xe thay so
-- cho trong tren bang LED nhay loan. Vuot tran thi dich vu ngung tu doi khoi do
-- va cho nguoi xu ly — mot khoi dao dong la dau hieu hong phan cung, khong phai
-- thu de may tu chua.
--
-- Bo dem reset theo NGAY, nen can luu ca ngay dem: khong co no thi bo dem chi
-- tang mai va sau vai ngay moi khoi deu vuot tran.

SET @db := DATABASE();

-- --------------------------------------------------------- tu_dong_ha_luc
SET @co := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'block'
               AND COLUMN_NAME = 'tu_dong_ha_luc');
SET @sql := IF(@co = 0,
  'ALTER TABLE block ADD COLUMN tu_dong_ha_luc DATETIME(3) NULL DEFAULT NULL
     COMMENT "Khac NULL = chinh dich vu da ha khoi nay. Chi khoi co dau nay moi duoc tu bat lai."',
  'SELECT "tu_dong_ha_luc da co"');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ----------------------------------------------------- so_lan_doi_hom_nay
SET @co := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'block'
               AND COLUMN_NAME = 'so_lan_doi_hom_nay');
SET @sql := IF(@co = 0,
  'ALTER TABLE block ADD COLUMN so_lan_doi_hom_nay SMALLINT UNSIGNED NOT NULL DEFAULT 0
     COMMENT "So lan dich vu tu doi co van hanh trong ngay_dem. Vuot tran thi ngung tu doi."',
  'SELECT "so_lan_doi_hom_nay da co"');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ------------------------------------------------------------- ngay_dem
SET @co := (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'block'
               AND COLUMN_NAME = 'ngay_dem');
SET @sql := IF(@co = 0,
  'ALTER TABLE block ADD COLUMN ngay_dem DATE NULL DEFAULT NULL
     COMMENT "Ngay ma so_lan_doi_hom_nay dang dem. Khac hom nay thi bo dem duoc reset."',
  'SELECT "ngay_dem da co"');
PREPARE st FROM @sql; EXECUTE st; DEALLOCATE PREPARE st;

-- ===================== VIEW: KHOI DANG LECH GIUA HAI TANG =====================
-- Tra loi dung mot cau hoi: "khoi nao co PLC chet ma van dang nhan xe?"
--
-- Khong co cho nay thi viec tach vai chi tao them mot cot phai tu nho ma tra;
-- phat hien van hoan toan thu cong. Mot view la du, khong can giao dien.
CREATE OR REPLACE VIEW v_plc_lech_tang AS
SELECT b.block_no,
       b.zone_id,
       b.slot_count,
       b.is_active            AS dang_van_hanh,
       d.is_connected         AS plc_ket_noi,
       d.connected_changed_at AS doi_trang_thai_luc,
       d.last_probe_at        AS quan_sat_luc,
       b.tu_dong_ha_luc,
       b.so_lan_doi_hom_nay,
       CASE
         WHEN d.is_connected = 0 AND b.is_active = 1 THEN 'PLC chet ma van nhan xe'
         WHEN d.is_connected = 1 AND b.is_active = 0 AND b.tu_dong_ha_luc IS NULL
              THEN 'PLC song nhung nguoi da khoa'
         WHEN d.is_connected IS NULL AND b.is_active = 1 THEN 'Khong giam sat ma van nhan xe'
         ELSE 'Binh thuong'
       END AS tinh_trang
  FROM block b
  JOIN plc_device d ON d.block_id = b.block_id
 WHERE NOT (COALESCE(d.is_connected, -1) = 1 AND b.is_active = 1);

SELECT COLUMN_NAME FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'block'
   AND COLUMN_NAME IN ('tu_dong_ha_luc', 'so_lan_doi_hom_nay', 'ngay_dem')
 ORDER BY COLUMN_NAME;
