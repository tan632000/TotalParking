USE total_parking;
-- Cung quy tac loc voi CarLocatorService: mot ma the chi duoc nam o DUNG MOT
-- block. Ma nao xuat hien o nhieu block la rac (ladder noi bo), khong phai xe.
-- KHONG con doi hoi the phai dang ky trong parking_card: the test ngoai hien
-- truong khong co trong danh sach do.
CREATE OR REPLACE VIEW v_car_location AS
SELECT s.card_code,
       c.card_no,
       b.block_no,
       b.zone_id,
       b.block_id,
       s.slot_index,
       s.word_addr,
       (c.card_id IS NOT NULL) AS the_dang_ky,
       s.read_at,
       s.changed_at
FROM   plc_slot_state s
JOIN   block b        ON b.block_id  = s.block_id
LEFT   JOIN parking_card c ON c.card_code = s.card_code
WHERE  s.card_code IS NOT NULL
  AND  (SELECT COUNT(DISTINCT s2.block_id) FROM plc_slot_state s2
        WHERE s2.card_code = s.card_code) = 1;

SELECT * FROM v_car_location ORDER BY changed_at DESC;
