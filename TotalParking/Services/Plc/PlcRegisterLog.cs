using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Hosting;

namespace TotalParking.Services.Plc
{
    // Nhat ky THAY DOI gia tri thanh ghi, mot file rieng.
    //
    // ===================== VI SAO TACH KHOI plc_audit.log =====================
    // plc_audit.log tra loi "he thong da lam gi": ghi xuong PLC, mat ket noi, quet
    // o. No la nhat ky SU KIEN. Cau hoi khac hoan toan la "thanh ghi nay da doi
    // gia tri nhu the nao theo thoi gian" — tron hai thu vao mot file thi ca hai
    // deu kho doc, va loc file 8 MB moi lan can nhin mot thanh ghi la lang phi.
    //
    // ===================== CHI GHI KHI DOI =====================
    // Vong poll chay 55 PLC x ~2 lan/giay. Ghi moi lan doc la hon 100 dong/giay,
    // 360 nghin dong moi gio, va thu can tim se chim trong do. Nen giu gia tri lan
    // truoc trong bo nho va chi ghi khi khac.
    //
    // ===================== DONG DAU TIEN SAU KHI KHOI DONG =====================
    // Bang gia tri nam trong bo nho nen mat khi app khoi dong lai. Lan quan sat
    // dau tien cua moi thanh ghi duoc ghi kem dau [dau tien] thay vi bo qua.
    //
    // Co chu y: no cho mot anh chup TOAN BO thanh ghi tai thoi diem khoi dong —
    // dung thu can de tra loi "luc 3 gio sang thanh ghi nay dang la bao nhieu".
    // Khoang 770 dong moi lan khoi dong, va app chi recycle mot lan moi ngay.
    public static class PlcRegisterLog
    {
        private static readonly object Sync = new object();

        // Khoa theo IP chu khong theo block_no. Anh xa block -> IP da doi may lan
        // va van chua duoc thiet bi xac nhan; IP moi la danh tinh that cua thiet bi
        // ma goi tin duoc gui toi. Cung ly do voi PlcAuditLog.
        private static readonly Dictionary<string, string> Last =
            new Dictionary<string, string>();

        private static string _path;
        private static bool   _resolved;

        // Xoay file khi qua co, giu dung mot file cu (.1) — giong plc_audit.log.
        private const long MaxBytes = 8L * 1024 * 1024;

        public static bool Enabled
        {
            get
            {
                bool b;
                string v = ConfigurationManager.AppSettings["plc:registerLogEnabled"];
                return !bool.TryParse(v, out b) || b;   // mac dinh BAT
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
                    string cfg = ConfigurationManager.AppSettings["plc:registerLogPath"];
                    if (string.IsNullOrWhiteSpace(cfg))
                        cfg = "~/App_Data/plc_register_changes.log";
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

        // Ghi nhan gia tri hien tai cua mot thanh ghi. Chi sinh ra mot dong khi
        // gia tri KHAC lan goi truoc cho cung IP + thanh ghi do.
        //
        // `value` la chuoi da dinh dang san (hex tho, hoac so) de nhat ky giu
        // nguyen van thu doc duoc, khong dien giai lai.
        // `note` la dien giai tuy chon, vi du ma the da giai ra.
        public static void Track(string ip, int blockNo, string register,
                                 string value, string note = null)
        {
            if (!Enabled) return;
            if (string.IsNullOrEmpty(register)) return;

            value = value ?? "(rong)";
            string key = (ip ?? "?") + "|" + register;

            string previous;
            bool first;

            lock (Sync)
            {
                first = !Last.TryGetValue(key, out previous);
                if (!first && previous == value) return;   // khong doi -> im lang
                Last[key] = value;
            }

            Append(ip, blockNo, register,
                   first ? "[dau tien]" : previous,
                   value, note);
        }

        private static void Append(string ip, int blockNo, string register,
                                   string from, string to, string note)
        {
            string path = Path;
            if (string.IsNullOrEmpty(path)) return;

            var sb = new StringBuilder(160);
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append("  ").Append((ip ?? "?").PadRight(16));
            sb.Append("  block ").Append(blockNo.ToString().PadRight(4));
            sb.Append("  ").Append(register.PadRight(8));
            sb.Append("  ").Append(from.PadRight(11)).Append(" -> ").Append(to);
            if (!string.IsNullOrEmpty(note)) sb.Append("  | ").Append(note);

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
                    // Nhat ky hong KHONG duoc lam hong viec dieu khien. Mat mot dong
                    // log con hon dung vong poll. Giong het plc_audit.log.
                }
            }
        }
    }
}
