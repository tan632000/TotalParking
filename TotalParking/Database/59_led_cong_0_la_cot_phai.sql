-- =========================================================================
-- ĐÃ VÔ HIỆU HOÁ — KHÔNG LÀM GÌ
-- =========================================================================
--
-- File này từng đảo thứ tự cổng các bảng LED 51/53/57/58/65/66 theo kết luận
-- "cổng 0 là cột phải", suy từ ảnh hiện trường của bảng 51, 57, 65.
--
-- Người dùng chốt ngày 12/10/2026: tài liệu "Giao thức giao tiếp giữa máy tính
-- và bảng led hiển thị hướng" là chuẩn (P1 = cổng 0 = cột trái). Chạy lại nội
-- dung cũ sẽ đảo ngược đúng phần đã đưa về chuẩn, nên phần thân đã bị gỡ.
--
-- Trạng thái đúng hiện nay do:
--     60_led_bang_2_huong_co_thang.sql      bảng 58, 66
--     63_led_dao_trai_phai_dau_nguoc.sql    bảng 51, 53, 57, 65 (bù đấu ngược)
--
-- Nội dung cũ còn trong lịch sử git (commit 84f7eac).

SELECT '59_led_cong_0_la_cot_phai.sql da vo hieu hoa, xem 60 va 61' AS ghi_chu;
