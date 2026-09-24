-- 44_poll_du.sql — dua moi PLC da khai bao vao vong poll.
--
-- ===================== DAY LA THAY DOI CHAM THIET BI THAT =====================
-- Bat mot khoi vao vong poll la GHI xuong PLC cua no:
--   PlcConnection.cs:67  khoi tao _lastBandWritten = -1
--   PlcConnection.cs:558 nhip poll dau tien cua MOI ket noi moi ghi D1004 mot lan
-- Do tren nhat ky that: 2285/3083 dong WRITE D1004 la ghi 0 khi D106 rong.
--
-- Nen truoc khi chay file nay, PHAI co nguoi xac nhan khong co tho dang lam viec
-- tren cac khoi sap bat. Xac nhan cua lan chay 24/09 duoc ghi trong Receipt cua
-- task-04.
--
-- ===================== CHAY XONG PHAI GOI RELOAD =====================
-- UPDATE nay khong tu lam vong poll doc lai. Tap ket noi trong bo nho chi doi khi
-- goi Load() hoac Reload(). Do duoc luc 17:56 ngay 24/09: CSDL 107 ma vong poll
-- chi giu 87 — vi co nguoi UPDATE ma quen buoc nay.
--
--   POST http://localhost:8080/PlcStatus/Reload
--
-- ===================== KHOI PHUC =====================
-- Danh sach khoi duoc bat nam trong specs/tach-vai-co-plc/artifacts/poll-du.json.
-- Tat lai bang cach doi is_active ve 0 cho dung nhung block_no do, roi Reload.

SELECT b.block_no, d.ip_address, b.slot_count
  FROM plc_device d JOIN block b ON b.block_id = d.block_id
 WHERE d.is_active = 0
 ORDER BY b.block_no;

UPDATE plc_device SET is_active = 1 WHERE is_active = 0;

SELECT COUNT(*) AS con_bi_bo_quen FROM plc_device WHERE is_active = 0;
SELECT COUNT(*) AS tong_trong_vong_poll FROM plc_device WHERE is_active = 1;
