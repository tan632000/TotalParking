-- =========================================================================
-- Gán lại zone cho 29 block theo sheet CL1, và đổi tên zone theo công thức
-- =========================================================================
--
-- Nguồn sự thật: docs/TTP_LUMI_TYPE_BLOCK_RE01.xlsx, sheet CL1 (112 block,
-- 6 zone, tổng 764 ô). Sheet CL2 là CỤM VẬT LÝ KHÁC (91 block, 4 zone) và
-- không được dùng cho hệ thống này.
--
-- ===================== RÀNG BUỘC THỨ TỰ CHẠY =====================
--
-- File này phải chạy SAU 14_zone_blocks_from_customer.sql.
-- 14_...sql:52 có `DELETE FROM block;` rồi INSERT lại zone_id viết cứng cho
-- cả 118 block. Chạy lại file 14 sẽ đưa 29 block dưới đây về zone cũ mà
-- không cảnh báo gì. Nếu file 14 được chạy lại, PHẢI chạy lại 51 rồi 52.
--
-- ===================== VÌ SAO LIỆT KÊ TỪNG SỐ BLOCK =====================
--
-- Danh sách không liên tục: block 60 sang zone 4 trong khi 61 ngay cạnh sang
-- zone 5, và 63..69 sang zone 6. Mọi cách viết gọn bằng BETWEEN hay bằng
-- điều kiện suy diễn từ zone hiện tại đều có nguy cơ quét nhầm block không
-- nằm trong danh sách. Viết tường minh thì câu lệnh tự nó là bằng chứng.
--
-- Đối chiếu:  python tools/doi_chieu_zone_block.py --phan zone
-- Cần quyền DDL (tài khoản totalparking không có) -> chạy bằng root.


-- ----------------------------------------------------------------- bản chụp
-- Chụp TRƯỚC câu UPDATE đầu tiên. CREATE TABLE ... LIKE giữ nguyên khoá chính.
-- Chỉ chụp khi bảng sao lưu còn RỖNG. INSERT IGNORE một mình là chưa đủ: nó
-- bỏ qua dòng trùng khoá, nhưng dòng MỚI sinh ra sau lần chạy đầu vẫn lọt vào,
-- làm câu hoàn tác tưởng chúng là dữ liệu gốc và không xoá nữa.
CREATE TABLE IF NOT EXISTS block_sao_luu_51 LIKE block;
SET @da_chup := (SELECT COUNT(*) FROM block_sao_luu_51);
INSERT IGNORE INTO block_sao_luu_51 SELECT * FROM block WHERE @da_chup = 0;

CREATE TABLE IF NOT EXISTS zone_sao_luu_51 LIKE zone;
SET @da_chup := (SELECT COUNT(*) FROM zone_sao_luu_51);
INSERT IGNORE INTO zone_sao_luu_51 SELECT * FROM zone WHERE @da_chup = 0;


-- ------------------------------------------------------- gán lại zone_id
-- kind='Mechanical' loại 6 block đỗ nền 901..906 ra khỏi mọi câu lệnh:
-- CL1 chỉ liệt kê block cơ khí, zone của đỗ nền phải giữ nguyên.

UPDATE block SET zone_id = 1
 WHERE kind = 'Mechanical' AND block_no IN (86, 96);

UPDATE block SET zone_id = 4
 WHERE kind = 'Mechanical' AND block_no IN (39, 48, 49, 50, 60, 76, 77, 78, 79);

UPDATE block SET zone_id = 5
 WHERE kind = 'Mechanical' AND block_no IN (29, 30, 31, 32, 58, 59, 61, 62);

UPDATE block SET zone_id = 6
 WHERE kind = 'Mechanical' AND block_no IN (21, 22, 23, 63, 64, 65, 66, 67, 68, 69);

-- Tổng: 2 + 9 + 8 + 10 = 29 block.


-- ------------------------------------------------------- tên zone theo CT
-- Công thức khách chốt:  Zone n (Hầm Bn CLx)
-- Cụm này là CL1 ở cả 6 zone. Tên cũ trong CSDL chỉ là "Zone 1".."Zone 6";
-- tên dài sai ("Bãi ngoài trời A", "Khối cao tầng 2") nằm ở danh sách viết
-- cứng trong OperationControl.cshtml và ZoneDetail.cshtml, sửa kèm theo.
-- Không có bãi ngoài trời trong công trình này.

UPDATE zone SET name = 'Zone 1 (Hầm B1 CL1)' WHERE zone_id = 1;
UPDATE zone SET name = 'Zone 2 (Hầm B2 CL1)' WHERE zone_id = 2;
UPDATE zone SET name = 'Zone 3 (Hầm B3 CL1)' WHERE zone_id = 3;
UPDATE zone SET name = 'Zone 4 (Hầm B4 CL1)' WHERE zone_id = 4;
UPDATE zone SET name = 'Zone 5 (Hầm B5 CL1)' WHERE zone_id = 5;
UPDATE zone SET name = 'Zone 6 (Hầm B6 CL1)' WHERE zone_id = 6;


-- ===================== HOÀN TÁC =====================
--
-- UPDATE một cột là nguyên tử nên file này không cần giao dịch. Nếu cần trả
-- về đúng trạng thái trước khi chạy:
--
--   UPDATE block b JOIN block_sao_luu_51 s USING (block_id)
--      SET b.zone_id = s.zone_id;
--   UPDATE zone z JOIN zone_sao_luu_51 s USING (zone_id)
--      SET z.name = s.name;
--
-- Không dùng RENAME TABLE: bản chụp không mang khoá ngoại fk_block_zone,
-- fk_block_lane_node của bảng gốc.
