using System;
using MySqlConnector;

namespace TotalParking.Services
{
    // Tra ma the -> bang tai trong de ghi xuong D1004.
    //
    //     1  duoi 2200 kg
    //     2  tu 2200 den 2600 kg
    //     3  tren 2600 kg
    //     0  khong biet
    //
    // ======================= VI SAO SUY TU max_weight_kg =======================
    // Bang weight_class co ca `code` ('2200KG') lan `max_weight_kg` (2200). Dung
    // con so chu khong dung ten: them mot hang tai moi thi cau SQL van dung, con
    // so sanh chuoi thi phai sua code. Ten chi de doc, con so moi la hop dong.
    //
    // THUONG co max_weight_kg = NULL — qua tai, khong len duoc pallet nao — nen
    // roi vao bang 3.
    //
    // ======================= THE LA THI COI LA QUA TAI =======================
    // Quyet dinh cua nguoi van hanh: ma the doc duoc o D106 nhung KHONG co trong
    // parking_card thi tra 3 — qua tai, do nen, khong len pallet co khi.
    //
    // Day la huong AN TOAN VE CO KHI: mot chiec xe khong ro trong luong ma bi xep
    // len pallet 2200 kg co the lam qua tai pallet. Dua xuong do nen thi khong.
    //
    // ======================= NHUNG MAT DB THI VAN TRA 0 =======================
    // Hai tinh huong nghe giong nhau nhung khac han:
    //
    //   the khong co trong danh ba  -> 3. Da HOI DUOC DB va biet chac no khong co.
    //   mat ket noi DB              -> 0. KHONG hoi duoc, nen khong biet gi ca.
    //
    // Tra 3 khi mat DB la khang dinh "xe nay tren 2600 kg" dua tren mot su co ha
    // tang, khong dua tren du lieu. Neu DB chet ca buoi thi MOI xe deu bi day
    // xuong do nen, ke ca xe co the hop le. 0 nghia la "chua co cau tra loi",
    // trung nghia voi luc D106 trong, nen ladder xu ly bang cung mot nhanh.
    public class WeightBandService
    {
        // Khong co the o D106, hoac khong hoi duoc DB.
        public const int Unknown = 0;

        // Doc duoc ma the nhung no khong co trong danh ba, hoac the da bi khoa.
        // Cung la bang cua hang tai THUONG.
        public const int Overweight = 3;

        private const string BandSql =
            "SELECT CASE WHEN w.max_weight_kg IS NULL  THEN 3 " +
            "            WHEN w.max_weight_kg <= 2200  THEN 1 " +
            "            WHEN w.max_weight_kg <= 2600  THEN 2 " +
            "            ELSE 3 END AS band " +
            "FROM   parking_card c " +
            "JOIN   weight_class w ON w.weight_class_id = c.weight_class_id " +
            "WHERE  c.card_code = @code AND c.is_active = 1";

        // 1/2/3 theo hang tai khi the co trong danh ba va con hieu luc.
        // 3 khi hoi duoc DB nhung the khong co trong danh ba hoac da bi khoa.
        // 0 khi ma the rong, hoac khong hoi duoc DB.
        public int BandFor(string cardCode)
        {
            if (string.IsNullOrWhiteSpace(cardCode)) return Unknown;

            try
            {
                using (var conn = new MySqlConnection(Db.ConnectionString))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = BandSql;
                    cmd.Parameters.AddWithValue("@code", cardCode.Trim());

                    conn.Open();
                    object v = cmd.ExecuteScalar();

                    // Truy van CHAY XONG ma khong co dong nao = da biet chac the
                    // khong co trong danh ba. Khac han voi nhanh catch ben duoi,
                    // noi ta khong hoi duoc gi ca.
                    return v == null || v == DBNull.Value ? Overweight : Convert.ToInt32(v);
                }
            }
            catch (MySqlException)
            {
                // Mat DB thi khong biet hang tai. Xem khoi chu thich dau lop.
                return Unknown;
            }
            catch (InvalidOperationException)
            {
                // Pool can, connection string sai, DB chua dung — cung nhom voi tren.
                return Unknown;
            }
        }
    }
}
