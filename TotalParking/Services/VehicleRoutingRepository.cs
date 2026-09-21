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
            "INSERT INTO vehicle_routing " +
            "  (event_id, decided_at, zone_id, outcome, reason, block_no, occupancy_verified) " +
            "VALUES (@event_id, @decided_at, @zone_id, @outcome, @reason, " +
            "        @block_no, @occupancy_verified) AS new " +
            "ON DUPLICATE KEY UPDATE " +
            "  decided_at = new.decided_at, zone_id = new.zone_id, " +
            "  outcome = new.outcome, reason = new.reason, " +
            "  block_no = new.block_no, occupancy_verified = new.occupancy_verified";

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
                Add(cmd, "@block_no",   routing.BlockNo);
                // Cột NOT NULL DEFAULT 0, nên gửi 0/1 chứ không gửi DBNull.
                cmd.Parameters.AddWithValue("@occupancy_verified", routing.OccupancyVerified ? 1 : 0);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // Quyết định điều hướng MỚI NHẤT, không giới hạn thời gian.
        //
        // Trước đây chỗ này lọc theo cửa sổ 90 giây: quá hạn thì màn hình tài xế
        // quay về trạng thái chờ. Người vận hành muốn ngược lại — chỉ dẫn ở lại
        // trên màn hình cho tới khi có xe kế tiếp được quét, rồi mới đổi.
        //
        // Lưu ý: cửa sổ 90 giây VẪN CÒN, nhưng chỉ ở một chỗ khác và cho một việc
        // khác — BlockAllocator dùng nó để trừ các suất vừa phát đi. Hai thứ đó
        // từng dùng chung một hằng số; gỡ nhầm cái kia thì phép trừ sẽ đếm mọi
        // quyết định từ trước tới nay và mọi block đều trông như đã đầy.
        //
        // Mốc sắp xếp là `decided_at` — thời điểm điểm đến trở nên có hiệu lực —
        // chứ KHÔNG phải `received_at`: lúc app pool recycle, hai worker cùng sống
        // tới 90 giây nên thứ tự nhận có thể khác thứ tự quyết định.
        private const string CurrentSql =
            "SELECT event_id, decided_at, zone_id, outcome, reason, block_no, occupancy_verified " +
            "FROM   vehicle_routing " +
            "ORDER  BY decided_at DESC, event_id DESC LIMIT 1";

        // null chỉ xảy ra khi bảng chưa có quyết định nào — bãi vừa dựng xong.
        public VehicleRouting GetCurrentDecision()
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = CurrentSql;

                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    return new VehicleRouting
                    {
                        EventId           = Str(r, "event_id"),
                        DecidedAt         = Convert.ToDateTime(r["decided_at"]),
                        ZoneId            = NullableInt(r, "zone_id"),
                        Outcome           = Str(r, "outcome"),
                        Reason            = Str(r, "reason"),
                        BlockNo           = NullableInt(r, "block_no"),
                        OccupancyVerified = Num(r, "occupancy_verified") == 1
                    };
                }
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
                    "SELECT event_id, received_at, make, model, length_mm, height_mm, weight_kg, " +
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
                            HeightMm    = NullableInt(r, "height_mm"),
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
        public int?     HeightMm    { get; set; }
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
