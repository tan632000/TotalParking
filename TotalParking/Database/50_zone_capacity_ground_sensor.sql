-- =========================================================================
-- v_zone_capacity biết số ô đỗ thường còn trống, DÙNG CHUNG công thức với LED
-- =========================================================================
--
-- VehicleRouting.FreeGround hiện trả về nguyên TotalGround, kèm ghi chú:
--
--     "Hệ thống không quan sát được ô nền, nên câu trả lời trung thực là
--      còn nguyên sức chứa, không phải một số bịa."
--
-- Lúc viết dòng đó thì đúng. Migration 48 và 49 làm tiền đề ấy sai: cảm biến
-- PGS quan sát được từng ô đỗ thường, và pgs_sensor_map biết ô nào thuộc zone
-- nào. Giữ công thức cũ bây giờ nghĩa là trang Điều hướng xe hiện sức chứa còn
-- bảng LED hiện số đo — hai màn hình nói hai điều về cùng một bãi.
--
-- ===================== VÌ SAO KHÔNG TỰ TÍNH LẠI =====================
--
-- Bản đầu của file này tính "total_ground - số ô có xe". Đo ra lệch với bảng
-- LED: zone 1 ra 12 trong khi LED ra 8. Lý do là ô LỖI. Bảng LED ĐẾM ô báo
-- trống, nên ô lỗi không được tính; phép trừ thì coi ô lỗi là trống.
--
-- Người dùng chốt ngày 24/09: ô lỗi và ô không lắp KHÔNG tính là trống. Báo
-- thiếu một chút, đổi lại tài xế đi theo bảng thì chắc chắn có chỗ. Nên cách
-- đúng là bảng LED, và cách chắc chắn không lệch lại là ĐỌC THẲNG con số của
-- bảng LED thay vì viết một công thức thứ hai tương đương.
--
-- ẢNH HƯỞNG TỚI ĐỊNH TUYẾN: ZoneRouter dùng FreeGround để quyết xe hạng THƯỜNG
-- vào được zone nào. Trước đây nó luôn thấy còn nguyên sức chứa nên KHÔNG BAO
-- GIỜ từ chối. Sau thay đổi này, một zone hết ô đỗ thường sẽ bị loại đúng lúc.
-- Đây là thay đổi hành vi có chủ ý, không phải tác dụng phụ.
--
-- DỰ PHÒNG: v_led_capacity_zone đã tự trả về nguyên sức chứa khi không còn cảm
-- biến tươi, nên vòng ghi chết thì FreeGround quay về đúng hành vi cũ.

USE total_parking;

CREATE OR REPLACE VIEW v_zone_capacity AS
SELECT  z.zone_id, z.code, z.gate_rank,
        SUM(CASE WHEN b.kind = 'Mechanical' THEN b.slot_count   ELSE 0 END) AS total_mech,
        SUM(CASE WHEN b.kind = 'Mechanical' THEN b.column_count ELSE 0 END) AS total_tier0,
        SUM(CASE WHEN b.kind = 'Ground'     THEN b.slot_count   ELSE 0 END) AS total_ground,
        -- Loc b2.is_active = 1 cho khop voi phep tinh total_* o tren: mot block
        -- bi tat phai bien mat khoi CA suc chua LAN so o dang dung, khong thi
        -- zone do se bao am so cho trong.
        (SELECT COUNT(*)
         FROM   v_slot_taken t
         JOIN   block b2 ON b2.block_id = t.block_id
         WHERE  b2.zone_id = z.zone_id AND b2.is_active = 1)                 AS in_use,
        -- Đọc thẳng số của bảng LED. Một công thức, một kết quả.
        COALESCE((SELECT c.free_standard FROM v_led_capacity_zone c
                   WHERE c.zone_id = z.zone_id), 0)                          AS ground_free,
        COALESCE((SELECT c.used_standard FROM v_led_capacity_zone c
                   WHERE c.zone_id = z.zone_id), 0)                          AS ground_in_use
FROM    zone z
LEFT JOIN block b ON b.zone_id = z.zone_id AND b.is_active = 1
WHERE   z.is_active = 1
GROUP BY z.zone_id, z.code, z.gate_rank;

-- ------------------------------------------------------------------ đối chiếu
SELECT v.zone_id, v.code, v.total_ground, v.ground_in_use, v.ground_free,
       c.free_standard AS led_hien,
       (v.ground_free = c.free_standard) AS khop_voi_led
FROM   v_zone_capacity v
JOIN   v_led_capacity_zone c ON c.zone_id = v.zone_id
ORDER  BY v.zone_id;
