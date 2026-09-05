using System;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Lưu sự kiện xe từ Camera AI. event_id là khoá chính nên việc camera gửi
    // lại cùng một xe không tạo bản ghi thứ hai — chống trùng nằm ở tầng lưu
    // trữ, bền qua mọi lần app pool recycle.
    public class VehicleEventRepository
    {
        private const string InsertSql =
            "INSERT INTO vehicle_event " +
            "  (event_id, received_at, camera_ts, make, model, year_range, " +
            "   length_mm, width_mm, height_mm, weight_kg, category, image_path, raw_body) " +
            "VALUES " +
            "  (@event_id, @received_at, @camera_ts, @make, @model, @year_range, " +
            "   @length_mm, @width_mm, @height_mm, @weight_kg, @category, @image_path, @raw_body) " +
            "ON DUPLICATE KEY UPDATE event_id = event_id";

        // Trả về true nếu đây là sự kiện mới, false nếu là bản gửi lại của một
        // event_id đã có. Cả hai đều là kết quả thành công.
        public bool Insert(VehicleEvent e)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = InsertSql;
                Add(cmd, "@event_id",    e.EventId);
                Add(cmd, "@received_at", e.ReceivedAt);
                Add(cmd, "@camera_ts",   e.CameraTimestamp);
                Add(cmd, "@make",        e.Make);
                Add(cmd, "@model",       e.Model);
                Add(cmd, "@year_range",  e.YearRange);
                Add(cmd, "@length_mm",   e.LengthMm);
                Add(cmd, "@width_mm",    e.WidthMm);
                Add(cmd, "@height_mm",   e.HeightMm);
                Add(cmd, "@weight_kg",   e.WeightKg);
                Add(cmd, "@category",    e.Category);
                Add(cmd, "@image_path",  e.ImagePath);
                Add(cmd, "@raw_body",    e.RawBody);

                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        // AddWithValue nhan null cua C# khong dong nghia voi NULL cua SQL.
        private static void Add(MySqlCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
