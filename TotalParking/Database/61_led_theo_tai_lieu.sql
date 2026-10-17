-- =========================================================================
-- ĐÃ VÔ HIỆU HOÁ — KHÔNG LÀM GÌ
-- =========================================================================
--
-- File này từng đưa bảng LED 51, 53, 57, 65 về đúng tài liệu giao thức
-- (P1 = cổng 0 = cột trái). Ngày 14/10/2026 khách báo bốn bảng đó bị bên thiết
-- kế đấu cáp ngược vị trí và nhờ bù trong phần mềm:
-- 63_led_dao_trai_phai_dau_nguoc.sql đảo lại đúng bốn bảng này.
--
-- Nội dung cũ đảo bảng khi cổng 0 mang PHẢI — đúng trạng thái sau file 63 — nên
-- chạy lại nó sẽ phá việc bù đấu nối ngược. Phần thân đã bị gỡ.
--
-- Trạng thái đúng hiện nay do:
--     60_led_bang_2_huong_co_thang.sql      bảng 58, 66 (theo tài liệu)
--     63_led_dao_trai_phai_dau_nguoc.sql    bảng 51, 53, 57, 65 (bù đấu ngược)
--
-- Nội dung cũ còn trong lịch sử git.

SELECT '61_led_theo_tai_lieu.sql da vo hieu hoa, xem 60 va 63' AS ghi_chu;
