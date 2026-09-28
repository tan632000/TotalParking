using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Đọc danh mục bảng LED và số chỗ trống theo chiều dài khoang.
    //
    // Cấu hình bảng đổi rất hiếm nên chỉ nạp lúc khởi động; số chỗ trống thì
    // đọc mỗi nhịp publish, nhưng đó là một truy vấn view trả đúng một dòng.
    public class LedPanelRepository
    {
        public IList<LedPanel> GetPanels(bool activeOnly = true)
        {
            var panels = new List<LedPanel>();
            var byId   = new Dictionary<int, LedPanel>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            {
                conn.Open();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT panel_id, code, name, ip_address, port, hub_type, kind, " +
                        "       pos_x, pos_y, note, is_active " +
                        "FROM   led_panel " +
                        (activeOnly ? "WHERE is_active = 1 " : "") +
                        "ORDER BY code";

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var p = new LedPanel
                            {
                                PanelId   = Convert.ToInt32(r["panel_id"]),
                                Code      = Convert.ToString(r["code"]),
                                Name      = Str(r, "name"),
                                IpAddress = Convert.ToString(r["ip_address"]),
                                Port      = Convert.ToInt32(r["port"]),
                                HubType   = Convert.ToString(r["hub_type"]),
                                Kind      = Convert.ToString(r["kind"]),
                                PosX      = NullableInt(r, "pos_x"),
                                PosY      = NullableInt(r, "pos_y"),
                                Note      = Str(r, "note"),
                                IsActive  = Convert.ToBoolean(r["is_active"])
                            };
                            panels.Add(p);
                            byId[p.PanelId] = p;
                        }
                    }
                }

                if (panels.Count == 0) return panels;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT panel_id, port_index, arrow_direction, arrow_color, " +
                        "       arrow_state, scope, zone_list, is_active " +
                        "FROM   led_panel_port " +
                        (activeOnly ? "WHERE is_active = 1 " : "") +
                        "ORDER BY panel_id, port_index";

                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int id = Convert.ToInt32(r["panel_id"]);
                            LedPanel panel;
                            if (!byId.TryGetValue(id, out panel)) continue;

                            panel.Ports.Add(new LedPanelPort
                            {
                                PanelId        = id,
                                PortIndex      = Convert.ToInt32(r["port_index"]),
                                ArrowDirection = Convert.ToInt32(r["arrow_direction"]),
                                ArrowColor     = Convert.ToInt32(r["arrow_color"]),
                                ArrowState     = Convert.ToInt32(r["arrow_state"]),
                                Scope          = Convert.ToString(r["scope"]),
                                ZoneList       = Str(r, "zone_list"),
                                IsActive       = Convert.ToBoolean(r["is_active"])
                            });
                        }
                    }
                }
            }

            return panels;
        }

        public LedCapacity GetCapacity()
        {
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM v_led_capacity";
                conn.Open();

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return new LedCapacity();

                    var cap = new LedCapacity
                    {
                        FreeL5m        = Num(r, "free_l5m"),
                        FreeL48m       = Num(r, "free_l48m"),
                        FreeStandard   = Num(r, "free_standard"),
                        TotalL5m       = Num(r, "total_l5m"),
                        TotalL48m      = Num(r, "total_l48m"),
                        TotalStandard  = Num(r, "total_standard"),
                        UsedUnassigned = Num(r, "used_unassigned"),
                        SlotsTotal     = Num(r, "slots_total"),
                        SlotsFresh     = Num(r, "slots_fresh"),
                        StandardSensors = Num(r, "standard_sensors"),
                        StandardFresh   = Num(r, "standard_fresh")
                    };

                    // free_standard giờ đọc thẳng từ view, không ghi đè nữa.
                    //
                    // Trước đây lớp này gọi StandardFreeSource để lấy số từ bộ
                    // nhớ, vì view tính đỗ thường bằng "tổng ô - số phiên gửi xe"
                    // mà khu đỗ thường không phát thẻ nên luôn ra nguyên sức
                    // chứa. Hệ quả: bảng LED đúng còn trang Điều hướng xe sai,
                    // hai màn hình nói hai điều về cùng một bãi.
                    //
                    // Migration 48 cho view đọc pgs_sensor_state, nên nguồn đã
                    // đúng ngay tại gốc. Giữ thêm đường ghi đè chỉ làm sự cố của
                    // vòng ghi bị che đi: bảng LED vẫn đúng nhờ bộ nhớ, còn mọi
                    // nơi khác lặng lẽ sai. StandardFresh ở trên là thứ để nhìn
                    // thấy sự cố đó thay vì che nó.
                    return cap;
                }
            }
        }

        // Sức chứa theo TỪNG ZONE. Bảng chỉ hướng phải hiện số của zone mà mũi
        // tên dẫn tới, không phải số toàn bãi — nếu không thì tài xế rẽ theo mũi
        // tên rồi mới biết zone đó đã đầy.
        public IDictionary<int, LedCapacity> GetCapacityByZone()
        {
            var map = new Dictionary<int, LedCapacity>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM v_led_capacity_zone";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        map[Num(r, "zone_id")] = new LedCapacity
                        {
                            FreeL5m       = Num(r, "free_l5m"),
                            FreeL48m      = Num(r, "free_l48m"),
                            FreeStandard  = Num(r, "free_standard"),
                            TotalL5m      = Num(r, "total_l5m"),
                            TotalL48m     = Num(r, "total_l48m"),
                            TotalStandard = Num(r, "total_standard"),
                            SlotsTotal    = Num(r, "slots_total"),
                            SlotsFresh    = Num(r, "slots_fresh"),
                            // Cùng lý do như đường toàn bãi: 0 cảm biến tươi
                            // nghĩa là free_standard đang là sức chứa dự phòng
                            // chứ không phải số đo.
                            StandardSensors = Num(r, "standard_sensors"),
                            StandardFresh   = Num(r, "standard_fresh")
                        };
                    }
                }
            }
            return map;
        }

        // Sức chứa theo TỪNG CỔNG, cho các cổng khai `scope = 'BLOCKS'`.
        //
        // Khác đường theo zone ở chỗ KHÔNG phải cộng gì trong C#: view
        // v_led_capacity_port đã gộp sẵn theo đúng danh sách block và cảm biến
        // của từng mũi tên. Công thức nằm một chỗ duy nhất, nên không có cửa
        // cho hai nơi tính ra hai số khác nhau như từng xảy ra giữa bảng LED và
        // trang Điều hướng xe.
        public IDictionary<Tuple<int, int>, LedCapacity> GetCapacityByPort()
        {
            var map = new Dictionary<Tuple<int, int>, LedCapacity>();
            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT * FROM v_led_capacity_port";
                conn.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var key = Tuple.Create(Num(r, "panel_id"), Num(r, "port_index"));
                        map[key] = new LedCapacity
                        {
                            FreeL5m       = Num(r, "free_l5m"),
                            FreeL48m      = Num(r, "free_l48m"),
                            FreeStandard  = Num(r, "free_standard"),
                            TotalL5m      = Num(r, "total_l5m"),
                            TotalL48m     = Num(r, "total_l48m"),
                            TotalStandard = Num(r, "total_standard"),
                            SlotsTotal    = Num(r, "slots_total"),
                            SlotsFresh    = Num(r, "slots_fresh"),
                            // Cùng lý do như hai đường kia: 0 cảm biến tươi nghĩa
                            // là free_standard đang là sức chứa dự phòng chứ không
                            // phải số đo.
                            StandardSensors = Num(r, "standard_sensors"),
                            StandardFresh   = Num(r, "standard_fresh")
                        };
                    }
                }
            }
            return map;
        }

        // Cộng dồn sức chứa của các zone mà một mũi tên dẫn tới.
        //
        // Trả về null khi KHÔNG tra được zone nào trong danh sách — phía gọi phải
        // coi đó là "chưa có dữ liệu" và xoá trắng bảng, chứ không được rơi về số
        // toàn bãi. Đẩy số toàn bãi lên một mũi tên chỉ về một hướng cụ thể là nói
        // với tài xế rằng hướng đó có ngần ấy chỗ.
        public static LedCapacity SumZones(IDictionary<int, LedCapacity> byZone, string zoneList)
        {
            if (byZone == null || string.IsNullOrWhiteSpace(zoneList)) return null;

            var sum = new LedCapacity();
            int found = 0;
            foreach (var part in zoneList.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int z;
                if (!int.TryParse(part.Trim(), out z)) continue;
                LedCapacity c;
                if (!byZone.TryGetValue(z, out c)) continue;
                found++;
                sum.FreeL5m       += c.FreeL5m;
                sum.FreeL48m      += c.FreeL48m;
                sum.FreeStandard  += c.FreeStandard;
                sum.TotalL5m      += c.TotalL5m;
                sum.TotalL48m     += c.TotalL48m;
                sum.TotalStandard += c.TotalStandard;
                // Độ phủ cộng dồn theo SỐ Ô, không lấy trung bình phần trăm:
                // mũi tên dẫn tới ba zone thì tài xế quan tâm tổng số ô mà hệ
                // thống đang thật sự nhìn thấy, chứ không phải trung bình cộng
                // của ba tỉ lệ — zone nhỏ sẽ kéo lệch con số đó.
                sum.SlotsTotal    += c.SlotsTotal;
                sum.SlotsFresh    += c.SlotsFresh;
                // Cộng luôn độ phủ của tầng cảm biến. Bỏ qua thì bảng chỉ hướng
                // luôn báo 0 cảm biến tươi, và người vận hành không phân biệt
                // được "zone này còn trống thật" với "vòng ghi vừa chết".
                sum.StandardSensors += c.StandardSensors;
                sum.StandardFresh   += c.StandardFresh;
            }
            return found > 0 ? sum : null;
        }

        private static int Num(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }

        private static int? NullableInt(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? (int?)null : Convert.ToInt32(v);
        }

        private static string Str(IDataRecord r, string col)
        {
            object v = r[col];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }
    }
}
