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
                        "SELECT panel_id, code, ip_address, port, hub_type, kind, " +
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

                    return new LedCapacity
                    {
                        FreeL5m        = Num(r, "free_l5m"),
                        FreeL48m       = Num(r, "free_l48m"),
                        FreeStandard   = Num(r, "free_standard"),
                        TotalL5m       = Num(r, "total_l5m"),
                        TotalL48m      = Num(r, "total_l48m"),
                        TotalStandard  = Num(r, "total_standard"),
                        UsedUnassigned = Num(r, "used_unassigned")
                    };
                }
            }
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
