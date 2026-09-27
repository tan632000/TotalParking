-- =========================================================================
-- Ghi nhận chỗ đỗ thường từ cảm biến PGS
-- =========================================================================
--
-- VÌ SAO CẦN BẢNG NÀY
--
-- `v_led_capacity.used_standard` đếm `v_slot_taken` nối với block kind='Ground'.
-- `v_slot_taken` dựng từ `plc_slot_state`, mà khu đỗ thường KHÔNG đi qua PLC và
-- KHÔNG phát thẻ, nên nó không bao giờ có dòng nào cho block Ground.
-- `used_standard` vì thế đứng yên ở 0 và `free_standard` luôn bằng sức chứa.
--
-- Đo ngày 27/09/2026: cảm biến đếm 17 xe đang đỗ, view vẫn trả về 80/80 trống.
--
-- Hệ quả: bảng LED đầu hầm phải đi đường vòng qua StandardFreeSource để lấy số
-- từ cảm biến, còn trang Điều hướng xe vẫn đọc view nên hiện số sai. Hai màn
-- hình nói hai điều khác nhau về cùng một bãi.
--
-- ĐỊA CHỈ CẢM BIẾN
--
-- Khung CCU (`$CCU,02,X1,X2,X3,Y1..Y32,Z1..Z32*CRC#`, tài liệu mục 2.2.2) mang
-- trạng thái hai lộ RS485, mỗi lộ 32 cảm biến. Một cảm biến vì thế định danh
-- được bằng bộ ba (zcu_id, lô, vị trí) — đúng cách bản vẽ
-- docs/IP_normal_parking_sensor.pdf đánh nhãn: "Z0,2.7" = ZCU 0, lô 2, vị trí 7.

CREATE TABLE IF NOT EXISTS pgs_sensor_state (
    zcu_id  TINYINT UNSIGNED NOT NULL,   -- 0..15 theo tài liệu CCU
    lo      TINYINT UNSIGNED NOT NULL,   -- 1 hoặc 2, ứng với hai lộ RS485
    vi_tri  TINYINT UNSIGNED NOT NULL,   -- 1..32, đếm từ 1 cho khớp bản vẽ

    -- 0 trống, 1 có xe, 2 lỗi, 3 không lắp. Giữ nguyên bảng mã của nhà cung cấp
    -- thay vì quy về boolean: "lỗi" và "không lắp" đều KHÁC "có xe" nhưng cũng
    -- không phải "trống", và gộp lại là mất đúng phần thông tin cần để biết bãi
    -- đang hỏng hay đang đầy.
    trang_thai TINYINT UNSIGNED NOT NULL,

    -- Vòng quét nào cũng cập nhật, kể cả khi trạng thái không đổi.
    -- Trả lời: "vòng đọc còn sống không".
    read_at    DATETIME(3) NULL,

    -- Chỉ đổi khi trang_thai đổi.
    -- Trả lời: "xe này đỗ bao lâu rồi", "cảm biến này chết từ bao giờ".
    --
    -- Tách hai cột vì gộp lại là mất một trong hai câu hỏi. Đã gặp đúng bài này
    -- ở plc_slot_state khi làm cảnh báo thẻ trùng.
    changed_at DATETIME(3) NULL,

    PRIMARY KEY (zcu_id, lo, vi_tri),
    KEY ix_pgs_sensor_state_trang_thai (trang_thai, read_at),
    KEY ix_pgs_sensor_state_changed (changed_at)
) ENGINE = InnoDB;


-- =========================================================================
-- Sức chứa đỗ thường, đọc từ cảm biến
-- =========================================================================
--
-- NGƯỠNG 5 PHÚT: CCU đẩy trọn một vòng 5 ZCU mỗi ~2,5 giây (đo 27/09/2026:
-- 2,0 gói/giây). 5 phút là đã bỏ lỡ khoảng 120 lượt liên tiếp — chắc chắn hỏng
-- chứ không phải chậm. Cùng ngưỡng mà ô cơ khí đang dùng.
--
-- CHỈ ĐẾM trang_thai = 0 là trống. Ô lỗi (2) và ô không lắp (3) KHÔNG tính.
-- Người dùng chốt ngày 24/09: báo thiếu một chút, đổi lại tài xế đi theo bảng
-- thì chắc chắn có chỗ.
CREATE OR REPLACE VIEW v_pgs_standard AS
SELECT COUNT(*)                                   AS tong_cam_bien,
       SUM(trang_thai = 0)                        AS trong,
       SUM(trang_thai = 1)                        AS co_xe,
       SUM(trang_thai = 2)                        AS loi,
       SUM(read_at >= NOW(3) - INTERVAL 5 MINUTE) AS con_tuoi,
       MAX(read_at)                               AS doc_gan_nhat
