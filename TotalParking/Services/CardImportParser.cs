using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TotalParking.Services
{
    // Doc sheet the xe xuat tu he thong DEC cua toa nha va LOC truoc khi cho ghi.
    //
    // ======================= VI SAO DOC DUNG FILE GOC =======================
    // Truoc day trang nhap the doi mot file CSV 8 cot theo mau rieng. Nghia la
    // moi lan khach gui danh sach, co nguoi phai mo Excel, xoa 14 cot, doi ten
    // 8 cot con lai va luu sang CSV -- mot buoc lam bang tay, khong ai kiem tra,
    // va la cho de lam lech du lieu nhat trong ca quy trinh.
    //
    // Nay doc thang file khach xuat ra. Khong con buoc chuan bi nao de lam sai.
    //
    // ======================= VI SAO TACH RIENG =======================
    // Cung mot bo luat phai chay o hai cho: luc XEM TRUOC va luc GHI THAT. Neu
    // viet hai lan thi ban xem truoc va ban ghi se lech nhau ngay lan sua dau,
    // va nguoi dung se thay "111 dong hop le" roi nhan ve 105 dong trong DB.
    //
    // Nhan vao Sheet (bang chuoi) chu khong nhan file: nho vay bo luat doc duoc
    // ma khong can mo ZIP, va XlsxReader chi lam mot viec la doc byte.
    public class CardImportParser
    {
        // Chi nhap the thang o to. Cot 'Ma nhom dinh danh' cua file khach con co
        // 'THE THANG XE MAY' va nhieu dong de trong -- bai co khi khong nhan xe
        // may, nen nhung dong do khong phai loi, chi la khong lien quan.
        public const string TargetGroup = "THE THANG OTO";

        // Ca lo deu la the thang, nen loai khach luon la XT. Day la KET LUAN tu
        // chinh dieu kien loc o tren, khong phai mot gia tri mac dinh doan bua:
        // mot dong vao duoc day thi chac chan thuoc nhom 'THE THANG OTO'.
        public const string CustomerTypeXt = "XT";

        // Hang tai dung khi cot J khong chi ra tai trong nao. Khong chon THUONG
        // vi y nghia cua THUONG la "qua tai, khong dung duoc pallet co khi" --
        // dat mac dinh do se loai han nhung xe nay khoi bai co khi. Giu nguyen
        // van chu cua khach o WeightText de con loc ra ma sua.
        public const string DefaultWeightClass = "2200KG";

        // Dong phai doan hang tai duoc dan nhan lo RIENG, theo dung cach file 28
        // da lam. Nho vay mot cau loc bang source_label la ra het nhung the can
        // nguoi xac nhan lai, khong phai doi chieu lai voi file Excel.
        public const string GuessedWeightSuffix = " (CHUA RO HANG TAI)";

        // Gia tri nho hon nguong nay khong phai ma the ma la thanh ghi noi bo.
        // Cung nguong dang dung o v_slot_taken va CarLocatorService -- ba noi phai
        // khop nhau, neu khong thi the nap duoc nhung tim xe lai khong thay.
        private const long MinCardValue = 65535;

        // Gioi han do dai theo dung schema parking_card. Cot khoa (ma the, so
        // the) thi BAO LOI khi vuot, con cot ho so thi CAT BOT: mat vai ky tu
        // cuoi cua ten xe van hon la mat ca cai the, va neu de nguyen thi MySQL
        // bao loi truncate va ca lo 111 dong khong dong nao vao duoc.
        private const int MaxCardNo       = 16;
        private const int MaxCardType     = 16;
        private const int MaxVehicleName  = 64;
        private const int MaxWeightText   = 32;
        private const int MaxPlate        = 16;
        private const int MaxCustomerName = 128;
        private const int MaxSourceLabel  = 64;

        public class Row
        {
            public int    LineNo       { get; set; }   // so dong trong Excel, de doi chieu tay
            public string CardCode     { get; set; }   // Ma dinh danh
            public string CardNo        { get; set; }  // Ten dinh danh
            public string CardType      { get; set; }  // Loai
            public string VehicleName   { get; set; }  // Ten phuong tien
            public string WeightClass   { get; set; }  // ma tra cuu: THUONG / 2200KG / 2600KG
            public string WeightText    { get; set; }  // nguyen van cot Phan loai tai trong xe
            public string Plate         { get; set; }  // Bien so hien tai
            public string CustomerName  { get; set; }  // Ten khach hang
            public DateTime? ExpiryDate { get; set; }  // Ngay het han, null = khong doc duoc
            public string CustomerType  { get; set; }
            public string SourceLabel   { get; set; }
            public bool   IsActive      { get; set; }

            // true = cot J khong chi ra hang tai, WeightClass la gia tri mac dinh.
            public bool   WeightGuessed { get; set; }

            // Null = hop le. Khac null = ly do bi bo, hien thi nguyen van cho nguoi dung.
            public string Reject { get; set; }
            public bool   Ok { get { return Reject == null; } }
        }

        public class Result
        {
            public IList<Row> Rows = new List<Row>();
            public string     FatalError;       // hong toi muc khong doc duoc dong nao
            public string     SheetName;

            // Tong so dong du lieu trong sheet, va so dong bi loai vi khong thuoc
            // nhom the thang o to. Dong khac nhom KHONG vao Rows: bao cao 850 dong
            // "bi bo qua" se chon mat 16 dong that su can nguoi xem.
            public int OtherGroupRows;
            public int TotalDataRows;

            public IList<Row> Accepted { get { return Rows.Where(r => r.Ok).ToList(); } }
            public IList<Row> Rejected { get { return Rows.Where(r => !r.Ok).ToList(); } }
        }

        // defaultLabel: nhan lo nhap, thuong la "ten file + ngay".
        public Result Parse(XlsxReader.Sheet sheet, string defaultLabel)
        {
            var res = new Result();
            if (sheet == null || sheet.Rows == null || sheet.Rows.Count == 0)
            {
                res.FatalError = "Sheet rong, khong co dong nao.";
                return res;
            }
            res.SheetName = sheet.Name;

            // Dong tieu de la dong CO DU LIEU dau tien. Mot so ban xuat chen may
            // dong trong hoac mot dong tieu de o tren bang, nen khong the mac
            // dinh la dong 1.
            int headerIdx = -1;
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                if (sheet.Rows[i].Any(c => !string.IsNullOrWhiteSpace(c))) { headerIdx = i; break; }
            }
            if (headerIdx < 0)
            {
                res.FatalError = "Sheet khong co dong nao co du lieu.";
                return res;
            }

            var header = sheet.Rows[headerIdx].Select(Norm).ToList();

            int iCode  = header.IndexOf("ma dinh danh");
            int iNo    = header.IndexOf("ten dinh danh");
            int iType  = header.IndexOf("loai");
            int iGroup = header.IndexOf("ma nhom dinh danh");
            int iVeh   = header.IndexOf("ten phuong tien");
            int iWc    = header.IndexOf("phan loai tai trong xe");
            int iPlate = header.IndexOf("bien so hien tai");
            int iExp   = header.IndexOf("ngay het han");
            int iCust  = header.IndexOf("ten khach hang");

            // So ten cot da chuan hoa (bo dau, chu thuong) chu khong so nguyen
            // van: 'Mã định danh' va 'MÃ ĐỊNH DANH' phai duoc coi la mot. Nhung
            // van so BANG NHAU chu khong phai chua nhau, vi file co ca cot
            // 'Ma dinh danh DEC' -- dung Contains la lay nham cot do.
            var missing = new List<string>();
            if (iCode  < 0) missing.Add("Ma dinh danh");
            if (iNo    < 0) missing.Add("Ten dinh danh");
            if (iGroup < 0) missing.Add("Ma nhom dinh danh");
            if (missing.Count > 0)
            {
                res.FatalError =
                    "Thieu cot bat buoc: " + string.Join(", ", missing) + ". " +
                    "Doc duoc dong tieu de: " + string.Join(" | ", sheet.Rows[headerIdx]);
                return res;
            }

            // Bat trung NGAY TRONG FILE, khong chi trung voi DB. Mot file co hai
            // dong cung ma thi dong sau se ghi de dong truoc ma khong ai biet.
            var seenCode = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNo   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Cat de con cho cho hau to, de ca hai bien the nhan deu vua 64 ky tu.
            string label = Cap(string.IsNullOrWhiteSpace(defaultLabel) ? "IMPORT" : defaultLabel.Trim(),
                               MaxSourceLabel - GuessedWeightSuffix.Length);

            for (int i = headerIdx + 1; i < sheet.Rows.Count; i++)
            {
                var f = sheet.Rows[i];
                if (!f.Any(c => !string.IsNullOrWhiteSpace(c))) continue;   // dong trong
                res.TotalDataRows++;

                Func<int, string> get = idx =>
                    idx >= 0 && idx < f.Count ? (f[idx] ?? "").Trim() : "";

                if (Norm(get(iGroup)) != Norm(TargetGroup)) { res.OtherGroupRows++; continue; }

                string weightText = get(iWc);
                bool guessed;
                var row = new Row
                {
                    LineNo       = i + 1,                       // Excel dem tu 1
                    CardCode     = get(iCode).ToLowerInvariant(),
                    CardNo       = get(iNo),
                    CardType     = Cap(get(iType),   MaxCardType),
                    VehicleName  = Cap(get(iVeh),    MaxVehicleName),
                    WeightText   = Cap(weightText,   MaxWeightText),
                    Plate        = Cap(get(iPlate),  MaxPlate),
                    CustomerName = Cap(get(iCust),   MaxCustomerName),
                    ExpiryDate   = ParseExpiry(get(iExp)),
                    WeightClass  = MapWeightClass(weightText, out guessed),
                    CustomerType = CustomerTypeXt,
                    SourceLabel  = guessed ? label + GuessedWeightSuffix : label,
                    IsActive     = true
                };
                row.WeightGuessed = guessed;

                row.Reject = Validate(row, seenCode, seenNo);
                if (row.Ok)
                {
                    seenCode.Add(row.CardCode);
                    seenNo.Add(row.CardNo);
                }
                res.Rows.Add(row);
            }

            if (res.TotalDataRows == 0)
                res.FatalError = "Khong co dong du lieu nao sau dong tieu de.";
            else if (res.Rows.Count == 0)
                res.FatalError =
                    "Doc duoc " + res.TotalDataRows + " dong nhung khong dong nao co " +
                    "'Ma nhom dinh danh' = '" + TargetGroup + "'.";

            return res;
        }

        private static string Validate(Row r, HashSet<string> seenCode, HashSet<string> seenNo)
        {
            // 1. co ma the
            if (r.CardCode.Length == 0) return "thieu ma dinh danh";

            // 2. dung 8 ky tu hex
            if (r.CardCode.Length != 8 || !r.CardCode.All(Uri.IsHexDigit))
                return "ma dinh danh phai la 8 ky tu hex";

            // 3. KHAC bien so cua chinh dong do
            //
            // Luat de bat nhat va quan trong nhat. File khach co nhung dong bi
            // dan bien so vao o ma dinh danh, trong do co bien so tinh co toan
            // ky tu hex nen luat 2 khong bat duoc:
            //     ma = 30A83260   bien so = 30A83260   <- la bien so
            //     ma = A0FF0790   bien so = 30M03135   <- ma the that
            if (r.Plate.Length > 0 &&
                string.Equals(r.CardCode, r.Plate.Replace("-", "").Replace(" ", ""),
                              StringComparison.OrdinalIgnoreCase))
                return "ma dinh danh trung bien so - nhieu kha nang dan nham";

            // 4. gia tri du lon
            long val;
            if (!long.TryParse(r.CardCode, NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out val))
                return "khong doc duoc ma dinh danh";
            if (val <= MinCardValue) return "gia tri qua nho, khong phai ma the";

            // 5. ten dinh danh (so the in tren mat the)
            if (r.CardNo.Length == 0)           return "thieu ten dinh danh";
            if (r.CardNo.Length > MaxCardNo)    return "ten dinh danh dai qua " + MaxCardNo + " ky tu";

            // 6. trung trong chinh file nay
            if (seenCode.Contains(r.CardCode)) return "ma dinh danh trung voi dong truoc trong file";
            if (seenNo.Contains(r.CardNo))     return "ten dinh danh trung voi dong truoc trong file";

            return null;
        }

        // ------------------------------------------------------------ hang tai
        //
        // Cot J cua file khach co 5 gia tri. Doi chieu voi bang weight_class:
        // THUONG co max_weight_kg NULL, nghia la khong dung duoc pallet co khi.
        //
        // So bang bang tren danh sach dong chu khong doc con so trong chu. Neu
        // sau nay khach them muc moi ('Duoi 2500 kg' chang han) thi dong do roi
        // vao nhanh mac dinh va duoc danh dau -- thay ngay, con doc so thi no se
        // bi xep im lang vao mot hang tai co the sai.
        private static string MapWeightClass(string text, out bool guessed)
        {
            guessed = false;
            switch (Norm(text))
            {
                case "duoi 2200 kg":      return "2200KG";
                case "2200 - 2600 kg":    return "2600KG";
                case "qua tai (>2600 kg)": return "THUONG";
                default:
                    // 'Chua xac dinh', 'Khong phai o to', o trong, hoac muc moi.
                    guessed = true;
                    return DefaultWeightClass;
            }
        }

        // ---------------------------------------------------------- ngay het han
        //
        // File khach ghi ngay duoi dang chu 'dd/MM/yyyy'. Nhung neu nguoi lam
        // file bam dinh dang Date thi Excel luu thanh SO -- so ngay ke tu
        // 30/12/1899. Doc ca hai kieu, vi khong doan duoc file sau se the nao.
        //
        // Khong doc duoc thi tra null chu khong bo ca dong: han the la thong tin
        // tham khao, mat no khong lam the sai, con bo ca the thi xe khong vao duoc.
        private static DateTime? ParseExpiry(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();

            var formats = new[]
            {
                "dd/MM/yyyy", "d/M/yyyy",
                "dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm:ss",
                "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss"
            };
            DateTime d;
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out d))
                return d.Date;

            // So ngay Excel. Moc 30/12/1899 (khong phai 31/12) de bu cho loi
            // Excel coi 1900 la nam nhuan. Chan duoi 60 de mot o dien nham
            // '1' hay '45' khong thanh ngay nam 1900.
            double serial;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out serial) &&
                serial >= 60 && serial < 2958466)     // 2958465 = 31/12/9999
                return new DateTime(1899, 12, 30).AddDays(Math.Floor(serial)).Date;

            return null;
        }

        private static string Cap(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        // Bo dau, chuyen chu thuong, gop khoang trang -- de so ten cot va gia
        // tri phan loai ma khong phu thuoc vao cach khach danh may.
        //
        // Phai xu ly rieng chu 'd' gach ngang: FormD tach duoc dau cua 'ị' thanh
        // i + dau, nhung 'đ' la MOT ky tu doc lap, khong tach ra 'd' + dau nao.
        // Bo qua no thi 'Ma dinh danh' khong bao gio khop 'Mã định danh'.
        internal static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            var lowered = s.Trim().ToLowerInvariant().Replace('đ', 'd');   // U+0111 = d gach ngang

            var sb = new StringBuilder(lowered.Length);
            bool lastWasSpace = false;
            foreach (char c in lowered.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsWhiteSpace(c))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastWasSpace = false;
                }
            }
            return sb.ToString().Trim();
        }
    }
}
