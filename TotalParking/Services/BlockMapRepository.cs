using System;
using System.Collections.Generic;
using MySqlConnector;

namespace TotalParking.Services
{
    // Toạ độ và trạng thái 112 block để chấm lên sơ đồ điều hướng.
    //
    // ===================== TOẠ ĐỘ LÀ CHÍNH XÁC, KHÔNG PHẢI ƯỚC LƯỢNG =====================
    // `block.map_x/map_y` nằm sẵn trong hệ toạ độ của `Images/plan_map.jpg`, nên
    // lớp này chỉ việc đọc lên. Không còn phép chiếu nào ở đây.
    //
    // Bốn cách căn TRƯỚC ĐÓ đều thất bại — khớp trọng tâm zone (68%), khớp hộp bao
    // vùng màu (tỉ lệ x/y lệch 15.6%), dò khối màu (79%), căn theo từng zone (đúng
    // zone nhưng lệch khỏi khối). Nguyên nhân chung: ảnh nền cũ `zones_map.jpeg`
    // được tạo độc lập với bản vẽ CAD, không có điểm neo chung, nên mọi phép khớp
    // đều là đoán.
    //
    // Cách giải: TỰ RENDER bản vẽ vector ra ảnh. Khi chính mình vẽ bức ảnh thì phép
    // biến đổi là do mình đặt ra, sai số bằng không theo định nghĩa. Chi tiết và
    // cách sinh lại nằm trong Database/34_block_map_xy.sql.
    //
    // ĐỔI ẢNH NỀN thì phải chạy lại migration đó, nếu không chấm sẽ lệch.
    public class BlockMapRepository
    {
        // Khung của Images/plan_map.jpg. Phải khớp với viewBox trong Routing.cshtml.
        // Ảnh được cắt lại rộng hơn để thấy trọn mặt bằng toà nhà: khung cũ 1200x1139 (cắt ôm sát block)
        // ôm sát 112 block nên cắt mất rìa toà nhà. Phép đổi là tịnh tiến thuần tuý
        // (+142, +33), tỉ lệ không đổi — xem Database/34_block_map_xy.sql.
        public const int ViewW = 1594;
        public const int ViewH = 1300;

        // Cổng vào: ram 1 ở phía dưới theo lời khách. Đặt tay ngay dưới cụm block
        // thấp nhất của zone 1 — bản vẽ không có nhãn chữ cho ram nên không suy ra
        // được bằng dữ liệu. Chỉ dùng làm điểm xuất phát cho hoạt ảnh.
        public const int GateX = 1055;
        public const int GateY = 1158;

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
            var list = new List<MapBlock>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT b.block_no, b.zone_id, b.slot_count, b.map_x, b.map_y, " +
                    "  (SELECT COUNT(*) FROM v_slot_taken t WHERE t.block_id = b.block_id) AS occupied, " +
                    "  (SELECT COUNT(*) FROM plc_slot_state s WHERE s.block_id = b.block_id " +
                    "     AND s.read_at >= NOW() - INTERVAL 5 MINUTE) AS fresh " +
                    "FROM  block b " +
                    "WHERE b.map_x IS NOT NULL " +
                    "ORDER BY b.block_no";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new MapBlock
                        {
                            BlockNo   = Convert.ToInt32(r["block_no"]),
                            ZoneId    = Convert.ToInt32(r["zone_id"]),
                            SlotCount = Convert.ToInt32(r["slot_count"]),
                            X         = Convert.ToInt32(r["map_x"]),
                            Y         = Convert.ToInt32(r["map_y"]),
                            Occupied  = Convert.ToInt32(r["occupied"]),
                            Fresh     = Convert.ToInt32(r["fresh"])
                        });
                    }
                }
            }
            return list;
        }
    }
}
