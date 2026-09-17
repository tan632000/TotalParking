using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;

namespace TotalParking.Services
{
    // Toạ độ và trạng thái 112 block để chấm lên BẢN ĐỒ THẬT (Images/zones_map.jpeg),
    // trong hệ toạ độ SVG 1016 x 781 mà trang Mặt bằng đang dùng.
    //
    // ===================== VÌ SAO KHÔNG DÙNG MỘT PHÉP BIẾN ĐỔI CHUNG =====================
    // Ảnh bản đồ và các đa giác zone vẽ tay được tạo ĐỘC LẬP với bản vẽ CAD mà
    // `origin_x/origin_y` lấy ra, nên không có điểm neo chung. Đã thử ba cách khớp
    // toàn cục — theo trọng tâm zone, theo hộp bao vùng màu, dò khối màu rồi khớp —
    // đạt 68% / 79% / 90% và đều LỆCH HỆ THỐNG: chấm bị co lại và dịch khỏi block.
    //
    // ===================== CÁCH ĐANG DÙNG: CĂN THEO TỪNG ZONE =====================
    // Ba nguồn, mỗi nguồn trả lời đúng một câu:
    //
    //   đa giác zone vẽ tay  ->  "zone này nằm ở khu nào trên bức ảnh"
    //   ảnh bản đồ           ->  "trong khu đó, block được VẼ ở đoạn nào"
    //   toạ độ bản vẽ        ->  "bố cục tương đối giữa các block trong zone"
    //
    // Ghép lại: lấy vị trí tương đối của block trong hộp bao của zone trên BẢN VẼ,
    // rồi trả vào hộp bao của VÙNG CÓ BLOCK trên ẢNH.
    //
    // Bảo đảm hai điều quan trọng nhất: block luôn nằm đúng zone của nó, và thứ tự
    // bố cục trong zone được giữ nguyên. Đổi lại, tỉ lệ giữa các zone không còn
    // tuyệt đối chính xác — khách đã chốt chỉ cần ước lượng.
    public class BlockMapRepository
    {
        public const int ViewW = 1016;
        public const int ViewH = 781;

        // Vùng block thật của từng zone, ĐO TỪ ẢNH: lọc điểm có màu bão hoà (block
        // được tô xanh lá / đỏ / vàng / xanh ngọc) nằm trong đa giác zone, rồi lấy
        // hộp bao sau khi cắt 2.5% hai đầu cho bớt nhiễu viền.
        //
        // Nếu thay ảnh bản đồ thì phải đo lại bộ số này, nếu không chấm sẽ lệch.
        private static readonly Dictionary<int, double[]> ZoneBox =
            new Dictionary<int, double[]>
        {
            { 1, new[] { 581.0, 836.0, 414.0, 673.0 } },
            { 2, new[] { 342.0, 596.0, 394.0, 600.0 } },
            { 3, new[] { 136.0, 317.0,  81.0, 293.0 } },
            { 4, new[] { 315.0, 522.0,  53.0, 384.0 } },
            { 5, new[] { 533.0, 814.0,  59.0, 213.0 } },
            { 6, new[] { 545.0, 734.0, 207.0, 407.0 } },
        };

        // Đa giác zone, lấy đúng từ Views/Home/FloorPlan.cshtml. Chỉ dùng để KÉO
        // các điểm lọt ra ngoài về phía trong: đa giác hình chữ L có phần lõm, và
        // ánh xạ theo hộp bao có thể đặt block vào đúng chỗ lõm đó.
        private static readonly Dictionary<int, double[][]> ZonePoly =
            new Dictionary<int, double[][]>
        {
            { 3, P("57,152 185,46 343,45 308,248 278,293 308,390") },
            { 4, P("343,45 526,45 526,390 412,390 308,390 278,293 308,248") },
            { 5, P("526,45 923,46 923,235 718,223 632,198 526,211") },
            { 6, P("526,211 632,198 718,223 923,235 923,410 602,410 602,380 573,380") },
            { 2, P("308,390 602,390 602,510 573,510 573,630") },
            { 1, P("602,410 923,410 923,582 688,748 573,630 573,510 602,510") },
        };

