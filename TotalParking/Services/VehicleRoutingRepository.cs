using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Đọc sức chứa zone và lưu/đọc quyết định điều hướng.
    public class VehicleRoutingRepository
    {
        private const string UpsertSql =
            "INSERT INTO vehicle_routing (event_id, decided_at, zone_id, outcome, reason) " +
            "VALUES (@event_id, @decided_at, @zone_id, @outcome, @reason) AS new " +
            "ON DUPLICATE KEY UPDATE " +
            "  decided_at = new.decided_at, zone_id = new.zone_id, " +
            "  outcome = new.outcome, reason = new.reason";

        // Ghi đè khi trùng event_id: quyết định điều hướng là dữ liệu dẫn xuất,
        // tính lại được bất cứ lúc nào. Camera gửi lại cùng event_id thì kết quả
        // mới nhất là kết quả đúng, không phải bản ghi thứ hai.
        public void Save(VehicleRouting routing)
        {
            if (routing == null || string.IsNullOrEmpty(routing.EventId)) return;

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = UpsertSql;
                Add(cmd, "@event_id",   routing.EventId);
                Add(cmd, "@decided_at", routing.DecidedAt);
                Add(cmd, "@zone_id",    routing.ZoneId);
                Add(cmd, "@outcome",    routing.Outcome);
                Add(cmd, "@reason",     routing.Reason);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        public IList<ZoneCapacity> GetZoneCapacity()
        {
            var result = new List<ZoneCapacity>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT zone_id, code, gate_rank, total_mech, total_tier0, " +
                    "       total_ground, in_use " +
                    "FROM   v_zone_capacity ORDER BY gate_rank, zone_id";

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        result.Add(new ZoneCapacity
                        {
                            ZoneId          = Convert.ToInt32(r["zone_id"]),
                            Code            = Convert.ToString(r["code"]),
                            GateRank        = Convert.ToInt32(r["gate_rank"]),
                            TotalMechanical = Num(r, "total_mech"),
                            TotalTier0      = Num(r, "total_tier0"),
                            TotalGround     = Num(r, "total_ground"),
                            InUse           = Num(r, "in_use")
                        });
                    }
                }
            }

            return result;
        }

        // Danh sách xe vừa vào, cho bảng theo dõi trên trang mặt bằng.
        public IList<RoutedVehicle> GetRecent(int limit)
        {
            var result = new List<RoutedVehicle>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT event_id, received_at, make, model, length_mm, weight_kg, " +
                    "       camera_category, lane, weight_class, outcome, reason, " +
                    "       zone_id, zone_code " +
                    "FROM   v_vehicle_routing " +
                    "ORDER  BY received_at DESC LIMIT @limit";
                cmd.Parameters.AddWithValue("@limit", limit);

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        result.Add(new RoutedVehicle
                        {
                            EventId     = Convert.ToString(r["event_id"]),
                            ReceivedAt  = Convert.ToDateTime(r["received_at"]),
                            Make        = Str(r, "make"),
                            Model       = Str(r, "model"),
                            LengthMm    = NullableInt(r, "length_mm"),
                            WeightKg    = NullableInt(r, "weight_kg"),
                            Category    = Str(r, "camera_category"),
                            Lane        = Str(r, "lane"),
                            WeightClass = Str(r, "weight_class"),
                            Outcome     = Str(r, "outcome"),
                            Reason      = Str(r, "reason"),
                            ZoneId      = NullableInt(r, "zone_id"),
                            ZoneCode    = Str(r, "zone_code")
                        });
                    }
                }
            }

            return result;
        }

        private static int Num(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }

        private static int? NullableInt(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        private static string Str(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }

        private static void Add(MySqlCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    // Một dòng cho bảng theo dõi xe vào.
    public class RoutedVehicle
    {
        public string   EventId     { get; set; }
        public DateTime ReceivedAt  { get; set; }
        public string   Make        { get; set; }
        public string   Model       { get; set; }
        public int?     LengthMm    { get; set; }
        public int?     WeightKg    { get; set; }
        public string   Category    { get; set; }
        public string   Lane        { get; set; }
        public string   WeightClass { get; set; }
        public string   Outcome     { get; set; }
        public string   Reason      { get; set; }
        public int?     ZoneId      { get; set; }
        public string   ZoneCode    { get; set; }
    }
}
