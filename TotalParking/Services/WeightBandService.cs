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
    // ======================= VI SAO KHONG BIET THI TRA 0 =======================
    // The la, the het hieu luc, hoac mat DB deu tra 0 chu KHONG tra 3.
    //
    // Tra 3 nghia la khang dinh "xe nay tren 2600 kg" — mot dieu ta khong he biet.
    // Ladder co the dua vao do de chon cho do. Con 0 la "chua co cau tra loi", dung
    // nghia voi luc D106 trong, nen ladder xu ly ca hai truong hop bang cung mot
    // nhanh: khong du thong tin thi khong hanh dong.
    //
    // Day cung la nguyen tac fail-closed ma CardScanService da dat ra: cho qua khi
    // khong biet chinh la loi ma toan bo co che sinh ra de chan.
    public class WeightBandService
    {
        // Gia tri ghi xuong D1004 khi khong phan loai duoc.
        public const int Unknown = 0;

        private const string BandSql =
            "SELECT CASE WHEN w.max_weight_kg IS NULL  THEN 3 " +
            "            WHEN w.max_weight_kg <= 2200  THEN 1 " +
            "            WHEN w.max_weight_kg <= 2600  THEN 2 " +
            "            ELSE 3 END AS band " +
            "FROM   parking_card c " +
            "JOIN   weight_class w ON w.weight_class_id = c.weight_class_id " +
            "WHERE  c.card_code = @code AND c.is_active = 1";

        // Tra 0 khi ma the rong, khong co trong he thong, the da khoa, hoac mat DB.
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
                    return v == null || v == DBNull.Value ? Unknown : Convert.ToInt32(v);
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
