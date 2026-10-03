-- =========================================================================
-- Xác nhận các cảnh báo ĐÃ KẾT THÚC mà chưa ai bấm xác nhận
-- =========================================================================
--
-- Trang Alarms đang hiện 68 dòng chưa xác nhận. Trong đó 64 dòng là sự cố đã
-- kết thúc từ lâu — `het_luc` đã có — nhưng người vận hành lúc đó không bấm
-- xác nhận, nên chúng nằm lại vĩnh viễn trong ô "chưa xác nhận".
--
-- Hệ quả không phải chuyện thẩm mỹ: một màn hình lúc nào cũng đỏ là một màn
-- hình không ai đọc nữa. Khi 64 dòng chết lấn át 4 dòng sống, người trực sẽ
-- lướt qua cả 68 — và 4 cảm biến đang hỏng thật biến mất khỏi tầm mắt.
--
-- ===================== CHỈ ĐỤNG SỰ CỐ ĐÃ KẾT THÚC =====================
--
-- Điều kiện `het_luc IS NOT NULL` là chốt quan trọng nhất của file này.
--
-- Hiện có 6 cảnh báo ĐANG MỞ, trong đó 4 là cảm biến đỗ thường Z0,1.4 đến
-- Z0,1.7 hỏng từ 28/09 và vẫn chưa ai xuống hầm. Xác nhận chúng là xoá dấu
-- vết của một sự cố CHƯA được xử lý — đúng thứ mà cột xác nhận sinh ra để
-- ngăn. File này tuyệt đối không được chạm vào chúng.
--
-- ===================== VÌ SAO GHI 'System' =====================
--
-- Không ai thật sự nhìn vào 64 dòng đó, nên ghi tên một người là khai man sổ
-- sách. 'System' nói đúng điều đã xảy ra: máy đóng sổ, không phải người.
-- Trùng với tên mà CanhBaoRepository.NguoiXacNhanTuDong dùng khi PLC nối lại.
--
-- xac_nhan_ip để NULL: không có ai ở đầu nào cả.
--
-- ===================== CHẠY LẠI ĐƯỢC =====================
--
-- Điều kiện `xac_nhan_luc IS NULL` làm file này tự vô hiệu ở lần chạy thứ
-- hai: dòng đã xác nhận không khớp nữa. Chạy mười lần cũng như chạy một lần,
-- và KHÔNG ghi đè bản ghi của người đã bấm trước đó.
--
-- Chỉ UPDATE -> tài khoản ứng dụng chạy được, không cần quyền DDL.


-- ------------------------------------------------- đếm trước khi đổi
-- Kỳ vọng: da_dong_chua_xac_nhan = 64, dang_mo_chua_xac_nhan = 4.
-- Con số thứ hai phải KHÔNG đổi sau khi chạy.
SELECT SUM(het_luc IS NOT NULL AND xac_nhan_luc IS NULL) AS da_dong_chua_xac_nhan,
       SUM(het_luc IS NULL     AND xac_nhan_luc IS NULL) AS dang_mo_chua_xac_nhan,
       SUM(xac_nhan_luc IS NOT NULL)                     AS da_xac_nhan,
       COUNT(*)                                          AS tong
FROM   canh_bao;


-- ------------------------------------------------------------- đánh dấu
-- Dùng MỘT mốc thời gian chung cho cả lô thay vì NOW(3) từng dòng: lô này là
-- một thao tác dọn dẹp duy nhất, không phải 64 lần xác nhận rải rác. Mốc
-- chung cũng là thứ cho phép hoàn tác chính xác lô này mà không đụng những
-- dòng 'System' do vòng tự động ghi ra trước hay sau đó.
SET @luc := NOW(3);

UPDATE canh_bao
   SET xac_nhan_boi = 'System',
       xac_nhan_ip  = NULL,
       xac_nhan_luc = @luc
 WHERE het_luc      IS NOT NULL      -- chỉ sự cố ĐÃ kết thúc
   AND xac_nhan_luc IS NULL;         -- chưa ai bấm, không ghi đè người

SELECT ROW_COUNT() AS so_dong_da_danh_dau, @luc AS moc_thoi_gian_lo_nay;


-- ------------------------------------------------------------- đối chiếu
-- Kỳ vọng: da_dong_chua_xac_nhan = 0, dang_mo_chua_xac_nhan VẪN = 4.
SELECT SUM(het_luc IS NOT NULL AND xac_nhan_luc IS NULL) AS da_dong_chua_xac_nhan,
       SUM(het_luc IS NULL     AND xac_nhan_luc IS NULL) AS dang_mo_chua_xac_nhan,
       SUM(xac_nhan_luc IS NOT NULL)                     AS da_xac_nhan
FROM   canh_bao;

-- Bốn cảnh báo cảm biến phải còn nguyên, chưa xác nhận.
SELECT canh_bao_id, ma_loi, thiet_bi, xay_ra_luc, xac_nhan_boi
FROM   canh_bao
WHERE  het_luc IS NULL
ORDER  BY xay_ra_luc;


-- ===================== HOÀN TÁC =====================
--
-- Thay <moc> bằng giá trị `moc_thoi_gian_lo_nay` in ra ở trên. Điều kiện theo
-- mốc chứ không theo tên: vòng tự động cũng ghi 'System', nên lọc theo tên
-- không thôi sẽ xoá nhầm cả những lần đóng tự động hợp lệ.
--
--   UPDATE canh_bao
--      SET xac_nhan_boi = NULL, xac_nhan_ip = NULL, xac_nhan_luc = NULL
--    WHERE xac_nhan_boi = 'System' AND xac_nhan_luc = '<moc>';