        // Cổng vào: khách cho biết ram 1 ở phía dưới. Điểm này nằm trên hành lang
        // dẫn vào zone 1, đặt tay theo bản đồ — bản vẽ không có nhãn chữ cho ram
        // nên không suy ra được bằng dữ liệu. Chỉ dùng làm gốc cho hoạt ảnh.
        public const int GateX = 598;
        public const int GateY = 700;

        private const double Pad = 0.04;

        private static double[][] P(string s)
        {
            return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Split(',').Select(double.Parse).ToArray())
                    .ToArray();
        }

        private static bool Inside(double x, double y, double[][] p)
        {
            bool c = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if (((p[i][1] > y) != (p[j][1] > y)) &&
                    (x < (p[j][0] - p[i][0]) * (y - p[i][1]) / (p[j][1] - p[i][1] + 1e-12) + p[i][0]))
                    c = !c;
            }
            return c;
        }

        public class MapBlock
        {
            public int BlockNo   { get; set; }
            public int ZoneId    { get; set; }
            public int SlotCount { get; set; }
            public int X         { get; set; }
            public int Y         { get; set; }
            public int Occupied  { get; set; }
            public int Fresh     { get; set; }
        }

        public IList<MapBlock> Load()
        {
            var raw = new List<MapBlock>();
            var dx  = new Dictionary<int, double>();
            var dy  = new Dictionary<int, double>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT b.block_no, b.zone_id, b.slot_count, b.origin_x, b.origin_y, " +
                    "  (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id = b.block_id) AS occupied, " +
                    "  (SELECT COUNT(*) FROM plc_slot_state s WHERE s.block_id = b.block_id " +
                    "     AND s.read_at >= NOW() - INTERVAL 5 MINUTE) AS fresh " +
                    "FROM  block b " +
                    "WHERE b.block_no <= 112 AND b.origin_x IS NOT NULL " +
                    "ORDER BY b.block_no";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int no = Convert.ToInt32(r["block_no"]);
                        raw.Add(new MapBlock
                        {
                            BlockNo   = no,
                            ZoneId    = Convert.ToInt32(r["zone_id"]),
                            SlotCount = Convert.ToInt32(r["slot_count"]),
                            Occupied  = Convert.ToInt32(r["occupied"]),
                            Fresh     = Convert.ToInt32(r["fresh"])
                        });
                        dx[no] = Convert.ToDouble(r["origin_x"]);
                        dy[no] = Convert.ToDouble(r["origin_y"]);
                    }
                }
            }

            foreach (var g in raw.GroupBy(b => b.ZoneId))
            {
                double[] box;
                if (!ZoneBox.TryGetValue(g.Key, out box)) continue;

                var list = g.ToList();
                double x0 = list.Min(b => dx[b.BlockNo]), x1 = list.Max(b => dx[b.BlockNo]);
                double y0 = list.Min(b => dy[b.BlockNo]), y1 = list.Max(b => dy[b.BlockNo]);

                double mx = (box[1] - box[0]) * Pad, my = (box[3] - box[2]) * Pad;
                double px0 = box[0] + mx, px1 = box[1] - mx;
                double py0 = box[2] + my, py1 = box[3] - my;

                double[][] poly;
                ZonePoly.TryGetValue(g.Key, out poly);
                double cx = 0, cy = 0;
                if (poly != null)
                {
                    cx = poly.Average(p => p[0]);
                    cy = poly.Average(p => p[1]);
                }

                foreach (var b in list)
                {
                    double fx = x1 > x0 ? (dx[b.BlockNo] - x0) / (x1 - x0) : 0.5;
                    double fy = y1 > y0 ? (dy[b.BlockNo] - y0) / (y1 - y0) : 0.5;

                    double X = px0 + fx * (px1 - px0);
                    double Y = py1 - fy * (py1 - py0);   // bản vẽ y lên, màn hình y xuống

                    if (poly != null)
                    {
                        for (int k = 0; k < 24 && !Inside(X, Y, poly); k++)
                        {
                            X += (cx - X) * 0.12;
                            Y += (cy - Y) * 0.12;
                        }
                    }

                    b.X = (int)Math.Round(X);
                    b.Y = (int)Math.Round(Y);
                }
            }

            return raw;
        }
    }
}
