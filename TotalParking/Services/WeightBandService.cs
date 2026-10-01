using System;
using MySqlConnector;

namespace TotalParking.Services
{
    // Tra ma the -> bang tai trong de ghi xuong D1004.
    //
    //     1  duoi 2200 kg
    //     2  tu 2200 den 2600 kg
    //     3  tren 2600 kg
    //     0  khong biet, HOAC the dang nam trong block khac
    //
    // ======================= MOT THE CHI O MOT BLOCK =======================
    // Khi mot the da gui xe thanh cong vao block X, quet lai no o block Y != X
    // se tra 0 chu khong phai hang tai. Ladder thay 0 thi khong cho thao tac.
    //
    // Day la lop chan duy nhat cho quy tac "1 the = 1 block". Khong co no thi
    // mot the dang giu xe o block X van quet duoc o block Y va mo them mot cho
    // nua, luc do he thong khong con biet chiec xe nam o dau.
    //
    // Nguon su that la plc_slot_state — trang thai doc TU PLC, khong phai y dinh
    // cua SCADA. Dung bang phien (parking_session) se chan nham: phien treo vi
    // khach quet roi lai di se khoa the vinh vien du khong co xe nao trong o.
    //
    // 0 o day trung voi 0 cua "khong biet" la co y: ladder da xu ly 0 bang nhanh
    // "khong cho thao tac" san roi, nen khong phai sua ladder. Cai gia phai tra
    // la ba truong hop khac nhau cung ra mot con so — bu lai bang chuoi `lyDo`
    // ghi xuong nhat ky.
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

        // Mot the dang nam trong block KHAC thi khong duoc thao tac o day.
        //
        // Dieu kien dat TRUOC moi nhanh hang tai: khi the da chiem mot o o cho
        // khac thi hang tai cua no khong con la cau hoi dang tra loi nua.
        //
        // So sanh voi block_id (khoa chinh) chu khong phai block_no (so in tren
        // ban ve): block_no di qua tay nguoi va qua may lan doi anh xa, con
        // block_id thi khong the trung. Cung ly le voi SlotOccupancyReader.
        //
        // `<> @block_id` chu khong phai `= @block_id`: the nam o DUNG block dang
        // quet van duoc tra hang tai that, vi do la luot lay xe. Chi khi no nam
        // o noi khac moi chan.
        private const string BandSql =
            "SELECT CASE " +
            "         WHEN EXISTS (SELECT 1 FROM plc_slot_state s " +
            "                       WHERE s.card_code = c.card_code " +
            "                         AND s.block_id <> @block_id)  THEN 0 " +
            "         WHEN w.max_weight_kg IS NULL                  THEN 3 " +
            "         WHEN w.max_weight_kg <= 2200                  THEN 1 " +
            "         WHEN w.max_weight_kg <= 2600                  THEN 2 " +
            "         ELSE 3 END AS band, " +
            // Block dang giu the, chi de ghi nhat ky. Khong co cot nay thi nguoi
            // van hanh nhin thay D1004 = 0 ma khong biet the dang ket o dau.
            "       (SELECT MIN(b.block_no) FROM plc_slot_state s2 " +
            "          JOIN block b ON b.block_id = s2.block_id " +
            "         WHERE s2.card_code = c.card_code " +
            "           AND s2.block_id <> @block_id) AS block_dang_giu " +
            "FROM   parking_card c " +
            "JOIN   weight_class w ON w.weight_class_id = c.weight_class_id " +
            "WHERE  c.card_code = @code AND c.is_active = 1";

        // 1/2/3 theo hang tai khi the co trong danh ba, con hieu luc, va khong
        //       dang nam trong block nao khac.
        // 3     khi hoi duoc DB nhung the khong co trong danh ba hoac da bi khoa.
        // 0     khi ma the rong, khi the dang nam trong block khac, hoac khi
        //       khong hoi duoc DB.
        //
        // `blockId` la block dang co nguoi quet the — khoa chinh, khong phai so
        // in tren ban ve.
        //
        // `lyDo` chi de ghi nhat ky. Ba truong hop khac nhau cung cho ra 0, va
        // nguoi van hanh dung truoc HMI khong the phan biet duoc chung tu con so
        // 0 tren thanh ghi. Khong co chuoi nay thi moi lan "the khong quet duoc"
        // deu phai lan nguoc bang tay.
        public int BandFor(string cardCode, int blockId, out string lyDo)
        {
            if (string.IsNullOrWhiteSpace(cardCode))
            {
                lyDo = "khong co ma the";
                return Unknown;
            }

            try
            {
                using (var conn = new MySqlConnection(Db.ConnectionString))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = BandSql;
                    cmd.Parameters.AddWithValue("@code",     cardCode.Trim());
                    cmd.Parameters.AddWithValue("@block_id", blockId);

                    conn.Open();
                    using (var r = cmd.ExecuteReader())
                    {
                        // Truy van CHAY XONG ma khong co dong nao = da biet chac
                        // the khong co trong danh ba. Khac han voi nhanh catch ben
                        // duoi, noi ta khong hoi duoc gi ca.
                        if (!r.Read())
                        {
                            lyDo = "the khong co trong danh ba hoac da bi khoa";
                            return Overweight;
                        }

                        int band = Convert.ToInt32(r["band"]);
                        if (band != Unknown)
                        {
                            lyDo = "hang tai cua the";
                            return band;
                        }

                        // band = 0 o day CHI co the den tu nhanh EXISTS: cac nhanh
                        // con lai cua CASE deu tra 1, 2 hoac 3.
                        object giu = r["block_dang_giu"];
                        lyDo = giu == null || giu == DBNull.Value
                                   ? "the dang nam trong block khac"
                                   : "the dang nam trong block " + Convert.ToInt32(giu);
                        return Unknown;
                    }
                }
            }
            catch (MySqlException)
            {
                // Mat DB thi khong biet hang tai. Xem khoi chu thich dau lop.
                lyDo = "khong hoi duoc DB";
                return Unknown;
            }
            catch (InvalidOperationException)
            {
                // Pool can, connection string sai, DB chua dung — cung nhom voi tren.
                lyDo = "khong hoi duoc DB";
                return Unknown;
            }
        }
    }
}
