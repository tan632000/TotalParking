-- Sua phan zone theo docs/tong_hop_phan_tich_zone_block.md (ban moi nhat cua khach).
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- CHAY SAU 14_zone_blocks_from_customer.sql.
--
-- ======================= THAY DOI =======================
-- So voi docs/tong_hop_zone_blocks.md (ban truoc), chi khac dung 2 block:
--
--   Block 78, 79:  ZONE 3  ->  ZONE 2
--
-- File moi ghi ro: "Block 78 va Block 79 thuoc ZONE 2, khong chuyen sang ZONE 3."
--
-- Sau khi sua, ca 6 zone deu la DAI SO LIEN MACH — day la dau hieu tot, vi so
-- block tren ban ve duoc danh theo duong di vat ly:
--
--   ZONE 1 -> 97-112            (16)
--   ZONE 2 -> 60-96             (37)
--   ZONE 3 -> 39-50             (12)
--   ZONE 4 -> 1-8, 33-38, 51-59 (23)
--   ZONE 5 -> 9-24              (16)
--   ZONE 6 -> 25-32             (8)
--
-- ============== CANH BAO VE slot_count ==============
-- File moi RUT LAI bang Block -> Model chi tiet cua file truoc:
--
--   "chua nen gan loai Model cu the cho tung Block 1-112 chi dua tren thu tu so Block"
--   "Bang Block -> Model chi tiet truoc do khong nen dung lam du lieu chinh thuc
--    neu chua duoc kiem tra lai truc tiep tren ban ve"
--
-- Nghia la cot slot_count hien tai (nap tu file truoc, tong 755) KHONG con la du
-- lieu chinh thuc. Bang model tren ban ve xac nhan tong dung la 764:
--
--   9 x 3sp + 1 x 5sp-4800L + 40 x 5sp-5000L + 22 x 6sp + 40 x 10sp = 112 bo / 764 cho
--
-- Da doi chieu doc lap bang may doc nhan tren mat bang: khop tuyet doi o 6sp (22)
-- va 10sp (40) voi bang model, chi file cu la lech. Xem muc doi chieu trong
-- 14_zone_blocks_from_customer.sql.
--
-- QUYET DINH DA CHOT: giu slot_count theo docs/tong_hop_zone_blocks.md.
--
-- Khach xac nhan KHONG CON tai lieu nao khac ngoai hai file da gui, nen khong co
-- bang Block -> Model doi chieu tung con. Ba lua chon deu khong hoan hao:
--
--   755  docs/tong_hop_zone_blocks.md   <- DANG DUNG (so tu file khach)
--   761  suy tu hinh hoc ban ve         <- suy luan cua he thong, khong phai so khach
--   764  bang model tren ban ve         <- dung tong, nhung khong co phan bo tung block
--
-- Chon 755 vi day la con so DUY NHAT co du lieu tung block, va la so do khach
-- cung cap. Hai con kia hoac khong chia duoc ve tung block (764), hoac la suy
-- luan cua may chu khong phai tai lieu (761).
--
-- Da thu ghep hinh hoc bang thuat toan gan toi uu (Hungarian) tren nhan so va
-- nhan model trong ban ve: 6/112 block van ghep sai ro ret, deu roi vao cac block
-- co nhan so BI LAP tren ban ve (1,2,3,5,9,22,27,40,100,112). Khong du tin cay
-- de thay the so cua khach.
--
-- HE QUA PHAI BIET: tong 755 thap hon bang model 9 cho. He thong bao IT cho hon
-- so cho that — sai ve phia an toan: tai xe vao roi van con cho, con nguoc lai
-- thi ho di vao roi khong co cho.
--
-- Muon sua cho dung 764 thi phai biet block NAO bi dem thieu; doan bua con nao
-- se lam hong ca du lieu dieu huong. Neu sau nay khach co bang Block -> Model,
-- cap nhat slot_count va bay_length_mm o day.

USE total_parking;

UPDATE block
SET    zone_id = 2
WHERE  kind = 'Mechanical' AND block_no IN (78, 79);

-- ------------------------------------------------------------------ doi chieu
SELECT b.zone_id,
       COUNT(*)                                   AS so_block,
       MIN(b.block_no)                            AS block_nho_nhat,
       MAX(b.block_no)                            AS block_lon_nhat,
       SUM(b.slot_count)                          AS so_o_tam_tinh
FROM   block b
WHERE  b.kind = 'Mechanical' AND b.is_active = 1
GROUP  BY b.zone_id
ORDER  BY b.zone_id;

-- Kiem tra khop dung con so file moi: 16 / 37 / 12 / 23 / 16 / 8
SELECT SUM(so_block) AS tong_block FROM (
    SELECT COUNT(*) AS so_block FROM block
    WHERE kind = 'Mechanical' AND is_active = 1 GROUP BY zone_id
) t;
