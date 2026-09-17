-- Nap danh sach the o to bo sung tu docs/oto_card (nhan ngay 17/09/2026).
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- Chi INSERT/UPDATE -> tai khoan ung dung chay duoc.
--
-- ======================= NGUON =======================
-- File CSV xuat tu he thong the cua toa nha, 38 dong. Cot 'Ma dinh danh' la ma
-- the ma PLC doc duoc; cot 'Ten dinh danh' la so the in tren the (S.xxxxx).
--
-- 17/38 dong nap duoc. 21 dong con lai KHONG nap duoc vi du lieu nguon thieu
-- hoac sai cho -- liet ke day du o cuoi file de con truy nguoc.
--
-- ======================= CACH LOC =======================
-- Mot dong chi duoc nap khi ma dinh danh:
--   1. co gia tri
--   2. dung 8 ky tu hex  (ma the 32 bit, bo cuc Binary32Lo da chot)
--   3. KHAC bien so xe
--   4. lon hon 65535
--
-- Dieu kien 3 la thu de bat nhat. Nhieu dong bi dan BIEN SO vao o ma the, va
-- co nhung bien so tinh co toan ky tu hex nen luat 2 khong bat duoc:
--   STT 38  ma='30E83499'  bien so='30E83499'   <- la bien so, khong phai ma the
--
-- Dieu kien 4 trung voi luat da dung o v_slot_taken (file 27): gia tri duoi
-- 65536 la thanh ghi noi bo cua ladder, khong phai ma the.
--
-- ======================= HAI CHO PHAI DOAN =======================
-- customer_type_id = 2 (XT - xe the thang).
--   Chac chan: ca 17 dong deu thuoc nhom 'THE THANG OTO'.
--
-- weight_class_id = 1 (pallet 2200 kg).
--   KHONG chac. File nguon khong co thong tin hang tai. Cot nay NOT NULL nen
--   buoc phai dien mot gia tri.
--
--   Khong chon 0 (THUONG) vi y nghia cua no la "qua tai, khong dung duoc pallet
--   co khi" -- dat mac dinh do se loai ca 17 xe khoi bai co khi.
--
--   Chon 1 va DANH DAU ngay trong source_label de con loc ra ma sua:
--       SELECT * FROM parking_card WHERE source_label LIKE '%CHUA RO HANG TAI%';
--
--   Hien tai cot nay KHONG anh huong gi: no chi duoc doc boi CardScanService,
--   ma lop do thuoc hop dong cu (D402 + W75.0) va khong con duoc goi tu dau.
--   Luong dang chay (D1002 -> tra cuu -> D1000) khong dung hang tai. Nhung neu
--   sau nay bat dieu huong theo tai trong thi con so nay phai dung.
--
-- ======================= KHONG LUU HAN THE =======================
-- File co cot 'Ngay het han' (11 the den 31/12/2028, 6 the den 07/09/2099).
-- Bang parking_card khong co cot han the, nen thong tin nay BI BO. Neu can
-- chan the het han thi phai them cot, khong suy ra duoc tu du lieu hien co.

USE total_parking;

