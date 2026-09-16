-- Cau hinh PLC Omron theo dai chinh thuc 192.169.1.(block_no + 100).
-- MySQL 8.0.19+. Chay lai nhieu lan deu an toan. UTF-8 KHONG BOM.
--
-- ------------------------------------------------------------------ lich su, de khong ai lam lai
-- Lan quet dau: ca dai 192.169.1.100-254 TRONG (155 dia chi, khong ICMP, khong
-- ARP, khong ca RST). Tuong la tai lieu sai, va tim thay 7 PLC o 192.168.250.1-.7.
--
-- Sau do doi chieu bang MAC moi vo le: SAU trong so 7 con do lan luot bien mat
-- khoi 192.168.250.x va hien ra tren 192.169.1.x, GIU NGUYEN MAC, lech dung +100:
--
--   3C-F7-D1-49-C2-3B   192.168.250.2  ->  192.169.1.102
--   3C-F7-D1-4E-90-BA   192.168.250.3  ->  192.169.1.103
--   3C-F7-D1-4E-C5-69   192.168.250.4  ->  192.169.1.104
--   3C-F7-D1-49-BA-25   192.168.250.5  ->  192.169.1.105
--   3C-F7-D1-4E-26-39   192.168.250.6  ->  192.169.1.106
--   3C-F7-D1-4E-95-98   192.168.250.7  ->  192.169.1.107
--
-- Tuc la CUNG MOT BO THIET BI dang duoc doi dia chi sang dung dai tai lieu.
-- 192.168.250.x chi la dia chi tam trong luc thi cong, KHONG duoc dung lai.
-- Cai tuong la "thiet bi chap chon" that ra la qua trinh doi IP.
--
-- Bai hoc: quet mang mot lan chi cho biet trang thai CUA LUC DO. Khi ket qua
-- do mau thuan voi tai lieu, doi chieu MAC truoc khi ket luan tai lieu sai.
--
-- ------------------------------------------------------------------ anh xa block
-- Cong thuc N+100 khien plc_device khong con phai gan tay: block_no la so in
-- tren ban ve, va dia chi suy thang ra tu do. Khong con rang buoc tam nao.
--
-- 192.169.1.0/24 KHONG phai dai private (chi 192.168.0.0/16 moi private), nen
-- ve lau dai van nen doi sang 192.168.x.x hoac 10.x.x.x. Nhung do la viec cua
-- khach, va gio da co thiet bi that chay tren do roi.

USE total_parking;

-- Bang chi chua cau hinh, khong chua du lieu nghiep vu -> xoa roi nap lai.
-- plc_request tro FK toi block chu khong toi plc_device nen khong anh huong.
DELETE FROM plc_device;

-- is_active = 0 cho TAT CA: hien truong moi cap nguon toi .108, con lai chua len.
-- Vong poll chi lay dong is_active = 1 (xem PlcDeviceRepository.GetAll), nen de
-- 0 thi khong co chuyen 100+ ket noi chet lam nghen vong lap.
-- Thiet bi len toi dau thi bat toi do bang cau UPDATE ben duoi.
INSERT INTO plc_device
    (block_id, ip_address, port, plc_node, pc_node,
     timeout_ms, poll_ms,
     card_word, card_word_len, card_layout,
     request_bit, request_bit_area,
     permit_bit, permit_bit_area, class_word, is_active)
SELECT b.block_id,
       CONCAT('192.169.1.', b.block_no + 100),
       9600,
       -- plc_node: bat tay FINS tra ve srv=1 tren moi con da do duoc
       -- (khong phai octet cuoi cua IP nhu thuong le).
       1,
       -- pc_node = 0 nghia la "PLC tu chon node cho may tram".
       --
       -- KHONG duoc de 1 o day. Node 1 la cua chinh PLC, xin trung se bi tu choi
       -- bat tay voi ma 0x24 (FINS/TCP: node address already in use) va MOI ket
       -- noi deu hong cung luc. Da dinh phai loi nay mot lan roi.
       -- Voi 0, PLC cap node tu do (quan sat thuc te: 251, 252, 253) va
       -- OmronFinsClient ghi de lai bang gia tri duoc cap.
       0,
       3000,
       500,
       -- CHUA CHOT: khach vua bao bo thanh ghi D106 / D400 / W102, khac bo da
       -- chot truoc day (D100 / D402 / W75.0). Da doc ca D0-D511 va W0-W255 tren
       -- moi PLC song: TOAN 0, chua bat duoc luot quet nao de phan xu.
       -- Giu bo cu cho toi khi bat duoc gia tri that.
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

-- ------------------------------------------------------------- bat cac con da len
-- Cap nhat danh sach nay khi hien truong cap nguon them PLC. Kiem tra nhanh:
--   powershell -File C:\Users\Admin\plc_activate.ps1
UPDATE plc_device
SET    is_active = 1
WHERE  ip_address IN (
    '192.169.1.101','192.169.1.102','192.169.1.103','192.169.1.104',
    '192.169.1.105','192.169.1.106','192.169.1.107','192.169.1.108'
);

-- ------------------------------------------------------------------ doi chieu
SELECT d.is_active, COUNT(*) AS so_plc
FROM   plc_device d GROUP BY d.is_active;

SELECT d.plc_id, d.ip_address, b.block_no, b.zone_id, b.slot_count,
       d.card_word AS d_card, d.class_word AS d_class,
       CONCAT(d.permit_bit_area, d.permit_bit) AS permit_bit
FROM   plc_device d
JOIN   block b ON b.block_id = d.block_id
WHERE  d.is_active = 1
ORDER  BY b.block_no;
