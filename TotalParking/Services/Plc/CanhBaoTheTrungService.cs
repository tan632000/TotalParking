using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace TotalParking.Services.Plc
{
    // Sinh cảnh báo khi một mã thẻ đã đăng ký xuất hiện ở nhiều khối cùng lúc,
    // và đóng cảnh báo khi hết trùng.
    //
    // ===================== VÌ SAO KHÔNG CHẶN MÀ CHỈ BÁO =====================
    // Toàn hệ thống có đúng bốn lệnh ghi xuống PLC (PlcConnection: xoá lệnh tìm
    // thẻ, xoá ô trả lời, ghi hạng tải, ghi số khối trả lời). KHÔNG lệnh nào ghi
    // mã thẻ vào thanh ghi ô đỗ — D400/D200 do chính PLC ghi khi nó cất xe.
    //
    // Nghĩa là không tồn tại điểm nào trong mã này để "từ chối gán thẻ trùng":
    // việc trùng sinh ra trong bộ nhớ PLC, trước khi ta nhìn thấy. Vì vậy cũng
    // KHÔNG đặt UNIQUE lên plc_slot_state.card_code — ta không tạo ra dữ liệu đó,
    // ta chỉ chép lại. Thêm ràng buộc chỉ làm câu INSERT của vòng quét ném lỗi,
    // mà SlotScanHost nuốt lỗi vào LastError, nên cả bãi sẽ ngừng cập nhật trong
    // im lặng. Gốc rễ chỉ sửa được trong chương trình ladder: cất xe vào ô mới
    // phải xoá thanh ghi ô cũ trong cùng một chu kỳ. Cảnh báo này là bằng chứng
    // để đưa cho bên tích hợp PLC — nó chỉ đúng mã thẻ và đúng các khối.
    //
    // ===================== ÂN HẠN, VÀ VÌ SAO KHÔNG ĐẾM TRONG BỘ NHỚ =====================
    // Một lượt quét trải khoảng 2 giây cho cả 112 khối (đo trên bản đang chạy:
    // read_at sớm nhất 06:48:09.116, muộn nhất 06:48:11.114). Xe di chuyển từ
    // khối A sang khối B đúng trong cửa sổ đó sẽ HỢP LỆ xuất hiện ở cả hai, và
    // báo động ngay thì đó là báo động giả.
    //
    // Ân hạn ở đây dựa vào cột changed_at chứ không đếm số lượt trong bộ nhớ.
    // changed_at chỉ đổi khi mã thẻ của ô đổi, còn read_at đổi mỗi lượt quét —
    // đã kiểm trên dữ liệu thật: hai ô trùng có read_at 06:52 nhưng changed_at
    // 12:44 và 14:30 hôm trước. Với một cú di chuyển thật, ô ĐÍCH vừa đổi nên bị
    // lọc ra, nhóm còn lại một khối và không sinh cảnh báo. Cách này không mất
    // trạng thái khi ứng dụng khởi động lại, và hai tiến trình cùng chạy cũng
    // cho cùng kết quả.
    //
    // ===================== CHỈ THẺ ĐÃ ĐĂNG KÝ =====================
    // Mã KHÔNG có trong parking_card mà nằm ở nhiều khối là rác ladder, không
    // phải xe — dạng '03010000' từng xuất hiện ở 6-7 khối cùng lúc (xem
    // 24_led_capacity_from_plc.sql). migration 46 vẫn loại nó khỏi v_slot_taken,
    // và giao diện đã hiện riêng nó dưới nhãn "Thẻ lạ". Báo động cho rác chỉ làm
    // người vận hành quen bỏ qua bảng cảnh báo.
    public static class CanhBaoTheTrungService
    {
        // Tiền tố của khoá chống trùng. Dạng đầy đủ: THE_TRUNG_BLOCK:<card_code>.
        public const string TienToKhoa = "THE_TRUNG_BLOCK:";

        // Nhịp 60 giây: vòng quét chạy 45 giây một lượt, nên mỗi nhịp ở đây luôn
        // nhìn thấy kết quả của ít nhất một lượt quét mới.
        private const int NhipMs = 60000;
        private const string MaLoi = "SLOT-DUP-01";

        private static readonly object Sync = new object();
        private static Task _loop;
        private static CancellationTokenSource _cts;
        private static readonly CanhBaoRepository Repo = new CanhBaoRepository();

        public static long SoLanSinh { get; private set; }
        public static long SoLanDong { get; private set; }
        public static long SoLanLoi  { get; private set; }
        public static string LoiCuoi { get; private set; }
        public static DateTime? ChayCuoiUtc { get; private set; }

        // Số giây một ô phải giữ nguyên mã thẻ trước khi được coi là ổn định.
        // Mặc định 90: gấp đôi nhịp quét 45 giây, đủ để một cú di chuyển thật đi
        // qua trọn vẹn ít nhất một lượt quét.
        public static int NguongOnDinhGiay
        {
            get
            {
                int n;
                string raw = ConfigurationManager.AppSettings["plc:theTrungOnDinhGiay"];
                return int.TryParse(raw, out n) && n > 0 ? n : 90;
            }
        }

        public static bool IsRunning { get { return _loop != null; } }

        public static void Start()
        {
            lock (Sync)
            {
                if (_loop != null) return;
                _cts = new CancellationTokenSource();
                _loop = Task.Run(() => VongAsync(_cts.Token));
            }
        }

        public static void Stop()
        {
            lock (Sync)
            {
                if (_cts != null) { _cts.Cancel(); _cts = null; }
                _loop = null;
            }
        }

        private static async Task VongAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    ChayMotLuot();
                }
                catch (Exception ex)
                {
                    // Nuốt ở đây chứ không để ném ra: một lỗi CSDL thoát khỏi vòng
                    // lặp sẽ giết luôn luồng, và từ đó không còn cảnh báo nào được
                    // sinh ra trong khi IsRunning vẫn báo true.
                    SoLanLoi++;
                    LoiCuoi = ex.Message;
                    PlcAuditLog.Error(null, 0, "CANH BAO THE TRUNG",
                                      "Khong chay duoc vong the trung: " + ex.Message);
                }

                try { await Task.Delay(NhipMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        private sealed class TheTrung
        {
            public string MaThe   { get; set; }
            public string SoThe   { get; set; }
            public int    SoKhoi  { get; set; }
            public string CacKhoi { get; set; }
        }

        private static void ChayMotLuot()
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();
                IList<TheTrung> trung = DocTheTrung(conn, NguongOnDinhGiay);
                SinhChoTheTrung(trung);
                DongChoTheHetTrung(conn, trung);
            }

            ChayCuoiUtc = DateTime.UtcNow;
        }

        // Bộ lọc ổn định nằm ở WHERE, TRƯỚC khi gom nhóm — đó là điều làm cho ân
        // hạn hoạt động. Ô vừa đổi bị loại khỏi nhóm, nên một cú di chuyển đang
        // diễn ra chỉ còn lại một khối và rơi khỏi HAVING.
        private const string SqlTrung =
            "SELECT s.card_code, MIN(c.card_no) AS card_no, " +
            "       COUNT(DISTINCT s.block_id) AS so_khoi, " +
            "       GROUP_CONCAT(DISTINCT b.block_no ORDER BY b.block_no SEPARATOR ', ') AS cac_khoi " +
            "FROM   plc_slot_state s " +
            "JOIN   block b ON b.block_id = s.block_id " +
            "JOIN   parking_card c ON c.card_code = s.card_code " +
            "WHERE  s.card_code IS NOT NULL " +
            // changed_at NULL = chưa đổi lần nào kể từ khi ghi dòng đầu tiên, tức
            // là đã ổn định từ lâu. Coi là đủ điều kiện, không loại.
            "  AND  (s.changed_at IS NULL " +
            "        OR s.changed_at < NOW(3) - INTERVAL @giay SECOND) " +
            "GROUP  BY s.card_code " +
            "HAVING COUNT(DISTINCT s.block_id) > 1";

        private static IList<TheTrung> DocTheTrung(MySqlConnection conn, int nguongGiay)
        {
            var ds = new List<TheTrung>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SqlTrung;
                cmd.Parameters.AddWithValue("@giay", nguongGiay);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        ds.Add(new TheTrung
                        {
                            MaThe   = Convert.ToString(r[0]),
                            SoThe   = r[1] == DBNull.Value ? null : Convert.ToString(r[1]),
                            SoKhoi  = Convert.ToInt32(r[2]),
                            CacKhoi = r[3] == DBNull.Value ? "" : Convert.ToString(r[3])
                        });
                    }
                }
            }

            return ds;
        }

        private static void SinhChoTheTrung(IList<TheTrung> trung)
        {
            foreach (TheTrung t in trung)
            {
                string ten = string.IsNullOrEmpty(t.SoThe) ? t.MaThe : t.SoThe + " (" + t.MaThe + ")";

                var cb = new CanhBao
                {
                    Nguon = "hardware",
                    // high chứ không critical: từ migration 46 cả hai ô đều được
                    // tính là có xe, nên không còn nguy cơ xếp chồng. Cái mất là
                    // một suất sức chứa và khả năng trả lời "xe tôi ở đâu".
                    MucDo = "high",
                    MaLoi = MaLoi,
                    // Để trống khối: sự cố này trải trên NHIỀU khối, chọn đại một
                    // số thì nó biến mất khỏi bộ lọc của các khối còn lại.
                    BlockNo = null,
                    ZoneId  = null,
                    ThietBi = "Thẻ " + t.MaThe,
                    MoTa    = "Thẻ " + ten + " xuất hiện ở " + t.SoKhoi +
                              " khối cùng lúc: " + t.CacKhoi +
                              ". Sức chứa đang tính tất cả các ô đó là có xe, và " +
                              "chức năng tìm xe sẽ không trả lời cho thẻ này."
                };

                // Trả 0 nghĩa là thẻ này đã có cảnh báo đang mở — chuyện bình
                // thường, không phải lỗi.
                if (Repo.Ghi(cb, TienToKhoa + t.MaThe) > 0)
                {
                    SoLanSinh++;
                    PlcAuditLog.Error(null, 0, "CANH BAO THE TRUNG",
                        "Sinh canh bao: the " + t.MaThe + " nam o " + t.SoKhoi +
                        " khoi (" + t.CacKhoi + ").");
                }
            }
        }

        // Đóng cảnh báo của những thẻ đã hết trùng.
        //
        // Đối chiếu khoá đang mở với danh sách trùng hiện tại, thay vì gọi
        // DongTheoKhoa cho mọi thẻ. Bình thường không có cảnh báo nào mở thì vòng
        // này không ghi gì cả.
        private static void DongChoTheHetTrung(MySqlConnection conn, IList<TheTrung> trung)
        {
            IList<string> dangMo = Repo.DocKhoaDangMo(conn, TienToKhoa);
            if (dangMo.Count == 0) return;

            var conTrung = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (TheTrung t in trung) conTrung.Add(t.MaThe);

            foreach (string khoa in dangMo)
            {
                string maThe = khoa.Substring(TienToKhoa.Length);
                if (maThe.Length == 0) continue;
                if (conTrung.Contains(maThe)) continue;

                if (Repo.DongTheoKhoa(khoa) > 0)
                {
                    SoLanDong++;
                    PlcAuditLog.Recovered(null, 0, "CANH BAO THE TRUNG");
                }
            }
        }
    }
}
