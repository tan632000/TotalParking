-- 62_motor_run.sql — lịch sử lượt chạy động cơ của khối cơ khí.
-- MySQL 8. Chạy lại nhiều lần an toàn. UTF-8 KHÔNG BOM.
--
-- !! CẦN TÀI KHOẢN QUẢN TRỊ: có CREATE TABLE. Tài khoản ứng dụng chỉ có
-- !! SELECT/INSERT/UPDATE/DELETE và sẽ bị từ chối ở file này.
--
-- ===================== MỖI DÒNG LÀ MỘT LƯỢT CHẠY QUAN SÁT ĐƯỢC =====================
-- PlcIoPoller đọc CIO 100..102 mỗi plc:ioPollMs và DongCoDetector nhận lượt chạy
-- theo docs/NOTE FINAL.xlsx: bắt đầu khi bit động cơ ON cùng contactor nguồn và
-- (thuận hoặc nghịch); kết thúc khi bit động cơ OFF. Dòng chỉ được ghi khi lượt
-- chạy KẾT THÚC, nên không có dòng "đang chạy" nào nằm dở trong bảng.
--
-- Độ phân giải bằng nhịp đọc: lượt ngắn hơn một nhịp có thể không thấy; lượt
-- đang dở lúc ứng dụng khởi động lại hoặc lúc mất đọc quá 3 nhịp bị bỏ, không ghi
-- (xem specs/d3-loi-va-dem-dong-co/task-02-dem-dong-co.md).
--
-- block_no chứ không block_id: người đọc tra theo số khối, và giữ được lịch sử
-- kể cả khi bảng block được gieo lại (14_zone_blocks_from_customer.sql xoá hết).
--
-- duration_ms đo bằng Stopwatch giữa hai lần đọc, KHÔNG phải ended_at - started_at:
-- đồng hồ hệ thống có thể nhảy (NTP) trong lúc động cơ chạy.
--
-- Không xoá dòng: lịch sử chạy là thứ cần cho bảo trì về sau.

CREATE TABLE IF NOT EXISTS motor_run (
  run_id      BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  block_no    SMALLINT UNSIGNED NOT NULL,
  motor_code  VARCHAR(16)       NOT NULL COMMENT '201, 202, 102, RAO_CHAN, NAM_CHAM...',
  started_at  DATETIME(3)       NOT NULL,
  ended_at    DATETIME(3)       NOT NULL,
  duration_ms INT UNSIGNED      NOT NULL,
  PRIMARY KEY (run_id),
  KEY ix_motor_run_block (block_no, started_at),
  KEY ix_motor_run_ended (ended_at)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4;

-- Tài khoản ứng dụng đã có quyền trên cả schema (GRANT ... ON total_parking.*),
-- nên không cần GRANT riêng cho bảng mới.

SELECT COUNT(*) AS so_dong_motor_run FROM motor_run;