FROM   pgs_sensor_state
WHERE  trang_thai <> 3;


-- =========================================================================
-- v_led_capacity — chỉ thay nhánh Ground
-- =========================================================================
--
-- Hai dòng cơ khí (l5m, l48m) giữ NGUYÊN VĂN từ 30_led_car_length_nested.sql:
-- chúng lấy từ PLC qua v_slot_taken và đang đúng. Chỉ `used_standard` và
-- `free_standard` đổi nguồn sang cảm biến.
--
-- KHI CHƯA CÓ SỐ LIỆU TƯƠI thì trả về sức chứa từ bảng `block` như cũ, KHÔNG
-- trả về 0. Số 0 trên bảng LED nghĩa là "bãi đã đầy" — nói thế khi vòng đọc vừa
-- chết là đuổi tài xế đi khỏi một bãi đang trống. Giống cách LedPublisher xoá
-- sạch bảng khi chưa biết gì, thay vì đẩy `0 0 0`.
CREATE OR REPLACE VIEW v_led_capacity AS
SELECT
    t.total_l5m, t.total_l48m, t.total_standard,
    u.used_l5m,  u.used_l48m,
    COALESCE(s.co_xe, 0) AS used_standard,
    u.used_unassigned,
    GREATEST(CAST(t.total_l5m  AS SIGNED) - u.used_l5m,  0) AS free_l5m,
    GREATEST(CAST(t.total_l48m AS SIGNED) - u.used_l48m, 0) AS free_l48m,
    CASE WHEN s.con_tuoi IS NULL OR s.con_tuoi = 0
         THEN t.total_standard
         ELSE s.trong
    END AS free_standard,
    -- Độ phủ của riêng tầng cảm biến, để mọi nơi đọc view đều biết con số đỗ
    -- thường đang dựa trên bao nhiêu cảm biến còn tươi. Không có cột này thì
    -- "80 trống" lúc vòng đọc chết trông giống hệt "80 trống" lúc bãi trống.
    COALESCE(s.tong_cam_bien, 0) AS standard_sensors,
    COALESCE(s.con_tuoi, 0)      AS standard_fresh,
    s.doc_gan_nhat               AS standard_read_at,
    cv.slots_total, cv.slots_fresh
FROM (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN b.slot_count END),0) AS total_l5m,
        COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN b.slot_count END),0) AS total_l48m,
        COALESCE(SUM(CASE WHEN b.kind='Ground'                                THEN b.slot_count END),0) AS total_standard
    FROM block b WHERE b.is_active = 1
) t
CROSS JOIN (
    SELECT
        COALESCE(SUM(CASE WHEN b.kind='Mechanical' AND b.bay_length_mm > 4800 THEN 1 END),0) AS used_l5m,
        COALESCE(SUM(CASE WHEN b.kind='Mechanical'                            THEN 1 END),0) AS used_l48m,
        (SELECT COUNT(*) FROM parking_session ps
         WHERE ps.active_card_id IS NOT NULL AND ps.block_id IS NULL) AS used_unassigned
    FROM v_slot_taken s
    JOIN block b ON b.block_id = s.block_id AND b.is_active = 1
) u
CROSS JOIN (
    SELECT COALESCE(SUM(o_co_khi),0) AS slots_total,
           COALESCE(SUM(o_vua_doc),0) AS slots_fresh
    FROM   v_zone_coverage
) cv
LEFT JOIN v_pgs_standard s ON 1 = 1;


-- =========================================================================
-- CHƯA ĐỘNG TỚI: v_led_capacity_zone
-- =========================================================================
-- `free_standard` theo từng zone vẫn lấy từ sức chứa và vẫn sai. Sửa được nó
-- cần bảng ánh xạ (zcu_id, lô, vị trí) -> zone, mà ánh xạ đó hiện còn hai cảm
-- biến chưa phân định (Z3,1.1 và Z3,1.2 — thuộc ZCU 3 hay ZCU 4) và chưa có
-- ranh giới zone. Để nguyên còn hơn đoán: mười một bảng LED chỉ hướng đọc số
-- này, và chỉ sai đường một zone là tài xế chạy nhầm cả tầng.


-- ------------------------------------------------------------------ đối chiếu
SELECT 'truoc khi vong ghi chay' AS moc,
       total_standard, used_standard, free_standard,
       standard_sensors, standard_fresh
FROM   v_led_capacity;
