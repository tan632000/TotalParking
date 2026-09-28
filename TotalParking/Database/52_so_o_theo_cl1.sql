-- =========================================================================
-- Sửa số ô đỗ của 7 block theo CL1 và đồng bộ plc_slot_state
-- =========================================================================
--
-- Nguồn sự thật: docs/TTP_LUMI_TYPE_BLOCK_RE01.xlsx, sheet CL1. Tổng 764 ô,
-- khớp bảng model in trên bản vẽ ("112 bo / 764 cho" — xem 16_zone_fix_78_79.sql)
-- và đóng đúng khoảng lệch 9 ô mà 14_zone_blocks_from_customer.sql từng ghi nhận.
--
--     block 5, 29   :  5 -> 10   (thêm 5 ô mỗi block)
--     block 87, 89  :  3 -> 10   (thêm 7 ô mỗi block)
--     block 6, 33   : 10 ->  6   (bớt 4 ô mỗi block)
--     block 88      : 10 ->  3   (bớt 7 ô)
--     755 + 9 = 764
--
-- ===================== RÀNG BUỘC THỨ TỰ CHẠY =====================
--
-- Chạy SAU 51_zone_theo_cl1.sql. Cả hai phải chạy lại mỗi khi
-- 14_zone_blocks_from_customer.sql được chạy lại — file 14 có
-- `DELETE FROM block;` rồi INSERT lại slot_count viết cứng.
--
-- ===================== PHẦN NÀY XOÁ DỮ LIỆU =====================
--
-- 15 dòng plc_slot_state bị xoá (block 6 ô 7..10, block 33 ô 7..10,
-- block 88 ô 4..10). Ba lớp bảo vệ:
--
-- 1. BA VỊ TỪ AN TOÀN NẰM TRONG CÂU DELETE, không kiểm trước rồi xoá sau.
--    Vòng quét ô đỗ chạy 45 giây một lần bằng connection riêng, autocommit,
--    nằm ngoài giao dịch này — kiểm trước rồi xoá sau là cửa sổ TOCTOU thật.
--    Vị từ read_at là bắt buộc chứ không thừa: khi PLC rớt mạng,
--    SlotOccupancyReader bắt lỗi rồi bỏ qua, KHÔNG đụng card_code lẫn read_at.
--    Một xe cất bằng HMI trong lúc đó để lại card_code NULL cũ, và phép kiểm
--    chỉ nhìn card_code sẽ cho qua rồi xoá mất dấu chiếc xe đó.
--    Ngưỡng 5 phút lấy đúng theo BlockAllocator.PlcFreshMinutes.
--
-- 2. GIAO DỊCH BAO TRỌN DELETE -> INSERT -> UPDATE. Nếu DELETE xong mà UPDATE
--    hỏng, block 6 sẽ khai 10 ô nhưng chỉ còn 6 dòng: BlockAllocator tính chỗ
--    trống từ slot_count nên sẽ phát 4 suất vào 4 ô mà SCADA không đọc được.
--
-- 3. BẢN CHỤP TẠO TRƯỚC KHI MỞ GIAO DỊCH. CREATE TABLE gây implicit COMMIT
--    nên không được đặt trong giao dịch.
--
-- Kiểm ngay trước khi chạy (trạng thái này ĐỔI ĐƯỢC, đừng tin số đo hôm qua):
--     python tools/doi_chieu_zone_block.py --kiem-truoc-khi-xoa
-- Đối chiếu sau khi chạy:
--     python tools/doi_chieu_zone_block.py --phan o-do
--
-- KHÔNG chạy tools/xoa_thanh_ghi_o_do.py trong cửa sổ migration: công cụ đó
-- ghi xuống PLC và sẽ phá giá trị bằng chứng của changed_at.
--
-- Cần quyền DDL (tài khoản totalparking không có) -> chạy bằng root.
--
-- ===================== NGOÀI PHẠM VI =====================
--
-- column_count / tier_count vẫn để NULL. Cột D của CL1 cho số HÀNG (1 hoặc 2),
-- nhưng kích thước ngoài của block (ví dụ 1 hàng 5 ô = 5870x7660 với pallet
-- 5000x2500) đọc ra một cơ cấu 2 tầng kiểu xếp hình, tức tier_count = 2 ở mọi
-- block — mâu thuẫn với bất biến slot_count = tier_count * column_count.
-- Hai cách đọc cho total_tier0 khác nhau, nên phần này chờ bên thiết kế trả
-- lời: một block 5 ô (1 hàng, 5870x7660) có mấy chỗ ở TẦNG DƯỚI CÙNG?
-- Luật định tuyến 2600KG vẫn chưa chạy được cho tới lúc đó.