INSERT INTO parking_card (card_code, card_no, customer_type_id, weight_class_id, source_label, is_active)
VALUES
    ('61d41330', 'S.06575', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30M11368  Su Ngoc Hai
    ('61d4faa0', 'S.02461', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30G72033  Nguyen Dinh Nguyen
    ('61e2a3c0', 'S.04280', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30H43981  Duong Quang Dat
    ('61bb8380', 'S.03715', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30H04547  Nguyen Xuan Hop
    ('611910a0', 'S.03750', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30H40539  Pham MInh Hang
    ('61d46010', 'S.03208', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30K30076  Nguyen Thien Toan
    ('61dc6320', 'S.04453', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30F41198  Do Van Cuong
    ('a118eff1', 'S.01221', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30L84955  Nguyen Van HIeu
    ('61e410b0', 'S.05042', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 51F85058  NGuyen Quy Thanh
    ('61df5ae0', 'S.04255', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30K72576  Doan LInh Trang
    ('9c2e01d0', 'S.06754', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30B66685  Nguyen chi nghia
    ('a0d41e40', 'S.00053', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30L64633  Ngo Van Tuan
    ('a0a6f770', 'S.00052', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30L81559  Nguyen Thi Hien
    ('a0b965c0', 'S.00048', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30M63906  Hoang Anh
    ('a0d96050', 'S.00051', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30E32432  Thu
    ('9c323300', 'S.00050', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1),  -- 30L95574  Hai
    ('a0ff0790', 'S.00049', 2, 1, 'OTO CARD 17/09 (CHUA RO HANG TAI)', 1)  -- 30M03135  Thuy
ON DUPLICATE KEY UPDATE
    is_active    = 1,
    source_label = 'OTO CARD 17/09 (CHUA RO HANG TAI)';

-- ------------------------------------------------------------------ doi chieu
SELECT source_label, COUNT(*) AS so_the, SUM(is_active) AS dang_bat
FROM   parking_card GROUP BY source_label ORDER BY so_the DESC;

SELECT card_id, card_code, card_no, source_label
FROM   parking_card WHERE source_label LIKE 'OTO CARD 17/09%' ORDER BY card_no;

-- The moi nap co dang xuat hien trong thanh ghi o nao khong
SELECT s.card_code, b.block_no, s.slot_index AS o, c.card_no
FROM   plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
JOIN   parking_card c ON c.card_code = s.card_code
WHERE  c.source_label LIKE 'OTO CARD 17/09%';

-- ======================= 21 DONG KHONG NAP DUOC =======================
--   STT 1   ma=''               ten=-          bien=30H00434     [khong co ma dinh danh]
--   STT 2   ma=''               ten=-          bien=29K25493     [khong co ma dinh danh]
--   STT 3   ma=''               ten=-          bien=30M18568     [khong co ma dinh danh]
--   STT 4   ma=''               ten=-          bien=29A14368     [khong co ma dinh danh]
--   STT 5   ma=''               ten=-          bien=34A45398     [khong co ma dinh danh]
--   STT 6   ma=''               ten=-          bien=30A60957     [khong co ma dinh danh]
--   STT 7   ma=''               ten=-          bien=30B16824     [khong co ma dinh danh]
--   STT 8   ma=''               ten=-          bien=30E00239     [khong co ma dinh danh]
--   STT 13  ma=''               ten=-          bien=30H07862     [khong co ma dinh danh]
--   STT 24  ma='30K78831'       ten=S.00100    bien=30K78831     [khong phai 8 ky tu hex]
--   STT 25  ma='30A83260'       ten=S.00099    bien=30A83260     [ma trung bien so]
--   STT 26  ma='30M48124'       ten=S.00094    bien=30M48124     [khong phai 8 ky tu hex]
--   STT 27  ma='30F30984'       ten=S.00093    bien=30F30984     [ma trung bien so]
--   STT 28  ma='30B55509'       ten=S.00090    bien=30B55509     [ma trung bien so]
--   STT 29  ma='30L02273'       ten=S.00089    bien=30L02273     [khong phai 8 ky tu hex]
--   STT 30  ma='30F99214'       ten=S.00088    bien=30F99214     [ma trung bien so]
--   STT 31  ma='30G27945'       ten=S.00086    bien=30G27945     [khong phai 8 ky tu hex]
--   STT 35  ma='30H33682'       ten=S.00032    bien=30H33682     [khong phai 8 ky tu hex]
--   STT 36  ma='30L54170'       ten=S.00002    bien=30L54170     [khong phai 8 ky tu hex]
--   STT 37  ma='THE THANG OTO'  ten=HUNG ANH   bien=35A-48961    [khong phai 8 ky tu hex]
--   STT 38  ma='30E83499'       ten=S.00034    bien=30E83499     [ma trung bien so]--
-- Chin dong dau la xe da dang ky nhung CHUA duoc gan the -- khong phai loi du
-- lieu, chi la chua phat the. Muoi hai dong con lai la bien so bi dan nham vao
-- o ma the; can lay lai ma the that tu he thong the cua toa nha.
