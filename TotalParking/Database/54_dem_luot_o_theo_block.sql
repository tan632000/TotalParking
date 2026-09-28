-- =========================================================================
-- Đếm số lượt gửi/lấy xe theo từng khối
-- =========================================================================
--
-- Vòng quét ô đỗ ghi một dòng `parking_event` mỗi lần ô đổi trạng thái
-- (SlotOccupancyReader.GhiSuKienDoiO). View này gộp chúng lại theo khối.
--
-- ===================== ĐÂY KHÔNG PHẢI SỐ LẦN MOTOR CHẠY =====================
--
-- Khách hỏi "số lần hoạt động motor của các block". View này KHÔNG trả lời được
-- câu đó, và đừng dùng nó để lập lịch bảo trì theo chu kỳ.
--
-- Hệ thống là puzzle parking: lấy một xe ra thì cơ cấu phải dịch chuyển nhiều
-- khay khác để mở đường. Những lần chạy motor đó không làm đổi mã thẻ ô nào nên
-- vòng quét không nhìn thấy. Bảng vật tư CL1 còn cho thấy mỗi khối có HAI motor
-- riêng — "Motor trượt" và "Motor hàng rào" — chạy số lần khác nhau mà một con
-- số gộp không tách được.
--
-- Thêm một điểm mù: vòng quét chạy 45 giây một lượt (`plc:slotScanMs`), nên gửi
-- rồi lấy trong cùng cửa sổ sẽ mất dấu hoàn toàn.
--
-- Vì vậy con số ở đây LUÔN THẤP HƠN số lần motor chạy thật, và thấp bao nhiêu
-- thì không đo được. Muốn đếm đúng thì PLC phải tự đếm rồi phơi ra một thanh
-- ghi: nó là bên duy nhất biết motor chạy mấy lần, và bộ đếm nằm trong PLC thì
-- không mất khi SCADA khởi động lại.
--
-- Dùng được cho: khối nào bận, phân bố tải giữa các khối, và đối chiếu với bộ
-- đếm thật khi nào bên ladder cấp.
--
-- ===================== VÌ SAO KHÔNG DÙNG parking_slot.cycle_count =====================
--
-- Cột đó đã có sẵn từ `05_parking_topology.sql` nhưng bảng `parking_slot` chưa
-- bao giờ được gieo (0 dòng) và không mã nào tăng nó. Quan trọng hơn: nó là một
-- con số cộng dồn, không có dấu thời gian, nên không trả lời được "tháng này
-- khối nào bận" — mà đó mới là thứ dùng được. `parking_event` là bảng
-- append-only có `occurred_at`, hỏi theo khoảng thời gian nào cũng được.


-- Chỉ gộp sự kiện do vòng quét sinh ra. Lọc theo cả `actor` lẫn `event_type` để
-- sau này bảng này mang thêm loại sự kiện khác thì view vẫn đúng.
CREATE OR REPLACE VIEW v_luot_o_theo_block AS
SELECT b.block_no,
       b.zone_id,
       b.kind,
       COUNT(*)                                  AS tong_luot,
       SUM(e.event_type = 'SLOT_STORE')          AS luot_gui,
       SUM(e.event_type = 'SLOT_RETRIEVE')       AS luot_lay,
       -- Cả trước lẫn sau đều có thẻ nhưng khác nhau: một lượt lấy và một lượt
       -- gửi lọt vào cùng cửa sổ quét 45 giây. Đếm riêng để thấy được mức độ
       -- quan sát thiếu, thay vì trộn vào hai cột trên.
       SUM(e.event_type = 'SLOT_SWAP')           AS luot_gop,
       COUNT(DISTINCT e.detail->>'$.slot_index') AS so_o_tung_doi,
       MIN(e.occurred_at)                        AS lan_dau,
       MAX(e.occurred_at)                        AS lan_cuoi
FROM   parking_event e
JOIN   block b ON b.block_id = e.block_id
WHERE  e.actor = 'PLC'
  AND  e.event_type IN ('SLOT_STORE', 'SLOT_RETRIEVE', 'SLOT_SWAP')
GROUP  BY b.block_id, b.block_no, b.zone_id, b.kind;


-- ------------------------------------------------------------------ đối chiếu
SELECT COUNT(*) AS so_khoi_da_co_luot FROM v_luot_o_theo_block;

SELECT block_no, zone_id, tong_luot, luot_gui, luot_lay, luot_gop, lan_cuoi
FROM   v_luot_o_theo_block
ORDER  BY tong_luot DESC, block_no
LIMIT  15;