-- ----------------------------------------------------------------- bản chụp
-- Tạo TRƯỚC giao dịch: CREATE TABLE gây implicit COMMIT nên không đặt trong được.
-- Chỉ chụp khi bảng sao lưu còn RỖNG. INSERT IGNORE một mình là chưa đủ: nó
-- bỏ qua dòng trùng khoá, nhưng dòng MỚI sinh ra sau lần chạy đầu vẫn lọt vào,
-- làm câu hoàn tác tưởng chúng là dữ liệu gốc và không xoá nữa.
CREATE TABLE IF NOT EXISTS block_sao_luu_52 LIKE block;
SET @da_chup := (SELECT COUNT(*) FROM block_sao_luu_52);
INSERT IGNORE INTO block_sao_luu_52 SELECT * FROM block WHERE @da_chup = 0;

CREATE TABLE IF NOT EXISTS plc_slot_state_sao_luu_52 LIKE plc_slot_state;
SET @da_chup := (SELECT COUNT(*) FROM plc_slot_state_sao_luu_52);
INSERT IGNORE INTO plc_slot_state_sao_luu_52 SELECT * FROM plc_slot_state WHERE @da_chup = 0;


-- ------------------------------------------------------------- áp thay đổi
-- Gói trong thủ tục vì MySQL không cho IF/ROLLBACK ở mức câu lệnh rời: cần
-- đọc ROW_COUNT() rồi mới quyết định huỷ hay không.
DROP PROCEDURE IF EXISTS ap_dung_52;

