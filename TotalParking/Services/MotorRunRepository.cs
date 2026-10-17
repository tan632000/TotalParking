using System;
using System.Collections.Generic;
using MySqlConnector;

namespace TotalParking.Services
{
    // Bảng motor_run (Database/62_motor_run.sql): mỗi dòng một lượt chạy động cơ
    // quan sát được. Chỉ ghi thêm, không sửa, không xoá.
    public class MotorRunRepository
    {
        public void Ghi(int blockNo, string motorCode, DateTime batDau, DateTime ketThuc, long thoiLuongMs)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO motor_run (block_no, motor_code, started_at, ended_at, duration_ms) " +
                    "VALUES (@b, @m, @bd, @kt, @ms)";
                cmd.Parameters.AddWithValue("@b",  blockNo);
                cmd.Parameters.AddWithValue("@m",  motorCode);
                cmd.Parameters.AddWithValue("@bd", batDau);
                cmd.Parameters.AddWithValue("@kt", ketThuc);
                // Kẹp vào miền INT UNSIGNED: âm thì MySQL strict từ chối cả dòng.
                cmd.Parameters.AddWithValue("@ms", Math.Max(0L, Math.Min(thoiLuongMs, uint.MaxValue)));
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ===================== ĐỌC CHO TRANG BẢO TRÌ =====================
        // "Hôm nay" và mốc lần cuối tính theo ended_at: một dòng chỉ tồn tại khi
        // lượt chạy đã kết thúc, nên ended_at là lúc con số được biết. Bảng chưa
        // tạo thì MySqlException 1146 ném ra — người gọi quyết định hiển thị gì.
        public IList<TongHopDongCo> TongHop(int blockNo)
        {
            var ra = new List<TongHopDongCo>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT motor_code, SUM(ended_at >= CURDATE()) AS hom_nay, COUNT(*) AS tong, " +
                    "       MAX(ended_at) AS lan_cuoi " +
                    "FROM motor_run WHERE block_no = @b GROUP BY motor_code";
                cmd.Parameters.AddWithValue("@b", blockNo);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ra.Add(new TongHopDongCo
                        {
                            MotorCode = Convert.ToString(r["motor_code"]),
                            HomNay    = r["hom_nay"] == DBNull.Value ? 0 : Convert.ToInt32(r["hom_nay"]),
                            Tong      = Convert.ToInt32(r["tong"]),
                            LanCuoi   = r["lan_cuoi"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["lan_cuoi"])
                        });
                    }
                }
            }
            return ra;
        }

        // `gioiHan` lượt chạy gần nhất của một khối, mới nhất trước.
        public IList<LuotChay> LichSu(int blockNo, int gioiHan)
        {
            var ra = new List<LuotChay>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT motor_code, started_at, ended_at, duration_ms FROM motor_run " +
                    "WHERE block_no = @b ORDER BY ended_at DESC, run_id DESC LIMIT @n";
                cmd.Parameters.AddWithValue("@b", blockNo);
                cmd.Parameters.AddWithValue("@n", gioiHan);
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ra.Add(new LuotChay
                        {
                            MotorCode  = Convert.ToString(r["motor_code"]),
                            BatDau     = Convert.ToDateTime(r["started_at"]),
                            KetThuc    = Convert.ToDateTime(r["ended_at"]),
                            ThoiLuongMs = Convert.ToInt64(r["duration_ms"])
                        });
                    }
                }
            }
            return ra;
        }

        // block_no -> slot_count của khối cơ khí đang dùng. slot_count chọn bảng
        // bit của NOTE FINAL (sheet BLOCK 3 / 5 / 6 / 10).
        public IDictionary<int, int> DocLoaiKhoi()
        {
            var ra = new Dictionary<int, int>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT block_no, slot_count FROM block " +
                    "WHERE kind = 'Mechanical' AND is_active = 1";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        ra[Convert.ToInt32(r["block_no"])] = Convert.ToInt32(r["slot_count"]);
                }
            }
            return ra;
        }
    }

    public class TongHopDongCo
    {
        public string    MotorCode { get; set; }
        public int       HomNay    { get; set; }
        public int       Tong      { get; set; }
        public DateTime? LanCuoi   { get; set; }
    }

    public class LuotChay
    {
        public string   MotorCode   { get; set; }
        public DateTime BatDau      { get; set; }
        public DateTime KetThuc     { get; set; }
        public long     ThoiLuongMs { get; set; }
    }
}
