using System;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Nhật ký từng lượt quẹt thẻ: SCADA đọc được gì và đã trả lời HMI ra sao.
    //
    // Ghi hai giá trị ĐÃ GHI xuống W75.0 và D402, không suy lại từ trạng thái
    // phiên. Khi truy vết sự cố, cái cần biết là SCADA đã nói gì với HMI, không
    // phải cái đáng lẽ nó nên nói.
    public class PlcRequestRepository
    {
        private const string InsertSql =
            "INSERT INTO plc_request " +
            "  (block_id, raw_words, card_code, received_at, " +
            "   result_permit, result_class, reject_reason, session_id, answered_at) " +
            "VALUES " +
            "  (@block_id, @raw_words, @card_code, @received_at, " +
            "   @result_permit, @result_class, @reject_reason, @session_id, @answered_at)";

        public void Log(int blockId, string rawWords, string cardCode,
                        DateTime receivedAt, CardScanDecision decision, DateTime? answeredAt)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = InsertSql;
                Add(cmd, "@block_id",      blockId);
                Add(cmd, "@raw_words",     rawWords ?? "");
                Add(cmd, "@card_code",     cardCode);
                Add(cmd, "@received_at",   receivedAt);
                Add(cmd, "@result_permit", decision == null ? (object)null : decision.Permit);
                Add(cmd, "@result_class",  decision == null ? (object)null : decision.WeightClassValue);
                Add(cmd, "@reject_reason", decision == null ? null : decision.RejectReason);
                Add(cmd, "@session_id",    decision == null ? null : decision.SessionId);
                Add(cmd, "@answered_at",   answeredAt);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static void Add(MySqlCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
