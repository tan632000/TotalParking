using System;
using System.Collections.Generic;
using System.Data;
using MySqlConnector;
using TotalParking.Models;

namespace TotalParking.Services
{
    // Đọc cấu hình PLC. Bảng này đổi rất hiếm nên chỉ nạp lúc khởi động và khi
    // có thao tác cấu hình — không truy vấn mỗi vòng poll.
    //
    // Trạng thái kết nối (online, lần bắt tay cuối) KHÔNG nằm ở đây: 112 PLC
    // poll 500ms sẽ thành hàng chục UPDATE mỗi giây vào một bảng cấu hình, và
    // mọi truy vấn đọc cấu hình phải chờ khoá hàng. Trạng thái sống giữ trong
    // bộ nhớ ở PlcConnectionManager.
    public class PlcDeviceRepository
    {
        private const string SelectSql =
            "SELECT p.plc_id, p.block_id, b.block_no, b.zone_id, " +
            "       p.ip_address, p.port, p.plc_node, p.pc_node, " +
            "       p.timeout_ms, p.poll_ms, " +
            "       p.card_word, p.card_word_len, p.card_layout, " +
            "       p.request_bit, p.request_bit_area, " +
            "       p.permit_bit, p.permit_bit_area, p.class_word, p.is_active " +
            "FROM   plc_device p " +
            "JOIN   block b ON b.block_id = p.block_id ";

        public IList<PlcDevice> GetAll(bool activeOnly = true)
        {
            var result = new List<PlcDevice>();

            using (var conn = new MySqlConnection(Db.ConnectionString))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = SelectSql +
                    (activeOnly ? "WHERE p.is_active = 1 AND b.is_active = 1 " : "") +
                    "ORDER BY b.block_no";

                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()) result.Add(Map(reader));
                }
            }

            return result;
        }

        // Convert.To* thay vi GetInt32: cot TINYINT/SMALLINT UNSIGNED tra ve
        // kieu nho hon Int32, va GetInt32 tren mot so kieu se nem InvalidCast.
        // Day cung la khuon ma ParkingCardRepository dang dung.
        private static PlcDevice Map(IDataRecord r)
        {
            return new PlcDevice
            {
                PlcId          = Convert.ToInt32(r["plc_id"]),
                BlockId        = Convert.ToInt32(r["block_id"]),
                BlockNo        = Convert.ToInt32(r["block_no"]),
                ZoneId         = Convert.ToInt32(r["zone_id"]),
                IpAddress      = Convert.ToString(r["ip_address"]),
                Port           = Convert.ToInt32(r["port"]),
                PlcNode        = Convert.ToByte(r["plc_node"]),
                PcNode         = Convert.ToByte(r["pc_node"]),
                TimeoutMs      = Convert.ToInt32(r["timeout_ms"]),
                PollMs         = Convert.ToInt32(r["poll_ms"]),
                CardWord       = Convert.ToInt32(r["card_word"]),
                CardWordLen    = Convert.ToInt32(r["card_word_len"]),
                CardLayout     = GetNullableString(r, "card_layout"),
                RequestBit     = GetNullableString(r, "request_bit"),
                RequestBitArea = Convert.ToString(r["request_bit_area"]),
                PermitBit      = Convert.ToString(r["permit_bit"]),
                PermitBitArea  = Convert.ToString(r["permit_bit_area"]),
                ClassWord      = Convert.ToInt32(r["class_word"]),
                IsActive       = Convert.ToBoolean(r["is_active"])
            };
        }

        private static string GetNullableString(IDataRecord r, string column)
        {
            object v = r[column];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }
    }
}
