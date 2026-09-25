using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Web.Hosting;

namespace TotalParking.Services
{
    // Nhật ký thao tác quản trị thẻ: sửa thông tin, tắt và bật lại.
    //
    // ===================== VÌ SAO KHÔNG DÙNG CHUNG plc_audit.log =====================
    // PlcAuditLog xoay file ở 8 MB và giữ đúng một bản cũ, trong khi vòng quét ô
    // đỗ đổ hàng trăm dòng mỗi phút vào đó. Một thao tác quản trị ghi vào chung
    // file sẽ bị cuốn trôi trong vài ngày — đúng lúc câu hỏi "ai đổi thẻ này"
    // được đặt ra thì không còn gì để đọc.
    //
    // File riêng chỉ nhận ghi từ đường quản trị, nên nó lớn rất chậm và giữ được
    // lịch sử dài hơn nhiều.
    //
    // ===================== TÊN NGƯỜI THAO TÁC LÀ TỰ KHAI =====================
    // Hệ thống chưa có đăng nhập. Tên gửi lên là người vận hành tự gõ, nên nó là
    // dấu vết vận hành chứ không phải danh tính đã xác thực. Ghi kèm địa chỉ máy
    // khách để còn có thứ đối chiếu.
    public static class CardAdminLog
    {
        private static readonly object Sync = new object();
        private const long MaxBytes = 4L * 1024 * 1024;

        private static string _path;
        private static bool _resolved;

        public static string Path
        {
            get
            {
                if (_resolved) return _path;
                lock (Sync)
                {
                    if (_resolved) return _path;
                    string cfg = ConfigurationManager.AppSettings["card:adminLogPath"];
                    if (string.IsNullOrWhiteSpace(cfg)) cfg = "~/App_Data/card_admin.log";
                    try
                    {
                        _path = cfg.StartsWith("~") ? HostingEnvironment.MapPath(cfg) : cfg;
                    }
                    catch { _path = null; }
                    _resolved = true;
                    return _path;
                }
            }
        }

        // Ghi một dòng. Các trường phân tách bằng tab để còn mở được bằng Excel.
        //
        // Ghi cả lần BỊ TỪ CHỐI chứ không chỉ lần thành công: "vì sao thẻ này vẫn
        // dùng được dù tôi đã bấm tắt" là câu hỏi chỉ trả lời được khi có dòng
        // ghi lại lần bấm bị chặn.
        public static void Ghi(string thaoTac, int cardId, string cardCode,
                               string nguoiTuKhai, string ip, string ketQua, string chiTiet)
        {
            string p = Path;
            if (string.IsNullOrEmpty(p)) return;

            // Khử tab và xuống dòng khỏi MỌI trường trước khi ghép.
            //
            // Tên người, biển số và tên khách đều do người gửi POST đặt. Không khử
            // thì một giá trị chứa "\r\n...\t..." sẽ chèn được một dòng nhật ký
            // hoàn chỉnh giả — và vì hệ thống chưa có đăng nhập, file này là vật
            // chứng trách nhiệm duy nhất. Làm giả được nó thì nó hết giá trị.
            Func<string, string> sach = (v) => (v ?? "")
                .Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

            string dong = string.Join("\t",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                sach(thaoTac),
                "card_id=" + cardId.ToString(),
                sach(cardCode ?? "-"),
                string.IsNullOrWhiteSpace(nguoiTuKhai) ? "-" : sach(nguoiTuKhai.Trim()),
                sach(ip ?? "-"),
                sach(ketQua),
                sach(chiTiet));

            try
            {
                lock (Sync)
                {
                    string thuMuc = System.IO.Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(thuMuc) && !Directory.Exists(thuMuc))
                        Directory.CreateDirectory(thuMuc);

                    if (File.Exists(p) && new FileInfo(p).Length > MaxBytes)
                    {
                        string cu = p + ".1";
                        if (File.Exists(cu)) File.Delete(cu);
                        File.Move(p, cu);
                    }

                    File.AppendAllText(p, dong + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
                // Không ghi được nhật ký thì cũng không được làm hỏng thao tác đang
                // chạy. Người vận hành vừa bấm lưu; ném lỗi ở đây nghĩa là họ thấy
                // thất bại trong khi CSDL đã đổi rồi.
            }
        }
    }
}
