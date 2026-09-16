using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Hosting;

namespace TotalParking.Services.Plc
{
    // Nhật ký mọi thao tác ĐỌC/GHI thanh ghi PLC, ra file.
    //
    // Mục đích: trả lời được câu "lúc đó hệ thống đọc từ IP nào, ghi xuống thanh
    // ghi của PLC nào". Mỗi dòng đều mang ĐẦY ĐỦ danh tính — IP, số block, tên
    // thanh ghi — chứ không chỉ block_no. Chỉ ghi block_no là không đủ: ánh xạ
    // block -> IP đã đổi mấy lần và vẫn chưa được thiết bị xác nhận, nên nhật ký
    // phải ghi lại ĐỊA CHỈ THẬT đã gửi gói tin tới, không phải thứ suy ra từ DB.
    //
    // ===================== VÌ SAO KHÔNG GHI MỌI LẦN ĐỌC =====================
    // Vòng poll chạy 51 PLC × ~2 lần/giây = hơn 100 lệnh đọc mỗi giây. Ghi hết là
    // 360 nghìn dòng mỗi giờ, và thứ cần tìm sẽ chìm trong đó.
    //
    //   GHI     : luôn luôn. Hiếm, và là thao tác tác động lên thiết bị.
    //   ĐỌC     : chỉ khi giá trị KHÁC lần trước, hoặc khác 0.
    //   LỖI     : luôn luôn.
    //   Vòng quét ô: một dòng tổng kết cho mỗi block, cộng một dòng cho mỗi ô ĐỔI.
    public static class PlcAuditLog
    {
        private static readonly object Sync = new object();
        private static string _path;
        private static bool   _resolved;

        // Xoay file khi quá cỡ. Giữ đúng một file cũ (.1): mục đích là truy vết
        // sự cố vừa xảy ra, không phải lưu trữ lâu dài.
        private const long MaxBytes = 8L * 1024 * 1024;

        public static bool Enabled
        {
            get
            {
                bool b;
                string v = ConfigurationManager.AppSettings["plc:auditEnabled"];
                return !bool.TryParse(v, out b) || b;   // mặc định BẬT
            }
        }

        public static string Path
        {
            get
            {
                if (_resolved) return _path;
                lock (Sync)
                {
                    if (_resolved) return _path;
                    string cfg = ConfigurationManager.AppSettings["plc:auditPath"];
                    if (string.IsNullOrWhiteSpace(cfg)) cfg = "~/App_Data/plc_audit.log";
                    try
                    {
                        _path = cfg.StartsWith("~")
                            ? HostingEnvironment.MapPath(cfg)
                            : cfg;
                    }
                    catch { _path = null; }
                    _resolved = true;
                    return _path;
                }
            }
        }

        // ------------------------------------------------------------ ghi xuống PLC
        public static void Write(string ip, int blockNo, string register, int value,
                                 bool ok, string note = null)
        {
            Append("WRITE", ip, blockNo,
                   string.Format("{0,-10} <- {1,-6}", register, value),
                   ok ? "OK" : "LOI",
                   note);
        }

        // ------------------------------------------------------------ đọc từ PLC
        public static void Read(string ip, int blockNo, string register, string hex,
                                string note = null)
        {
            Append("READ", ip, blockNo,
                   string.Format("{0,-10} =  {1}", register, hex),
                   "", note);
        }

        // ------------------------------------------------------------ lỗi
        public static void Error(string ip, int blockNo, string op, string message)
        {
            Append("ERROR", ip, blockNo, op, "", message);
        }

        // Dòng SCAN chứng minh "đã đọc từ IP này lúc này" — đúng thứ cần để truy
        // vết. Nhưng 51 block × mỗi 45 giây là ~68 dòng/phút, đủ để chôn vùi sự
        // kiện đáng quan tâm. Tắt được bằng plc:auditScanLines = false; lúc đó chỉ
        // còn dòng SCAN của block CÓ ô đổi trạng thái.
        public static bool ScanLines
        {
            get
            {
                bool b;
                string v = ConfigurationManager.AppSettings["plc:auditScanLines"];
                return !bool.TryParse(v, out b) || b;   // mặc định BẬT
            }
        }

        public static void Scan(string ip, int blockNo, int slots, int changed, string note = null)
        {
            if (!ScanLines && changed == 0) return;
            Append("SCAN", ip, blockNo,
                   string.Format("{0} o, {1} doi", slots, changed),
                   "", note);
        }

        private static void Append(string kind, string ip, int blockNo,
                                   string detail, string status, string note)
        {
            if (!Enabled) return;
            string path = Path;
            if (string.IsNullOrEmpty(path)) return;

            // IP là cột quan trọng nhất của cả file — đặt ngay sau loại thao tác,
            // rộng cố định để đọc bằng mắt và grep đều dễ.
            var sb = new StringBuilder(160);
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append("  ").Append(kind.PadRight(5));
            sb.Append("  ").Append((ip ?? "?").PadRight(16));
            sb.Append("  block ").Append(blockNo.ToString().PadRight(4));
            sb.Append("  ").Append(detail);
            if (!string.IsNullOrEmpty(status)) sb.Append("  ").Append(status);
            if (!string.IsNullOrEmpty(note))   sb.Append("  | ").Append(note);

            lock (Sync)
            {
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }

                    File.AppendAllText(path, sb.ToString() + Environment.NewLine, Encoding.UTF8);
                }
                catch
                {
                    // Nhật ký hỏng KHÔNG được làm hỏng việc điều khiển. Nuốt lỗi
                    // là đúng ở đây — mất một dòng log còn hơn dừng vòng poll.
                }
            }
        }
    }
}
