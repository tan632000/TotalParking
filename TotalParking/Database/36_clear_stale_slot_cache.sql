-- Xoa BAN SAO (cache) mã thẻ test 62b2fee0 tại ba block đang mất kết nối.
-- MySQL 8.0.19+. Chạy lại nhiều lần an toàn. UTF-8 KHÔNG BOM.
-- Chỉ UPDATE -> tài khoản ứng dụng chạy được.
--
-- ======================= VÌ SAO CẦN FILE NÀY =======================
-- plc_slot_state KHÔNG phải sự thật, nó là bản sao lần đọc cuối từ PLC. Khi PLC
-- mất kết nối, SlotOccupancyReader không đọc được nên giữ nguyên giá trị cũ.
--
-- Ba block 73, 82, 109 đang mất kết nối lần lượt 161 / 1552 / 514 phút. Kỹ thuật
-- xóa tay thanh ghi tại HMI thì SCADA KHÔNG THẤY, vẫn tiếp tục tin là thẻ nằm ở
-- đó. Mà CarLocatorService loại mọi mã xuất hiện ở nhiều hơn một block, nên sau
-- khi quẹt tại block 71 thẻ sẽ ở 4 block -> tìm xe trả về 0.
--
-- ======================= VÌ SAO AN TOÀN =======================
-- SlotOccupancyReader.Save ghi ĐÈ cả card_code lẫn raw_words ở MỌI lần đọc, chỉ
-- changed_at mới có điều kiện. Nên khi ba PLC nối lại được, giá trị thật của
-- thanh ghi sẽ ghi đè lên những gì file này xóa -- không để lại sai lệch vĩnh
-- viễn. Nếu lúc đó thanh ghi vẫn còn mã thẻ thì dòng này quay lại y như cũ, và
-- đó là dấu hiệu HMI chưa được xóa tay.
--
-- KHÔNG đụng vào read_at và changed_at: để nguyên thì vẫn nhìn ra ba dòng này
-- đã cũ, không giả vờ là vừa đọc được từ PLC.
--
-- ======================= KHÔNG THAY THẾ VIỆC XÓA Ở HMI =======================
-- File này chỉ gỡ bản sao trong SCADA. Thanh ghi thật trong PLC vẫn còn mã thẻ
-- cho tới khi hiện trường xóa tay. Xem docs/xoa_thanh_ghi_test.txt.

UPDATE plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
SET    s.card_code = NULL,
       s.raw_words = '0000 0000'
WHERE  s.card_code = '62b2fee0'
  AND  b.block_no IN (73, 82, 109);

-- Kiểm tra: phải còn đúng 2 block (62, 112), cả hai đang online.
SELECT b.block_no, s.slot_index, CONCAT('D', s.word_addr) AS thanh_ghi, s.card_code
FROM   plc_slot_state s
JOIN   block b ON b.block_id = s.block_id
WHERE  s.card_code = '62b2fee0'
ORDER  BY b.block_no, s.slot_index;
