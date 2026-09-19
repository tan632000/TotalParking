using System;
using System.Collections.Generic;
using MySqlConnector;

namespace TotalParking.Services
{
    // Mạng lưới làn đường và đường đi ngắn nhất từ đầu dốc tới một block.
    //
    // ===================== VÌ SAO KHÔNG VẼ ĐƯỜNG THẲNG =====================
    // Trang điều hướng cũ vẽ một đoạn thẳng từ cổng tới block. Trên màn hình cho
    // tài xế, một nét cắt ngang qua các block đang đỗ chính là lệnh "lái xuyên qua
    // chỗ đỗ". Ở đây đường đi bám theo làn đã số hoá trong `lane_node`/`lane_edge`,
    // nên mọi đoạn của nó đều là chỗ xe đi được thật.
    //
    // ===================== KHÔNG CÓ ĐƯỜNG LUI HÌNH HỌC =====================
    // Khi block đích không nối được với đầu dốc, lớp này trả về "không có đường"
    // kèm số block, KHÔNG trả về đoạn thẳng, đường bẻ góc vuông, hay phần đường đã
    // dò được. Một đường đi sai còn tệ hơn là không có đường nào: tài xế tin vào nó.
    public class LaneNetwork
    {
        // Một điểm trên đường đi, trong khung toạ độ của plan_map.jpg.
        public class LanePoint
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public class RouteResult
        {
            public bool             Found  { get; set; }
            public IList<LanePoint> Points { get; set; }
            // Chỉ có giá trị khi Found = false, và luôn gọi tên block đang xét.
            public string           Reason { get; set; }

            public static RouteResult No(string reason)
            {
                return new RouteResult { Found = false, Points = new List<LanePoint>(), Reason = reason };
            }
        }

        // Đọc cả đồ thị cho mỗi lần gọi. 313 nút và 341 cạnh trên MySQL cùng máy là
        // hai truy vấn nhỏ; giữ nguyên như vậy để một lần sửa dữ liệu làn đường có
        // hiệu lực ngay, không phải chờ hết hạn cache hay khởi động lại site.
        public RouteResult RouteToBlock(int blockNo)
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();

                var x = new Dictionary<int, int>();
                var y = new Dictionary<int, int>();
                int entry = -1;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT node_id, x, y, is_entry FROM lane_node";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int id = Convert.ToInt32(r["node_id"]);
                            x[id] = Convert.ToInt32(r["x"]);
                            y[id] = Convert.ToInt32(r["y"]);
                            if (Convert.ToInt32(r["is_entry"]) == 1) entry = id;
                        }
                    }
                }

                if (entry < 0)
                    return RouteResult.No("Chua co nut dau doc trong mang luoi lan duong.");

                int? target = ArrivalNodeOf(conn, blockNo);
                if (!target.HasValue)
                    return RouteResult.No("Block " + blockNo + " chua duoc gan nut lan duong.");

                var adj = LoadEdges(conn, x);
                return Search(entry, target.Value, x, y, adj, blockNo);
            }
        }

        private static int? ArrivalNodeOf(MySqlConnection conn, int blockNo)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT lane_node_id FROM block WHERE block_no = @b";
                cmd.Parameters.AddWithValue("@b", blockNo);
                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return null;
                return Convert.ToInt32(v);
            }
        }

        // C3: cạnh là VÔ HƯỚNG và chỉ lưu một lần cho mỗi cặp. Mở ra hai chiều ở
        // đây, vì nếu không thì một nửa số hướng đi sẽ không tồn tại với Dijkstra.
        private static Dictionary<int, List<int>> LoadEdges(MySqlConnection conn, Dictionary<int, int> known)
        {
            var adj = new Dictionary<int, List<int>>();
            foreach (var id in known.Keys) adj[id] = new List<int>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT from_node, to_node FROM lane_edge";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        int a = Convert.ToInt32(r["from_node"]);
                        int b = Convert.ToInt32(r["to_node"]);
                        if (!adj.ContainsKey(a) || !adj.ContainsKey(b)) continue;
                        adj[a].Add(b);
                        adj[b].Add(a);
                    }
                }
            }
            return adj;
        }

        // Dijkstra quét tuyến tính thay vì dùng hàng đợi ưu tiên. Với vài trăm nút
        // thì chi phí không đáng kể, đổi lại chọn nút kế tiếp là một phép so sánh
        // tường minh: rẻ hơn thì thắng, hoà thì `node_id` nhỏ hơn thắng. Nhờ vậy
        // hai lần gọi liên tiếp trên cùng dữ liệu luôn cho ra ĐÚNG một dãy điểm,
        // kể cả khi có nhiều đường cùng độ dài — tài xế không thấy đường nhảy.
        private static RouteResult Search(int entry, int target,
                                          Dictionary<int, int> x, Dictionary<int, int> y,
                                          Dictionary<int, List<int>> adj, int blockNo)
        {
            var dist  = new Dictionary<int, double>();
            var prev  = new Dictionary<int, int>();
            var doneN = new HashSet<int>();
            foreach (var id in x.Keys) dist[id] = double.PositiveInfinity;
            dist[entry] = 0;

            while (true)
            {
                int cur = -1;
                double best = double.PositiveInfinity;
                foreach (var id in x.Keys)
                {
                    if (doneN.Contains(id)) continue;
                    double d = dist[id];
                    if (d < best || (d == best && cur >= 0 && id < cur)) { best = d; cur = id; }
                }

                if (cur < 0 || double.IsPositiveInfinity(best)) break;
                if (cur == target) break;

                doneN.Add(cur);
                foreach (var next in adj[cur])
                {
                    if (doneN.Contains(next)) continue;
                    double step = Distance(x[cur], y[cur], x[next], y[next]);
                    double alt  = dist[cur] + step;
                    // Hoà thì giữ nút trước có id nhỏ hơn, cùng một lý do xác định
                    // như trên: đường đi phải lặp lại y hệt giữa hai lần gọi.
                    if (alt < dist[next] || (alt == dist[next] && prev.ContainsKey(next) && cur < prev[next]))
                    {
                        dist[next] = alt;
                        prev[next] = cur;
                    }
                }
            }

            if (double.IsPositiveInfinity(dist[target]))
                return RouteResult.No("Khong co duong lan noi tu dau doc toi block " + blockNo + ".");

            var reversed = new List<int>();
            int walk = target;
            reversed.Add(walk);
            while (walk != entry)
            {
                if (!prev.ContainsKey(walk))
                    return RouteResult.No("Khong co duong lan noi tu dau doc toi block " + blockNo + ".");
                walk = prev[walk];
                reversed.Add(walk);
            }
            reversed.Reverse();

            var points = new List<LanePoint>();
            foreach (var id in reversed) points.Add(new LanePoint { X = x[id], Y = y[id] });

            return new RouteResult { Found = true, Points = points, Reason = null };
        }

        private static double Distance(int x1, int y1, int x2, int y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
