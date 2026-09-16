-- Cau hinh PLC Omron: IP = 192.169.1.(block_no + 100).
-- MySQL 8.0.19+. Chay lai nhieu lan an toan. UTF-8 KHONG BOM.
-- CHAY SAU 14_zone_blocks_from_customer.sql.
--
-- ======================= VI SAO PHAI NAP LAI =======================
-- 13_plc_real_range.sql cung dung cong thuc N+100, NHUNG luc do block_no chi la
-- SO THU TU TAM tu 12_block_from_cad.sql, khong phai so in tren ban ve. Nghia la
-- 192.169.1.101 khi do tro vao mot block tuy y, khong phai Block 1 that.
--
-- Doc D100 thi khong sao. Dieu huong xe thi sai cho nay la gui xe xuong NHAM TU.
--
-- Tu 14_*, block_no da la so that (khach xac nhan trong docs/tong_hop_zone_blocks.md,
-- doi chieu 99/106 khop voi nhan so tren ban ve), nen cong thuc moi dung.
--
-- Dai IP xac nhan trong docs/LUMI_IP_Range_CL1.xlsx:
--   PLC | Block 1 = 192.169.1.101 | Block N = 192.169.1.(N + 100) | range .100 ~ .254
--
-- is_active: de 0 cho tat ca, roi bat rieng nhung con DO DUOC tren mang. Vong poll
-- chi lay is_active = 1 (PlcDeviceRepository.GetAll), de 1 het thi 87 ket noi chet
-- se lam nghen vong lap. Cap nhat bang: powershell -File C:\Users\Admin\plc_activate.ps1

USE total_parking;

DELETE FROM plc_device;

INSERT INTO plc_device
    (block_id, ip_address, port, plc_node, pc_node,
     timeout_ms, poll_ms,
     card_word, card_word_len, card_layout,
     request_bit, request_bit_area,
     permit_bit, permit_bit_area, class_word, is_active)
SELECT b.block_id,
       CONCAT('192.169.1.', b.block_no + 100),
       9600,
       -- plc_node: bat tay FINS tra ve srv=1 tren moi con da do duoc.
       1,
       -- pc_node = 0 nghia la "PLC tu chon node cho may tram".
       --
       -- KHONG duoc de 1. Node 1 la cua chinh PLC, xin trung se bi tu choi bat tay
       -- voi ma 0x24 (FINS/TCP: node address already in use) va MOI ket noi deu
       -- hong cung luc. Da dinh phai loi nay mot lan roi.
       0,
       3000,
       500,
       -- CHUA CHOT bo thanh ghi. Khach tung bao D106 / D400 / W102, khac bo da chot
       -- truoc do (D100 / D402 / W75.0). Da doc ca D0-D511 va W0-W255 tren moi PLC
       -- song: toan 0, chua bat duoc luot quet nao de phan xu. Giu bo cu.
       100,     -- D100: ma the RFID
       0,       -- 0 = chua biet bo cuc, CardCodeDecoder tu do
       NULL,
       NULL, 'WR',
       '75.0', 'WR',
       402,     -- D402: 2200 / 2600 de HMI an hien pallet
       0
FROM   block b
WHERE  b.kind = 'Mechanical'
  AND  b.is_active = 1
  AND  b.block_no BETWEEN 1 AND 154;   -- .101 ~ .254

-- ------------------------------------------------------------------ doi chieu
SELECT COUNT(*) AS so_plc, MIN(ip_address) AS ip_dau, MAX(ip_address) AS ip_cuoi
FROM   plc_device;

SELECT b.zone_id, COUNT(*) AS so_plc,
       MIN(d.ip_address) AS ip_nho_nhat, MAX(d.ip_address) AS ip_lon_nhat
FROM   plc_device d JOIN block b ON b.block_id = d.block_id
GROUP  BY b.zone_id ORDER BY b.zone_id;
