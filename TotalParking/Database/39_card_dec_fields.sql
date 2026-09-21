-- Them cac cot ho so the lay tu file xuat DEC vao parking_card.
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
--
-- ======================= VI SAO CAN CAC COT NAY =======================
-- Truoc day parking_card chi luu duoc ma the, so the, loai khach va hang tai.
-- File goc cua khach (docs/Phuong_tien_phan_loai_tai_trong_DEC.xlsx) mang theo
-- bien so, ten phuong tien, ten khach hang va ngay het han -- nhung khong co
-- cho nao de luu, nen moi lan nhap la mot lan bo mat thong tin. File 28 da ghi
-- lai chinh xac cai gia do:
--
--     "Bang parking_card khong co cot han the, nen thong tin nay BI BO."
--
-- Tat ca deu NULL duoc: 487 the dang co khong he biet nhung cot nay, va moi
-- lenh INSERT cu van chay dung sau khi ALTER.
--
-- ======================= VI SAO GIU CA weight_text =======================
-- Cot J cua file khach ('Phan loai tai trong xe') co 5 gia tri, trong do hai
-- gia tri KHONG chi ra hang tai nao: 'Chua xac dinh' va 'Khong phai o to'.
-- weight_class_id la NOT NULL va duoc tang PLC doc, nen buoc phai dien mot
-- con so -- 2200KG, giong lua chon cua file 28.
--
-- Nhung nhu vay thi mot the that su duoi 2200 kg va mot the chua ai phan loai
-- se nam trong DB y het nhau. weight_text giu nguyen van chu cua khach, nen
-- van loc ra duoc dung nhung dong can nguoi xac nhan lai:
--
--     SELECT card_no, plate, customer_name FROM parking_card
--     WHERE  weight_text IN ('Chua xac dinh', 'Khong phai o to');
--
-- ======================= VI SAO DDL CO GUARD =======================
-- MySQL 8 KHONG ho tro 'ALTER TABLE ... ADD COLUMN IF NOT EXISTS' (do la cu
-- phap MariaDB). Chay lan hai se bao ERROR 1060 Duplicate column name va dung
-- ca script. Dem truoc trong information_schema roi sinh cau lenh la cach duy
-- nhat lam duoc dieu do bang SQL thuan.
--
-- Guard dem ca 6 cot va chi chay khi thieu ca 6, vi day la MOT lenh ALTER duy
-- nhat: DDL trong MySQL 8 la nguyen tu, khong the dung lai o giua voi 3 cot da
-- them va 3 cot chua.

USE total_parking;

SET @missing = (
    SELECT 6 - COUNT(*)
    FROM   information_schema.columns
    WHERE  table_schema = DATABASE()
      AND  table_name   = 'parking_card'
      AND  column_name IN ('card_type', 'vehicle_name', 'weight_text',
                           'plate', 'customer_name', 'expiry_date')
);

SET @ddl = IF(@missing = 6,
    'ALTER TABLE parking_card
        ADD COLUMN card_type     VARCHAR(16)  NULL COMMENT "cot Loai cua file khach, vi du The"        AFTER card_no,
        ADD COLUMN vehicle_name  VARCHAR(64)  NULL COMMENT "cot Ten phuong tien"                       AFTER card_type,
        ADD COLUMN weight_text   VARCHAR(32)  NULL COMMENT "nguyen van cot Phan loai tai trong xe"     AFTER weight_class_id,
        ADD COLUMN plate         VARCHAR(16)  NULL COMMENT "cot Bien so hien tai"                      AFTER weight_text,
        ADD COLUMN customer_name VARCHAR(128) NULL COMMENT "cot Ten khach hang"                        AFTER plate,
        ADD COLUMN expiry_date   DATE         NULL COMMENT "cot Ngay het han, NULL = khong ro"         AFTER customer_name',
    'SELECT "parking_card da co day du 6 cot ho so the, khong ALTER" AS ket_qua');

PREPARE s FROM @ddl;
EXECUTE s;
DEALLOCATE PREPARE s;

-- Tra bien so ve mot the -- viec thuong xuyen nhat o quay bao ve khi khach doc
-- bien so chu khong doc so the. Khong UNIQUE: mot xe co the doi the, va file
-- khach cung co the mang bien so trung khi xe sang chu.
SET @ix = (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE  table_schema = DATABASE() AND table_name = 'parking_card'
      AND  index_name = 'ix_parking_card_plate'
);
SET @ddl2 = IF(@ix = 0,
    'CREATE INDEX ix_parking_card_plate ON parking_card (plate)',
    'SELECT "ix_parking_card_plate da co" AS ket_qua');
PREPARE s2 FROM @ddl2;
EXECUTE s2;
DEALLOCATE PREPARE s2;
