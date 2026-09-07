using System;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Quyết định trả lời một lượt quẹt thẻ tại HMI của một block.
    //
    // Đây là nơi quy tắc "1 RFID = 1 phiên đang mở" được thi hành. Kết quả ánh xạ
    // thẳng sang hai thanh ghi:
    //   W75.0 <- Permit           1 = đúng block, 0 = sai block hoặc lỗi
    //   D402  <- WeightClassValue 2200 hoặc 2600
    //
    // Ba lớp chống hai phiên cùng thẻ, theo thứ tự từ ngoài vào:
    //   1. Vòng đọc chỉ xử lý khi PLC báo có lượt quẹt mới (PlcConnection).
    //   2. SELECT ... FOR UPDATE trên đúng dòng thẻ — hai lượt quẹt cùng thẻ bị
    //      tuần tự hoá tại đây.
    //   3. uq_session_active_card trong DB — quyền phán quyết cuối cùng.
    public class CardScanService
    {
        // MySQL 1062 = ER_DUP_ENTRY. Day la ma bao uq_session_active_card chan.
        private const int DuplicateKeyErrorNumber = 1062;

        // Khoá hàng thẻ trước, rồi mới đọc phiên. Khoá gắn với transaction nên tự
        // nhả khi commit/rollback/mất kết nối — không dùng GET_LOCK vì phải nhớ
        // RELEASE_LOCK và nó rò khi connection quay lại pool.
        private const string LockCardSql =
            "SELECT c.card_id, w.code AS weight_class, c.is_active " +
            "FROM   parking_card c " +
            "JOIN   weight_class w ON w.weight_class_id = c.weight_class_id " +
            "WHERE  c.card_code = @code " +
            "FOR UPDATE";

        private const string ActiveSessionSql =
            "SELECT session_id, block_id, status " +
            "FROM   parking_session " +
            "WHERE  active_card_id = @card_id";

        public CardScanDecision Evaluate(int blockId, string cardCode)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
                return CardScanDecision.Deny(RejectReason.UnknownCode);

            try
            {
                using (var conn = new MySqlConnection(Db.ConnectionString))
                {
                    conn.Open();
                    // REPEATABLE READ là mặc định của MySQL, và locking read luôn
                    // đọc bản mới nhất đã commit nên không dính bẫy snapshot cũ.
                    using (var tx = conn.BeginTransaction())
                    {
                        var decision = Decide(conn, tx, blockId, cardCode.Trim());
                        tx.Commit();
                        return decision;
                    }
                }
            }
            catch (MySqlException)
            {
                // Mất DB nghĩa là mất khả năng biết thẻ đã gửi ở đâu chưa. Cho qua
                // khi không biết chính là lỗi mà toàn bộ cơ chế này sinh ra để chặn,
                // nên fail-closed.
                return CardScanDecision.Deny(RejectReason.DbUnavailable);
            }
            catch (InvalidOperationException)
            {
                // Pool cạn, connection string sai, DB chưa dựng — cùng nhóm với trên.
                return CardScanDecision.Deny(RejectReason.DbUnavailable);
            }
        }

        private static CardScanDecision Decide(
            MySqlConnection conn, MySqlTransaction tx, int blockId, string cardCode)
        {
            int    cardId;
            string weightClass;
            bool   cardActive;

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = LockCardSql;
                cmd.Parameters.AddWithValue("@code", cardCode);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return CardScanDecision.Deny(RejectReason.InvalidCard);
                    cardId      = Convert.ToInt32(r["card_id"]);
                    weightClass = Convert.ToString(r["weight_class"]);
                    cardActive  = Convert.ToBoolean(r["is_active"]);
                }
            }

            if (!cardActive) return CardScanDecision.Deny(RejectReason.InactiveCard);

            long? sessionId    = null;
            int?  sessionBlock = null;

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = ActiveSessionSql;
                cmd.Parameters.AddWithValue("@card_id", cardId);

                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        sessionId    = Convert.ToInt64(r["session_id"]);
                        sessionBlock = r["block_id"] == DBNull.Value
                                           ? (int?)null
                                           : Convert.ToInt32(r["block_id"]);
                    }
                }
            }

            int classValue = ClassWordFor(weightClass);

            // Không có phiên nào đang mở: khách gửi xe mới, block nào cũng hợp lệ.
            // MỞ PHIÊN NGAY TẠI ĐÂY, trong cùng transaction đang giữ khoá hàng thẻ.
            //
            // Đây là giải pháp tạm. Đúng ra phiên chỉ nên mở khi PLC xác nhận xe đã
            // nằm trên pallet — "phân bổ chỉ là ý định, chỉ PLC chốt được sự thật".
            // Nhưng hợp đồng thanh ghi hiện chỉ có D100/D402/W75.0, không có tín
            // hiệu nào báo chu trình cất xe đã xong. Không mở phiên ở đây thì
            // parking_session mãi rỗng, và W75.0 mãi bằng 1 — tức toàn bộ cơ chế
            // chống nhầm block không hoạt động.
            //
            // Cái giá phải trả: khách quẹt thẻ rồi lái đi luôn sẽ để lại một phiên
            // treo, khoá thẻ đó cho tới khi có người huỷ tay:
            //     UPDATE parking_session SET status = 'CANCELLED'
            //      WHERE session_id = ? AND status = 'PARKING';
            // Khi ladder có tín hiệu chốt xong thì chuyển điểm mở phiên sang đó.
            if (!sessionId.HasValue)
            {
                long? newId = TryOpenSession(conn, tx, cardId, blockId);
                if (!newId.HasValue)
                {
                    // uq_session_active_card chặn: có lượt quẹt khác cùng thẻ vừa
                    // chen vào. Không cho gửi — ràng buộc DB là quyền phán quyết
                    // cuối cùng, kể cả khi khoá hàng ở trên đáng lẽ đã ngăn được.
                    return CardScanDecision.Deny(RejectReason.ConcurrentScan);
                }

                return new CardScanDecision
                {
                    Permit           = true,
                    WeightClassValue = classValue,
                    IsRetrieval      = false,
                    SessionId        = newId
                };
            }

            // Có phiên mở tại chính block này: khách quay lại lấy xe. Cũng là
            // Permit = true, nhưng là nghiệp vụ khác — W75.0 một mình không phân
            // biệt được hai trường hợp, HMI phải tự suy ra từ ngữ cảnh của nó.
            if (sessionBlock.HasValue && sessionBlock.Value == blockId)
            {
                // Đóng phiên: "lấy xe thì giải phóng block". Cũng là giải pháp tạm
                // vì không có tín hiệu từ PLC báo xe đã ra thật — ở đây coi lần
                // quẹt thứ hai tại đúng block là lấy xe.
                //
                // Hệ quả cần biết: khách lỡ tay quẹt hai lần tại cùng block sẽ đóng
                // phiên trong khi xe vẫn nằm trong đó, và thẻ được tự do gửi tiếp.
                // Một bit không mang đủ thông tin để phân biệt hai trường hợp này.
                CloseSession(conn, tx, sessionId.Value);

                return new CardScanDecision
                {
                    Permit           = true,
                    // Lúc lấy xe hạng tải không còn liên quan — ô đang giữ xe đã
                    // cố định. Vẫn ghi giá trị của thẻ để D402 không mang giá trị
                    // cũ của khách trước.
                    WeightClassValue = classValue,
                    IsRetrieval      = true,
                    SessionId        = sessionId
                };
            }

            // Có phiên mở ở block khác — đây chính là ca mà W75.0 sinh ra để chặn.
            var deny = CardScanDecision.Deny(RejectReason.WrongBlock);
            deny.SessionId = sessionId;
            return deny;
        }

        // Mở phiên. zone_id lấy từ block để không phải truyền thêm tham số và để
        // hai bảng không lệch nhau khi block được chuyển zone.
        //
        // status = PARKING chứ không phải PARKED: đã cấp quyền cho chu trình cất
        // xe chạy, nhưng chưa có gì xác nhận xe đã nằm trên pallet. Khi hợp đồng
        // thanh ghi có tín hiệu chốt xong thì bước đó mới chuyển sang PARKED.
        //
        // Trả null nếu uq_session_active_card chặn.
        private static long? TryOpenSession(
            MySqlConnection conn, MySqlTransaction tx, int cardId, int blockId)
        {
            const string sql =
                "INSERT INTO parking_session " +
                "  (card_id, status, zone_id, block_id, created_at, updated_at) " +
                "SELECT @card_id, 'PARKING', b.zone_id, b.block_id, @now, @now " +
                "FROM   block b WHERE b.block_id = @block_id";

            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@card_id", cardId);
                    cmd.Parameters.AddWithValue("@block_id", blockId);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now);

                    if (cmd.ExecuteNonQuery() == 0) return null; // block_id không tồn tại
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "SELECT LAST_INSERT_ID()";
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
            catch (MySqlException ex) when (ex.Number == DuplicateKeyErrorNumber)
            {
                return null;
            }
        }

        private static void CloseSession(
            MySqlConnection conn, MySqlTransaction tx, long sessionId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "UPDATE parking_session " +
                    "SET    status = 'COMPLETED', completed_at = @now, updated_at = @now " +
                    "WHERE  session_id = @id";
                cmd.Parameters.AddWithValue("@id", sessionId);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.ExecuteNonQuery();
            }
        }

        // THUONG (đỗ nền) không dùng pallet cơ khí. Nó không có giá trị D402 riêng,
        // nên ghi giá trị hạn chế nhất: nếu vì lý do nào đó xe vẫn tới được HMI của
        // một block cơ khí thì tầng trên vẫn bị ẩn.
        private static int ClassWordFor(string weightClass)
        {
            if (weightClass == WeightClassCode.Max2200) return WeightClassWord.Max2200;
            if (weightClass == WeightClassCode.Max2600) return WeightClassWord.Max2600;
            return WeightClassWord.Restricted;
        }
    }
}
