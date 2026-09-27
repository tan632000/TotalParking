using System;
using System.Collections.Generic;
using System.Text;
using MySqlConnector;

namespace TotalParking.Services.Pgs
{
    // Ghi trạng thái từng cảm biến đỗ thường vào pgs_sensor_state.
    //
    // ===================== VÌ SAO CẦN GHI =====================
    // Trước đây hệ thống chỉ giữ số đếm trong bộ nhớ: biết "còn 58 ô trống" mà
    // không biết ô NÀO trống, và tắt máy là mất sạch. Không có bảng nào lưu dữ
    // liệu đỗ thường, nên không trả lời được những câu hỏi cơ bản: xe đỗ bao lâu,
    // giờ nào đông, cảm biến nào hay chết.
    //
    // ===================== CHỈ ĐỌC =====================
    // Lớp này không có đường nào ghi xuống CCU hay ZCU.
    public class PgsSensorStateWriter
    {
        public class KetQua
        {
            public int SoZcu      { get; set; }
            public int SoCamBien  { get; set; }
            public int SoDoi      { get; set; }
            public string Loi     { get; set; }
        }

        // Mỗi khung mang 64 cảm biến (2 lộ × 32). Gộp cả ZCU vào MỘT câu lệnh
        // thay vì 64 câu: ở nhịp 5 giây, 5 ZCU thì đó là khác biệt giữa 1 và 320
        // lượt đi lại tới cơ sở dữ liệu mỗi vòng.
        //
        // Cú pháp `AS moi` thay cho VALUES(): MySQL 8.0.20 đánh dấu VALUES() là
        // lỗi thời và cảnh báo ở mọi lần chạy. Bãi này chạy 8.4.
        //
        // THỨ TỰ HAI DÒNG GÁN LÀ CỐ Ý. MySQL tính các phép gán từ trái sang phải,
        // nên changed_at phải đọc trang_thai CŨ trước khi trang_thai bị ghi đè.
        // Đảo hai dòng thì mọi lần quét đều thấy "không đổi" và changed_at đứng
        // yên vĩnh viễn — hỏng âm thầm, không báo lỗi gì.
        private const string DauSql =
            "INSERT INTO pgs_sensor_state (zcu_id, lo, vi_tri, trang_thai, read_at, changed_at) VALUES ";

        private const string CuoiSql =
            " AS moi ON DUPLICATE KEY UPDATE " +
            "   changed_at = IF(pgs_sensor_state.trang_thai <> moi.trang_thai, moi.read_at, pgs_sensor_state.changed_at), " +
            "   trang_thai = moi.trang_thai, " +
            "   read_at    = moi.read_at";

        public KetQua Ghi(IList<CcuZcuState> zcus)
        {
            var kq = new KetQua();
            if (zcus == null || zcus.Count == 0) return kq;

            var sb = new StringBuilder(DauSql);
            DateTime now = DateTime.Now;
            int n = 0;

            foreach (var z in zcus)
            {
                // ZCU mất kết nối thì chuỗi trạng thái là của lần đọc cuối. Bỏ qua
                // thay vì ghi lại: ghi sẽ làm read_at tươi lên và che mất đúng cái
                // dấu hiệu "số liệu này đã cũ" mà view dùng để quyết định.
                if (!z.DangKetNoi) continue;

                n = Them(sb, z.ZcuId, 1, z.Lo1, n);
                n = Them(sb, z.ZcuId, 2, z.Lo2, n);
                kq.SoZcu++;
            }

            if (n == 0) return kq;

            sb.Append(CuoiSql);
            kq.SoCamBien = n;

            try
            {
                using (var conn = new MySqlConnection(Db.ConnectionString))
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sb.ToString();
                    // MỘT tham số cho cả câu lệnh. Mọi dòng trong cùng một vòng
                    // quét đều mang chung mốc thời gian, nên tạo 320 tham số
                    // giống hệt nhau chỉ làm câu lệnh phình ra vô ích.
                    cmd.Parameters.AddWithValue("@now", now);
                    conn.Open();
                    // Với ON DUPLICATE KEY UPDATE, MySQL trả 1 cho mỗi dòng thêm
                    // mới và 2 cho mỗi dòng bị sửa. Không dùng con số này làm "số
                    // ô đổi trạng thái" — vòng nào cũng sửa read_at nên nó luôn
                    // bằng 2×n. Số ô đổi thật đọc bằng changed_at.
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                kq.Loi = ex.Message;
            }
            return kq;
        }

        // Trả về tổng số dòng đã ghép, kể cả các lộ trước. Dùng nó để biết có
        // cần dấu phẩy ngăn cách hay không.
        private static int Them(StringBuilder sb, int zcuId, int lo, string chuoi, int daCo)
        {
            if (string.IsNullOrEmpty(chuoi)) return daCo;

            for (int i = 0; i < chuoi.Length; i++)
            {
                char c = chuoi[i];
                // Ký tự ngoài 0..3 nghĩa là firmware đổi bảng mã. Bỏ qua dòng đó
                // thay vì ghi một con số bịa; CcuFrame đã đếm riêng vào KhongHieu
                // nên chuyện này vẫn nhìn thấy được ở /PgsStatus.
                if (c < '0' || c > '3') continue;

                if (daCo > 0) sb.Append(',');
                // zcuId, lo, vi_tri và trạng thái đều là số nguyên do chính lớp
                // này sinh ra từ khung đã kiểm CRC, không phải chuỗi từ bên ngoài.
                sb.Append("(").Append(zcuId).Append(",").Append(lo).Append(",")
                  .Append(i + 1).Append(",").Append(c - '0')
                  .Append(",@now,@now)");
                daCo++;
            }
            return daCo;
        }
    }
}
