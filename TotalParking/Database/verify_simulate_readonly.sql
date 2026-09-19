-- Doi chieu: goi /Monitor/Simulate KHONG duoc ghi gi vao co so du lieu.
-- Chi DOC, khong sua gi. UTF-8 KHONG BOM.
--
-- Cach dung: chay file nay, goi Simulate, roi chay lai file nay. Moi con so o
-- dong "Chu ky" phai giong het giua hai lan.
--
-- Vi sao khong chi dem so dong: quyet dinh dieu huong duoc ghi bang
-- INSERT ... ON DUPLICATE KEY UPDATE tren khoa event_id. Mot lan ghi de len dong
-- san co KHONG lam so dong thay doi. Nen phai kem theo moc thoi gian moi nhat va
-- tong kiem cua cot block_no; chung doi ngay ca khi so dong dung yen.

USE total_parking;

SELECT 'Chu ky truoc/sau khi goi Simulate' AS kiem_tra,
       (SELECT COUNT(*) FROM vehicle_event)    AS so_dong_vehicle_event,
       (SELECT COUNT(*) FROM vehicle_profile)  AS so_dong_vehicle_profile,
       (SELECT COUNT(*) FROM vehicle_routing)  AS so_dong_vehicle_routing,
       (SELECT IFNULL(MAX(received_at), '-') FROM vehicle_event)   AS su_kien_moi_nhat,
       (SELECT IFNULL(MAX(decided_at), '-')  FROM vehicle_routing) AS quyet_dinh_moi_nhat,
       (SELECT IFNULL(SUM(block_no), 0)      FROM vehicle_routing) AS tong_block_no,
       (SELECT COUNT(*) FROM vehicle_routing WHERE event_id = 'SIM') AS dong_mang_event_id_SIM;

-- Endpoint Simulate dung event_id gia la 'SIM' khi goi ZoneRouter. Con so cuoi
-- cung o tren phai luon la 0: neu mot dong 'SIM' xuat hien trong bang thi trang
-- xem truoc da ghi that, va no se lam lech ca phep tru suat cua D9.
