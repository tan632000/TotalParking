using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services.Plc
{
    // Đọc trạng thái từng ô đỗ từ PLC và lưu vào plc_slot_state.
    //
    // Hợp đồng thanh ghi (khách xác nhận): mỗi block tối đa 10 ô, đọc theo thứ tự
    //   ô 1 -> D400,  ô 2..5 -> D202 D204 D206 D208,  ô 6..10 -> D300..D308
    // Block 4 ô chỉ đọc 4 địa chỉ đầu, block 6 ô đọc 6 đầu, block 10 ô đọc hết.
    // Cả 10 có vai trò như nhau: giữ mã thẻ của xe đã gửi thành công vào ô đó.
    //
    // CHỈ ĐỌC. Không có đường nào từ lớp này ghi xuống PLC.
    public class SlotOccupancyReader
    {
        // Đọc gộp theo KHỐI thay vì từng ô một: 10 ô nằm trong 3 vùng liền nhau
        // (D200.., D300.., D400..), nên 3 lệnh đọc là đủ thay vì 10. Ở quy mô 112
        // PLC thì đây là khác biệt giữa 336 và 1120 lệnh mỗi vòng.
        private static readonly int[] Blocks200 = { 200, 300, 400 };
        private const int BlockSpan = 12;   // D200-D211, D300-D311, D400-D411

        private const string LoadSql =
            "SELECT s.block_id, s.slot_index, s.word_addr, s.card_code, " +
            "       s.raw_words, s.read_at, s.changed_at, " +
            "       b.block_no, b.zone_id, " +
            "       (c.card_id IS NOT NULL) AS card_known, " +
            // Ba cot ho so the: LEFT JOIN da co san nen khong them phep noi nao.
            //
            // CAN THAN: Load() KHONG chi phuc vu duong hien thi. ScanAsync cung
            // goi no de dung ban do slot theo block. Ba cot nay khong tham gia
            // giai ma hay ghi xuong PLC, nhung neu cau SELECT hong thi ca vong
            // quet chet theo, va SlotScanHost chi nuot loi vao LastError nen
            // ben ngoai khong thay gi. plate va vehicle_name den tu migration
            // 39_card_dec_fields.sql chu khong phai schema goc: mot CSDL chua
            // chay migration do se lam ca bai ngung cap nhat trang thai o.
            "       c.plate, c.card_no, c.vehicle_name " +
            "FROM   plc_slot_state s " +
            "JOIN   block b ON b.block_id = s.block_id " +
            "LEFT   JOIN parking_card c ON c.card_code = s.card_code " +
            "ORDER  BY b.block_no, s.slot_index";

        public int SlotWordCount
        {
            get
            {
                int n;
                if (!int.TryParse(ConfigurationManager.AppSettings["plc:slotWordCount"], out n) || n < 1 || n > 4)
                    n = 2;   // mã thẻ 32 bit = 2 word
                return n;
            }
        }

        public IList<SlotState> Load(int? blockNo = null)
        {
            var result = new List<SlotState>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = LoadSql;
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var s = Map(r);
                        if (blockNo.HasValue && s.BlockNo != blockNo.Value) continue;
                        result.Add(s);
                    }
                }
            }
            return result;
        }

        // Đọc toàn bộ PLC đang kết nối, ghi kết quả xuống DB.
        // Trả về số ô đã cập nhật và số ô ĐỔI trạng thái.
        public async Task<ScanResult> ScanAsync(PlcConnectionManager manager)
        {
            var res = new ScanResult();
            if (manager == null) return res;

            // Khoa theo BlockId (khoa chinh) chu KHONG phai BlockNo.
            //
            // BlockNo la so in tren ban ve — no dung, nhung no di qua tay nguoi va
            // qua may lan doi anh xa. BlockId la khoa chinh cua bang block, khong
            // the trung va khong ai sua bang tay. Doc tu PLC X roi ghi vao dong cua
            // block Y la loi nguy hiem nhat cua ca tang nay: he thong se bao xe
            // dang o block khac, va nguoi di tim xe se toi nham cho.
            var byBlock = Load().GroupBy(s => s.BlockId)
                                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var conn in manager.Connections)
            {
                List<SlotState> slots;
                if (!byBlock.TryGetValue(conn.Device.BlockId, out slots)) continue;

                res.BlocksTried++;
                Dictionary<int, ushort[]> areas;
                try
                {
                    areas = await ReadAreasAsync(conn).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Một PLC hỏng không được làm hỏng cả vòng quét. Ghi lại rồi đi tiếp.
                    res.Failures[conn.Device.BlockNo] = ex.Message;
                    PlcAuditLog.Error(conn.Device.IpAddress, conn.Device.BlockNo,
                                      "QUET O", ex.Message);
                    continue;
                }

                res.BlocksRead++;
                int changedHere = 0;
                foreach (var slot in slots)
                {
                    // Chot chan danh tinh: dong sap ghi PHAI thuoc dung PLC vua doc.
                    // Khong the xay ra neu dictionary o tren dung, nhung day la cho
                    // ma mot loi im lang se bien thanh "xe nam o block khac" — dat
                    // canh gac o day re hon la di lan vet sau.
                    if (slot.BlockId != conn.Device.BlockId)
                    {
                        res.Failures[conn.Device.BlockNo] =
                            "LECH DANH TINH: doc tu " + conn.Device.Endpoint +
                            " (block_id " + conn.Device.BlockId + ") nhung dong o thuoc block_id " +
                            slot.BlockId + ". Bo qua de khong ghi nham.";
                        continue;
                    }

                    ushort[] words = Extract(areas, slot.WordAddr, SlotWordCount);
                    if (words == null) continue;

                    string raw  = CardCodeDecoder.ToRawHex(words);
                    string card = Decode(words);

                    // Theo dõi theo giá trị THÔ, khác với `changed` bên dưới vốn so
                    // theo mã đã giải. Hai word đổi mà vẫn giải ra cùng một mã — hoặc
                    // cùng ra null — là thay đổi thật trong PLC mà phép so mã không
                    // thấy. Đúng loại việc nhật ký thanh ghi sinh ra để bắt.
                    // Phân biệt RÕ "ô trống" với "không giải mã được": cả hai đều
                    // cho card = null, nhưng một cái là bình thường còn một cái là
                    // dấu hiệu bố cục mã thẻ sai. Gộp chung thì đọc nhật ký không
                    // biết thanh ghi khác 0 mà giải không ra, tức giấu mất đúng thứ
                    // cần chú ý nhất.
                    string moTa = card != null      ? card
                                : CardCodeDecoder.IsEmpty(words, SlotWordCount)
                                                    ? "trong"
                                                    : "KHONG GIAI MA DUOC";

                    PlcRegisterLog.Track(conn.Device.IpAddress, conn.Device.BlockNo,
                                         "D" + slot.WordAddr, raw,
                                         "o " + slot.SlotIndex + ": " + moTa);

                    bool changed = !string.Equals(card, slot.CardCode, StringComparison.OrdinalIgnoreCase);
                    if (changed)
                    {
                        res.Changed++;
                        changedHere++;
                        // O DOI TRANG THAI = xe vao hoac ra. Su kien dang gia nhat
                        // cua ca vong quet, luon vao nhat ky kem IP that.
                        PlcAuditLog.Read(conn.Device.IpAddress, conn.Device.BlockNo,
                                         "D" + slot.WordAddr, raw,
                                         string.Format("o {0}: {1} -> {2}", slot.SlotIndex,
                                                       slot.CardCode ?? "(trong)",
                                                       card ?? "(trong)"));

                        // Nhật ký thanh ghi ở trên là để lần vết sự cố, ghi theo IP
                        // và địa chỉ word. Dòng dưới đây là để ĐẾM theo khối, nên
                        // vào bảng riêng có sẵn chỉ mục (block_id, occurred_at).
                        //
                        // Nuốt lỗi tại đây: đếm lượt là việc phụ, không được phép
                        // làm hỏng vòng quét ô đỗ — thứ mà bảng LED và chức năng
                        // tìm xe phụ thuộc vào.
                        try
                        {
                            GhiSuKienDoiO(slot.BlockId, slot.SlotIndex, slot.WordAddr,
                                          slot.CardCode, card);
                        }
                        catch (Exception ex)
                        {
                            PlcAuditLog.Error(conn.Device.IpAddress, conn.Device.BlockNo,
                                              "GHI SU KIEN O",
                                              "Khong ghi duoc parking_event: " + ex.Message);
                        }
                    }
                    res.SlotsRead++;

                    // Chi dem la CO XE khi ma doc duoc khop mot the that. Thanh ghi
                    // khac 0 nhung khong khop the nao la DU LIEU LA — dem no vao so
                    // o da dung se lam bang LED bao thieu cho.
                    if (!string.IsNullOrEmpty(card))
                    {
                        if (CardExists(card)) res.Occupied++;
                        else
                        {
                            res.Suspect++;
                            res.SuspectSlots[conn.Device.BlockNo + "/" + slot.SlotIndex] =
                                "D" + slot.WordAddr + " = " + raw;
                        }
                    }

                    Save(slot.BlockId, slot.SlotIndex, card, raw, changed);
                }

                PlcAuditLog.Scan(conn.Device.IpAddress, conn.Device.BlockNo,
                                 slots.Count, changedHere);
            }
            return res;
        }

        private static async Task<Dictionary<int, ushort[]>> ReadAreasAsync(PlcConnection conn)
        {
            var map = new Dictionary<int, ushort[]>();
            foreach (int b in Blocks200)
                map[b] = await conn.ReadWordsRawAsync(b, BlockSpan).ConfigureAwait(false);
            return map;
        }

        private static ushort[] Extract(Dictionary<int, ushort[]> areas, int addr, int count)
        {
            int baseAddr = (addr / 100) * 100;
            ushort[] arr;
            if (!areas.TryGetValue(baseAddr, out arr) || arr == null) return null;
            int off = addr - baseAddr;
            if (off < 0 || off + count > arr.Length) return null;
            var w = new ushort[count];
            Array.Copy(arr, off, w, 0, count);
            return w;
        }

        // Thử cả hai thứ tự word rồi lấy cái khớp thẻ có thật.
        //
        // Chưa có xe nào được gửi nên chưa xác nhận được thứ tự nào đúng. Dò như
        // thế này an toàn hơn chọn bừa: nếu không khớp thẻ nào thì trả về theo
        // Binary32Lo và phía trên vẫn giữ raw_words để giải mã lại.
        private string Decode(ushort[] words)
        {
            if (CardCodeDecoder.IsEmpty(words, words.Length)) return null;

            foreach (var layout in CardCodeDecoder.ProbeOrder)
            {
                if (CardCodeDecoder.WordCountFor(layout) != words.Length) continue;
                string c = CardCodeDecoder.TryDecode(words, layout);
                if (!string.IsNullOrEmpty(c) && CardExists(c)) return c;
            }
            return CardCodeDecoder.TryDecode(words, CardCodeLayout.Binary32Lo);
        }

        private static bool CardExists(string code)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1 FROM parking_card WHERE card_code = @c LIMIT 1";
                cmd.Parameters.AddWithValue("@c", code);
                conn.Open();
                return cmd.ExecuteScalar() != null;
            }
        }

        // Ghi một dòng nhật ký mỗi lần ô đổi trạng thái, để đếm được số lượt
        // gửi/lấy xe theo từng khối về sau.
        //
        // ===================== ĐÂY KHÔNG PHẢI SỐ LẦN MOTOR CHẠY =====================
        //
        // Đừng dùng con số này để lập lịch bảo trì theo chu kỳ. Hệ thống là puzzle
        // parking: lấy một xe ra thì cơ cấu phải dịch chuyển nhiều khay khác để mở
        // đường, và những lần chạy motor đó KHÔNG làm đổi mã thẻ ô nào nên vòng quét
        // không nhìn thấy gì. Bảng vật tư CL1 còn cho thấy mỗi khối có hai motor
        // riêng — "Motor trượt" và "Motor hàng rào" — chạy với số lần khác nhau mà
        // một con số gộp không tách được.
        //
        // Thêm một điểm mù: vòng quét chạy 45 giây một lượt (`plc:slotScanMs`), nên
        // gửi rồi lấy trong cùng cửa sổ sẽ mất dấu hoàn toàn.
        //
        // Vì vậy con số này LUÔN THẤP HƠN số lần motor chạy thật, và thấp bao nhiêu
        // thì không biết được. Muốn đếm đúng thì PLC phải tự đếm rồi phơi ra một
        // thanh ghi — nó là bên duy nhất biết motor chạy mấy lần, và bộ đếm nằm
        // trong PLC thì không mất khi SCADA khởi động lại.
        //
        // Dùng được cho: khối nào bận, phân bố tải giữa các khối, đối chiếu với bộ
        // đếm thật khi nào có.
        private static void GhiSuKienDoiO(int blockId, int slotIndex, int wordAddr,
                                          string cardCu, string cardMoi)
        {
            bool coCu  = !string.IsNullOrEmpty(cardCu);
            bool coMoi = !string.IsNullOrEmpty(cardMoi);

            // Cả hai đều có thẻ nhưng khác nhau = một lượt lấy và một lượt gửi lọt
            // vào cùng cửa sổ quét. Ghi riêng loại này thay vì đoán bừa một trong
            // hai, để người đọc sau biết đó là quan sát thiếu chứ không phải sự kiện
            // lạ của thiết bị.
            string loai = !coCu && coMoi ? "SLOT_STORE"
                        : coCu && !coMoi ? "SLOT_RETRIEVE"
                        : "SLOT_SWAP";

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // JSON_OBJECT thay vì tự nối chuỗi: mã thẻ đi thẳng vào JSON mà tự
                // ghép thì một ký tự lạ là hỏng cả dòng.
                cmd.CommandText =
                    "INSERT INTO parking_event " +
                    "  (session_id, block_id, actor, actor_ref, event_type, " +
                    "   from_status, to_status, detail, occurred_at) " +
                    "VALUES (NULL, @b, 'PLC', @ref, @loai, @tu, @den, " +
                    "        JSON_OBJECT('slot_index', @i, 'word_addr', @w, " +
                    "                    'from_card', @cu, 'to_card', @moi), @now)";
                cmd.Parameters.AddWithValue("@b",    blockId);
                cmd.Parameters.AddWithValue("@ref",  "D" + wordAddr);
                cmd.Parameters.AddWithValue("@loai", loai);
                cmd.Parameters.AddWithValue("@tu",   coCu  ? "OCCUPIED" : "EMPTY");
                cmd.Parameters.AddWithValue("@den",  coMoi ? "OCCUPIED" : "EMPTY");
                cmd.Parameters.AddWithValue("@i",    slotIndex);
                cmd.Parameters.AddWithValue("@w",    wordAddr);
                cmd.Parameters.AddWithValue("@cu",   (object)cardCu  ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@moi",  (object)cardMoi ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@now",  DateTime.Now);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static void Save(int blockId, int slotIndex, string card, string raw, bool changed)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                // changed_at chỉ nhích khi nội dung thật sự khác — read_at nhích mỗi
                // vòng. Tách ra để lần vết được lúc xe vào/ra mà không bị nhiễu bởi
                // hàng nghìn lần đọc không đổi.
                cmd.CommandText =
                    "UPDATE plc_slot_state " +
                    "SET    card_code = @card, raw_words = @raw, read_at = @now" +
                    (changed ? ", changed_at = @now " : " ") +
                    "WHERE  block_id = @b AND slot_index = @i";
                cmd.Parameters.AddWithValue("@card", (object)card ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@raw", (object)raw ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@now", DateTime.Now);
                cmd.Parameters.AddWithValue("@b", blockId);
                cmd.Parameters.AddWithValue("@i", slotIndex);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private static SlotState Map(IDataRecord r)
        {
            return new SlotState
            {
                BlockId   = Convert.ToInt32(r["block_id"]),
                BlockNo   = Convert.ToInt32(r["block_no"]),
                ZoneId    = Convert.ToInt32(r["zone_id"]),
                SlotIndex = Convert.ToInt32(r["slot_index"]),
                WordAddr  = Convert.ToInt32(r["word_addr"]),
                CardCode  = r["card_code"] == DBNull.Value ? null : Convert.ToString(r["card_code"]),
                RawWords  = r["raw_words"] == DBNull.Value ? null : Convert.ToString(r["raw_words"]),
                ReadAt    = r["read_at"]    == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["read_at"]),
                ChangedAt = r["changed_at"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["changed_at"]),
                CardKnown = Convert.ToBoolean(r["card_known"]),
                Plate       = r["plate"]        == DBNull.Value ? null : Convert.ToString(r["plate"]),
                CardNo      = r["card_no"]      == DBNull.Value ? null : Convert.ToString(r["card_no"]),
                VehicleName = r["vehicle_name"] == DBNull.Value ? null : Convert.ToString(r["vehicle_name"])
            };
        }

        public class ScanResult
        {
            public int BlocksTried { get; set; }
            public int BlocksRead  { get; set; }
            public int SlotsRead   { get; set; }
            public int Occupied    { get; set; }
            // Thanh ghi khac 0 nhung ma doc duoc khong khop the nao trong he thong.
            public int Suspect     { get; set; }
            public int Changed     { get; set; }
            public IDictionary<int, string> Failures { get; private set; }
            public IDictionary<string, string> SuspectSlots { get; private set; }

            public ScanResult()
            {
                Failures = new Dictionary<int, string>();
                SuspectSlots = new Dictionary<string, string>();
            }
        }
    }
}
