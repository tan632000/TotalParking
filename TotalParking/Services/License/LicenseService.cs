using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace TotalParking.Services.License
{
    // Xac minh giay phep va tinh trang thai cho gate web MVC.
    //
    // Hop dong ky PHAI khop tools/license/issue-license.ps1:
    //   - RSA-2048, VerifyData(SHA256, Pkcs1).
    //   - Payload canonical (byte duoc ky) = chuoi JSON UTF-8, dung thu tu khoa,
    //     khong khoang trang, khong xuong dong:
    //       {"customerId":"..","issuedUtc":"..","expiryUtc":".."}
    //     ngay ISO-8601 'yyyy-MM-ddTHH:mm:ssZ' (UTC, InvariantCulture).
    //   - File .lic = 2 dong: dong 1 payload, dong 2 Base64 chu ky.
    //
    // Luu key: public key dang Base64(XML) o Web.config license:publicKey.
    // ToXmlString chi co o RSACryptoServiceProvider; SHA-256 an toan o RSACng
    // tren .NET 4.7.2. Nen: FromXmlString qua RSACryptoServiceProvider ->
    // ExportParameters -> import sang RSACng de VerifyData. Giong het ben PS.
    public class LicenseService
    {
        // Buoc nhay thoi gian toi da coi la THAT khi nang moc chong-lui-dong-ho.
        // Nhay xa hon (vd pin CMOS chet -> 2030) bi coi la loi dong ho va BO QUA,
        // nen loi dong ho that khong khoa cung console hop le (finding C2-01).
        private const int MaxForwardJumpDays = 90;
        private const string IsoUtc = "yyyy-MM-ddTHH:mm:ssZ";

        private readonly string _publicKeyB64;
        private readonly string _dataDir;
        private readonly string _licensePath;
        private readonly string _highWaterPath;
        private readonly object _ioLock = new object();

        // Dung trong app: doc Web.config + App_Data.
        public LicenseService()
            : this(ConfigurationManager.AppSettings["license:publicKey"],
                   ResolveDataDir(ConfigurationManager.AppSettings["license:dataDir"]))
        {
        }

        // Dung cho probe/test: truyen thang public key + thu muc du lieu.
        public LicenseService(string publicKeyB64, string dataDir)
        {
            _publicKeyB64 = publicKeyB64;
            _dataDir = string.IsNullOrEmpty(dataDir) ? "." : dataDir;
            _licensePath = Path.Combine(_dataDir, "license.lic");
            _highWaterPath = Path.Combine(_dataDir, "clock-highwater.dat");
        }

        private static string ResolveDataDir(string configured)
        {
            if (string.IsNullOrWhiteSpace(configured)) configured = "~/App_Data/license";
            if (configured.StartsWith("~"))
            {
                var mapped = HostingEnvironment.MapPath(configured);
                if (!string.IsNullOrEmpty(mapped)) return mapped;
            }
            return configured;
        }

        // Diem vao chinh cho gate. KHONG BAO GIO nem: moi loi -> Locked (fail-closed).
        public LicenseStatus GetStatus()
        {
            try
            {
                var lic = LoadVerifiedLicense();
                if (lic == null)
                    return new LicenseStatus { State = LicenseState.Locked, Reason = "Khong co giay phep hop le" };

                var effectiveNow = ComputeEffectiveNow();
                var valid = effectiveNow <= lic.ExpiryUtc;
                return new LicenseStatus
                {
                    State = valid ? LicenseState.Valid : LicenseState.Locked,
                    CustomerId = lic.CustomerId,
                    ExpiryUtc = lic.ExpiryUtc,
                    Reason = valid ? null : "Giay phep het han"
                };
            }
            catch
            {
                // Hong/thieu/loi doc deu -> Locked, khong lam sap ca site.
                return new LicenseStatus { State = LicenseState.Locked, Reason = "Loi doc giay phep" };
            }
        }

        public bool IsLocked()
        {
            return GetStatus().State == LicenseState.Locked;
        }

        // Kich hoat tu noi dung license dan vao man Activation.
        // Thanh cong -> ghi license + reset moc dong ho -> mo khoa.
        // That bai -> tra false + ly do, KHONG ghi gi.
        public bool Activate(string licenseText, out string error)
        {
            error = null;
            try
            {
                var lic = ParseAndVerify(licenseText);
                if (lic == null) { error = "Chu ky khong hop le hoac file sai dinh dang."; return false; }
                if (lic.ExpiryUtc <= DateTime.UtcNow) { error = "Giay phep da het han."; return false; }

                var canonical = Normalize(licenseText);
                lock (_ioLock)
                {
                    Directory.CreateDirectory(_dataDir);
                    AtomicWriteAllText(_licensePath, canonical);
                    // Giay phep moi hop le thay the baseline: reset moc dong ho ve
                    // hien tai de moc cu (neu bi ket cao) khong khoa nham.
                    AtomicWriteAllText(_highWaterPath, DateTime.UtcNow.ToString(IsoUtc, CultureInfo.InvariantCulture));
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Loi khi luu giay phep: " + ex.Message;
                return false;
            }
        }

        // ------------------------------------------------------------------ noi bo

        private ParsedLicense LoadVerifiedLicense()
        {
            if (!File.Exists(_licensePath)) return null;
            string text;
            lock (_ioLock) { text = File.ReadAllText(_licensePath, new UTF8Encoding(false)); }
            return ParseAndVerify(text);
        }

        private ParsedLicense ParseAndVerify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var lines = Normalize(text).Split('\n');
            if (lines.Length < 2) return null;

            var payload = lines[0];
            var sigB64 = lines[1].Trim();
            if (!VerifySignature(payload, sigB64)) return null;

            var expiry = Match(payload, "\"expiryUtc\":\"([^\"]*)\"");
            if (expiry == null) return null;
            var issued = Match(payload, "\"issuedUtc\":\"([^\"]*)\"");

            return new ParsedLicense
            {
                CustomerId = Match(payload, "\"customerId\":\"([^\"]*)\""),
                IssuedUtc = issued != null ? ParseUtc(issued) : DateTime.MinValue,
                ExpiryUtc = ParseUtc(expiry)
            };
        }

        private bool VerifySignature(string payload, string sigB64)
        {
            if (string.IsNullOrWhiteSpace(_publicKeyB64) || _publicKeyB64 == "PASTE_BASE64_PUBLIC_KEY")
                return false;

            byte[] sig;
            try { sig = Convert.FromBase64String(sigB64); } catch { return false; }

            var pubXml = Encoding.UTF8.GetString(Convert.FromBase64String(_publicKeyB64));
            RSAParameters pars;
            using (var csp = new RSACryptoServiceProvider())
            {
                csp.FromXmlString(pubXml);
                pars = csp.ExportParameters(false);
            }
            using (var rsa = new RSACng())
            {
                rsa.ImportParameters(pars);
                var data = Encoding.UTF8.GetBytes(payload);
                return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        // Thoi diem dung de so voi han: chong CA lui dong ho LAN nhay vot vo ly.
        private DateTime ComputeEffectiveNow()
        {
            var now = DateTime.UtcNow;
            var hw = ReadHighWater();

            if (hw == null)
            {
                WriteHighWater(now);
                return now;
            }
            if (now > hw.Value && (now - hw.Value).TotalDays <= MaxForwardJumpDays)
            {
                // Thoi gian troi binh thuong: nang moc.
                WriteHighWater(now);
                return now;
            }
            // Lui dong ho (now < hw) HOAC nhay vot vo ly (> MaxForwardJumpDays):
            // giu moc cu, khong ghi. Lui thi khong ne duoc han; nhay vot thi khong
            // bi ghi lai nen sau khi sua dong ho khong khoa nham.
            return hw.Value;
        }

        private DateTime? ReadHighWater()
        {
            try
            {
                if (!File.Exists(_highWaterPath)) return null;
                string s;
                lock (_ioLock) { s = File.ReadAllText(_highWaterPath).Trim(); }
                return ParseUtc(s);
            }
            catch
            {
                // Moc hong -> coi nhu chua co, se duoc dat lai o lan danh gia nay.
                return null;
            }
        }

        private void WriteHighWater(DateTime utc)
        {
            try
            {
                lock (_ioLock)
                {
                    Directory.CreateDirectory(_dataDir);
                    AtomicWriteAllText(_highWaterPath, utc.ToString(IsoUtc, CultureInfo.InvariantCulture));
                }
            }
            catch
            {
                // Ghi moc that bai khong duoc lam sap gate.
            }
        }

        private static void AtomicWriteAllText(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            var tmp = Path.Combine(dir, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path))
                File.Replace(tmp, path, null);   // nguyen tu tren NTFS
            else
                File.Move(tmp, path);
        }

        private static string Normalize(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n');
        }

        private static string Match(string input, string pattern)
        {
            var m = Regex.Match(input, pattern);
            return m.Success ? m.Groups[1].Value : null;
        }

        private static DateTime ParseUtc(string s)
        {
            return DateTime.ParseExact(s, IsoUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}