DELIMITER $$
CREATE PROCEDURE ap_dung_52()
BEGIN
    DECLARE so_xoa   INT DEFAULT 0;
    DECLARE so_them  INT DEFAULT 0;
    DECLARE tong_o   INT DEFAULT 0;
    DECLARE so_lech  INT DEFAULT 0;

    -- Bất kỳ lỗi SQL nào cũng huỷ toàn bộ rồi ném lại nguyên văn.
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    -- 1) XOÁ ---------------------------------------------------------------
    DELETE s FROM plc_slot_state s JOIN block b USING (block_id)
     WHERE ( (b.block_no = 6  AND s.slot_index > 6)
          OR (b.block_no = 33 AND s.slot_index > 6)
          OR (b.block_no = 88 AND s.slot_index > 3) )
       AND s.card_code  IS NULL                              -- không giữ thẻ
       AND s.read_at    >= NOW(3) - INTERVAL 5 MINUTE        -- dữ liệu còn tươi
       AND s.changed_at IS NULL;                             -- chưa từng đổi
    SET so_xoa = ROW_COUNT();

    IF so_xoa <> 15 THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'HUY: DELETE khong xoa dung 15 dong. Chay tools/doi_chieu_zone_block.py --kiem-truoc-khi-xoa. Khong dong nao bi mat.';
    END IF;

    -- 2) THÊM --------------------------------------------------------------
    -- Thứ tự thanh ghi giữ nguyên theo 17_plc_slot_state.sql. Bốn block đều
    -- lên 10 ô nên ngưỡng viết thẳng là 10, KHÔNG suy từ slot_count hiện tại.
    -- ON DUPLICATE giữ nguyên card_code / read_at / changed_at của dòng cũ.
    INSERT INTO plc_slot_state (block_id, slot_index, word_addr)
    SELECT b.block_id, s.idx, s.addr
    FROM   block b
    JOIN (
        SELECT 1 AS idx, 400 AS addr UNION ALL SELECT  2, 202 UNION ALL
        SELECT 3, 204 UNION ALL SELECT  4, 206 UNION ALL SELECT  5, 208 UNION ALL
        SELECT 6, 300 UNION ALL SELECT  7, 302 UNION ALL SELECT  8, 304 UNION ALL
        SELECT 9, 306 UNION ALL SELECT 10, 308
    ) s ON s.idx <= 10
    WHERE  b.kind = 'Mechanical' AND b.block_no IN (5, 29, 87, 89)
    ON DUPLICATE KEY UPDATE word_addr = VALUES(word_addr);
    SET so_them = ROW_COUNT();

    -- 16 dòng đã có (5+5+3+3) trùng khoá và word_addr không đổi -> đếm 0.
    IF so_them <> 24 THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'HUY: INSERT khong them dung 24 dong. So o cua block 5/29/87/89 khac gia dinh 5/5/3/3.';
    END IF;

    -- 3) SỬA SỐ Ô ----------------------------------------------------------
    UPDATE block SET slot_count = 10
     WHERE kind = 'Mechanical' AND block_no IN (5, 29, 87, 89);
    UPDATE block SET slot_count = 6
     WHERE kind = 'Mechanical' AND block_no IN (6, 33);
    UPDATE block SET slot_count = 3
     WHERE kind = 'Mechanical' AND block_no = 88;

    -- 4) BẤT BIẾN TRƯỚC KHI CHỐT ------------------------------------------
    SELECT COALESCE(SUM(slot_count), 0) INTO tong_o
      FROM block WHERE kind = 'Mechanical' AND is_active = 1;
    IF tong_o <> 764 THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'HUY: tong so o co khi khong bang 764 sau khi sua.';
    END IF;

    -- Số dòng plc_slot_state phải khớp slot_count ở MỌI block cơ khí đang bật.
    -- Sửa slot_count mà quên plc_slot_state sẽ làm hai nguồn nói khác nhau.
    SELECT COUNT(*) INTO so_lech FROM (
        SELECT b.block_id
        FROM   block b
        LEFT   JOIN plc_slot_state s ON s.block_id = b.block_id
        WHERE  b.kind = 'Mechanical' AND b.is_active = 1
        GROUP  BY b.block_id, b.slot_count
        HAVING COUNT(s.slot_index) <> b.slot_count
    ) t;
    IF so_lech <> 0 THEN
        ROLLBACK;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT =
            'HUY: con block co so dong plc_slot_state khac slot_count.';
    END IF;

    COMMIT;
END$$
DELIMITER ;

CALL ap_dung_52();
DROP PROCEDURE ap_dung_52;


-- ------------------------------------------------------------------ đối chiếu
SELECT SUM(slot_count) AS tong_o_co_khi
  FROM block WHERE kind = 'Mechanical' AND is_active = 1;
SELECT COUNT(*) AS so_dong_o_do FROM plc_slot_state;


-- ===================== HOÀN TÁC =====================
--
-- Bản chụp không mang khoá ngoại của bảng gốc nên hoàn tác không dùng được
-- RENAME TABLE, phải là INSERT ... SELECT:
--
--   START TRANSACTION;
--   DELETE s FROM plc_slot_state s
--    WHERE NOT EXISTS (SELECT 1 FROM plc_slot_state_sao_luu_52 c
--                       WHERE c.block_id = s.block_id AND c.slot_index = s.slot_index);
--   INSERT INTO plc_slot_state SELECT * FROM plc_slot_state_sao_luu_52
--   ON DUPLICATE KEY UPDATE word_addr  = VALUES(word_addr),
--                           card_code  = VALUES(card_code),
--                           raw_words  = VALUES(raw_words),
--                           read_at    = VALUES(read_at),
--                           changed_at = VALUES(changed_at);
--   UPDATE block b JOIN block_sao_luu_52 c USING (block_id)
--      SET b.slot_count = c.slot_count;
--   COMMIT;
