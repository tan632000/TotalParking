using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Hosting;
using Newtonsoft.Json.Linq;

namespace TotalParking.Services
{
    // Mạng lối đi cho MÀN HÌNH TÀI XẾ, trong khung ảnh zones_map.jpeg (4800×3584).
    //
    // ===================== VÌ SAO KHÔNG DÙNG lane_node =====================
    // Mạng `lane_node`/`lane_edge` được dò trên plan_map.jpg từ mọi khoảng trống và
    // xuất phát ở cổng cũ. Màn tài xế cần đi theo dải vàng của zones_map và luôn
    // bắt đầu ở ram dốc 1, nên mạng của nó là file `App_Data/driver_lanes.json`
    // do `tools/so_hoa_duong_tai_xe.py` sinh và `tools/kiem_tra_duong_tai_xe.py`
    // kiểm. Trang Điều hướng xe vẫn dùng `LaneNetwork` + plan_map.
    //
    // ===================== KHÔNG BAO GIỜ NÉM LỖI =====================
    // Mọi lỗi (thiếu file, JSON hỏng, sai khung, nút treo, block lạ) thành
    // `RouteResult.No(lý do)`, để DriverRoute trả MESSAGE thay vì 500. Không có
    // đường lui hình học: dữ liệu sai thì im lặng, không vẽ đường đoán.
    public class DriverLaneMap
    {
        public const int FrameW = 4800;
        public const int FrameH = 3584;

        // Dữ liệu đã nạp và đã kiểm, không bao giờ sửa sau khi tạo. Một request
        // đang tìm đường giữ tham chiếu của nó, nên lần nạp lại song song không
        // làm nó thấy nửa cũ nửa mới.
        private sealed class Snapshot
        {
            public string Path;
            public DateTime Stamp;
            public int Entry;
            public Dictionary<int, int> X = new Dictionary<int, int>();
            public Dictionary<int, int> Y = new Dictionary<int, int>();
            public Dictionary<int, List<int>> Adj = new Dictionary<int, List<int>>();
            public Dictionary<int, int[]> Blocks = new Dictionary<int, int[]>(); // block_no -> {x, y, node}
        }

        private readonly Func<string> _resolvePath;
        // Chỉ giữ bản nạp THÀNH CÔNG. Lần nạp lỗi không ghi vào đây, nên sửa file
        // xong là lần gọi kế tiếp nạp lại được ngay.
        private volatile Snapshot _snapshot;

        // Constructor không đọc file: controller giữ instance tĩnh, và một lỗi lúc
        // khởi tạo kiểu tĩnh sẽ làm hỏng cả controller chứ không riêng màn tài xế.
        public DriverLaneMap(Func<string> resolvePath)
        {
            _resolvePath = resolvePath;
        }

        public DriverLaneMap(string path) : this(() => path) { }

        public static DriverLaneMap ForSite()
        {
            return new DriverLaneMap(() => HostingEnvironment.MapPath("~/App_Data/driver_lanes.json"));
        }

        public LaneNetwork.RouteResult RouteToBlock(int blockNo)
        {
            try
            {
                string error;
                var snap = Load(out error);
                if (snap == null) return LaneNetwork.RouteResult.No(error);

                int[] block;
                if (!snap.Blocks.TryGetValue(blockNo, out block))
                    return LaneNetwork.RouteResult.No("Block " + blockNo + " chua co tren ban do lan duong tai xe.");

                var route = LaneNetwork.Search(snap.Entry, block[2], snap.X, snap.Y, snap.Adj, blockNo);
                if (!route.Found) return route;

                // Chặng cuối: từ nút đến trên làn vào tới đúng vị trí block, để vòng
                // khoanh nằm trên block chứ không trên làn giữa hai dãy block.
                route.Points.Add(new LaneNetwork.LanePoint { X = block[0], Y = block[1] });
                return route;
            }
            catch (Exception ex)
            {
                return LaneNetwork.RouteResult.No("Loi ban do lan duong tai xe: " + ex.Message);
            }
        }

        private Snapshot Load(out string error)
        {
            error = null;
            string path = _resolvePath();
            if (string.IsNullOrEmpty(path))
            {
                error = "Khong xac dinh duoc duong dan file ban do lan duong tai xe.";
                return null;
            }

            var info = new FileInfo(path);
            if (!info.Exists)
            {
                error = "Thieu file ban do lan duong tai xe (" + info.Name + ").";
                return null;
            }

            var current = _snapshot;
            DateTime stamp = info.LastWriteTimeUtc;
            if (current != null && current.Path == path && current.Stamp == stamp)
                return current;

            var fresh = Parse(path, stamp, out error);
            if (fresh != null) _snapshot = fresh;
            return fresh;
        }

        // Kiểm toàn vẹn TRƯỚC khi tìm đường: Dijkstra tra `Adj[id]`, `X[id]` không
        // kiểm, nên một id treo ở đây sẽ thành KeyNotFoundException ở đó.
        private static Snapshot Parse(string path, DateTime stamp, out string error)
        {
            error = null;
            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                error = "File ban do lan duong tai xe hong: " + ex.Message;
                return null;
            }

            try
            {
                var frame = root["frame"];
                int w = frame == null ? 0 : (int)frame["w"];
                int h = frame == null ? 0 : (int)frame["h"];
                if (w != FrameW || h != FrameH)
                {
                    error = "Ban do lan duong tai xe sai khung " + w + "x" + h + ", can " + FrameW + "x" + FrameH + ".";
                    return null;
                }

                var s = new Snapshot { Path = path, Stamp = stamp, Entry = (int)root["entry"] };

                foreach (var n in (JArray)root["nodes"])
                {
                    int id = (int)n["id"];
                    if (s.X.ContainsKey(id)) { error = "Nut " + id + " bi trung trong ban do lan duong tai xe."; return null; }
                    s.X[id] = (int)n["x"];
                    s.Y[id] = (int)n["y"];
                    s.Adj[id] = new List<int>();
                }

                if (!s.X.ContainsKey(s.Entry)) { error = "Nut xuat phat " + s.Entry + " khong co trong ban do lan duong tai xe."; return null; }

                // Cạnh vô hướng, lưu một lần mỗi cặp: mở ra hai chiều như LaneNetwork.
                foreach (var e in (JArray)root["edges"])
                {
                    int a = (int)e[0], b = (int)e[1];
                    if (!s.Adj.ContainsKey(a) || !s.Adj.ContainsKey(b))
                    {
                        error = "Canh " + a + "-" + b + " tro toi nut khong ton tai trong ban do lan duong tai xe.";
                        return null;
                    }
                    s.Adj[a].Add(b);
                    s.Adj[b].Add(a);
                }

                foreach (var p in (JObject)root["blocks"])
                {
                    int no = int.Parse(p.Key, NumberStyles.None, CultureInfo.InvariantCulture);
                    int node = (int)p.Value["node"];
                    if (!s.X.ContainsKey(node))
                    {
                        error = "Block " + no + " tro toi nut " + node + " khong ton tai trong ban do lan duong tai xe.";
                        return null;
                    }
                    s.Blocks[no] = new[] { (int)p.Value["x"], (int)p.Value["y"], node };
                }

                return s;
            }
            catch (Exception ex)
            {
                // Thiếu trường, sai kiểu (null, chuỗi thay cho số), sai cấu trúc mảng.
                error = "File ban do lan duong tai xe sai cau truc: " + ex.Message;
                return null;
            }
        }
    }
}