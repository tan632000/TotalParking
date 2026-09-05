using System;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Lưu kết quả phân loại. Ghi đè theo event_id: ngưỡng trong Web.config có thể
    // đổi khi số liệu khoang thi công được xác nhận, và khi đó hồ sơ phải được
    // tính lại chứ không phải tích thêm bản ghi.
    public class VehicleProfileRepository
    {
        private const string UpsertSql =
            "INSERT INTO vehicle_profile " +
            "  (event_id, classified_at, rejected, reject_reason, requires_manual, manual_reason, " +
            "   lane, weight_class, required_pallet_kg, estimated_loaded_kg, effective_width_mm) " +
            "VALUES " +
            "  (@event_id, @classified_at, @rejected, @reject_reason, @requires_manual, @manual_reason, " +
            "   @lane, @weight_class, @required_pallet_kg, @estimated_loaded_kg, @effective_width_mm) AS new " +
            "ON DUPLICATE KEY UPDATE " +
            "  classified_at       = new.classified_at, " +
            "  rejected            = new.rejected, " +
            "  reject_reason       = new.reject_reason, " +
            "  requires_manual     = new.requires_manual, " +
            "  manual_reason       = new.manual_reason, " +
            "  lane                = new.lane, " +
            "  weight_class        = new.weight_class, " +
            "  required_pallet_kg  = new.required_pallet_kg, " +
            "  estimated_loaded_kg = new.estimated_loaded_kg, " +
            "  effective_width_mm  = new.effective_width_mm";

        public void Save(VehicleProfile p)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = UpsertSql;
                Add(cmd, "@event_id",            p.EventId);
                Add(cmd, "@classified_at",       DateTime.Now);
                Add(cmd, "@rejected",            p.Rejected ? 1 : 0);
                Add(cmd, "@reject_reason",       Clip(p.RejectReason));
                Add(cmd, "@requires_manual",     p.RequiresManual ? 1 : 0);
                Add(cmd, "@manual_reason",       Clip(p.ManualReason));
                Add(cmd, "@lane",                p.Lane.ToString());
                Add(cmd, "@weight_class",        p.WeightClass);
                Add(cmd, "@required_pallet_kg",  p.RequiredPalletKg);
                Add(cmd, "@estimated_loaded_kg", p.EstimatedLoadedWeightKg);
                Add(cmd, "@effective_width_mm",  p.EffectiveWidthMm);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Lý do là chuỗi ghép động, cột chỉ 255 ký tự. Cắt ở đây để một lý do dài
        // bất thường không làm hỏng cả lần ghi.
        private static string Clip(string value)
        {
            if (value == null) return null;
            return value.Length <= 255 ? value : value.Substring(0, 255);
        }

        private static void Add(MySqlCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
