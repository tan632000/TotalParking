using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace TotalParking.Services
{
    // Doc file .xlsx thanh bang o dang chuoi, khong dung thu vien ngoai.
    //
    // ======================= VI SAO TU DOC =======================
    // Project khong co thu vien Excel nao (xem packages.config), va mang OT cua
    // toa nha khong ra Internet nen them NuGet la viec phai lam bang tay tren
    // tung may. Nhung mot file .xlsx chi la mot file ZIP chua XML, ma .NET
    // Framework 4.7.2 da co san ca hai: ZipArchive va XDocument. Doc truc tiep
    // re hon nhieu so voi keo mot thu vien vao chi de lay 9 cot chu.
    //
    // Doc XLSX chu KHONG doc XLS: .xls la dinh dang nhi phan cu, hoan toan khac,
    // khong the doc bang ZipArchive. File khach gui la .xlsx nen du.
    //
    // ======================= TRA VE CHUOI, KHONG DOI KIEU =======================
    // Moi o tra ve dung van ban tho nam trong XML. Lop nay khong tu doi so thanh
    // ngay hay thanh so thap phan, vi de biet mot con so la ngay hay la so thi
    // phai doc them bang dinh dang (styles.xml) -- va doan sai thi '31/12/2028'
    // bien thanh '47119' ma khong ai thay. Viec hieu y nghia cot la cua lop goi;
    // o day chi bao dam khong lam sai lech byte nao.
    public static class XlsxReader
    {
        // Khong gian ten cua SpreadsheetML va cua quan he trong goi OPC.
        private static readonly XNamespace Main =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace PkgRel =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace DocRel =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // Chan file co so dong bat thuong. File khach hien tai 994 dong; 100k
        // dong la du rong tay ma van khong de mot file dung nuot het bo nho.
        private const int MaxRows = 100000;

        public class Sheet
        {
            public string Name { get; set; }

            // Moi dong la mot danh sach o theo thu tu cot A, B, C...
            // O trong hoac o bi luoc trong XML deu tra ve chuoi rong.
            public IList<IList<string>> Rows { get; set; }
        }

        public class XlsxFormatException : Exception
        {
            public XlsxFormatException(string message) : base(message) { }
        }

        // Doc sheet DAU TIEN theo thu tu tab hien trong Excel.
        public static Sheet ReadFirstSheet(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            // ZipArchive can stream doc duoc ngau nhien (Seek). HttpPostedFile
            // cho seek, nhung khong phai moi nguon deu vay, nen sao ra bo nho
            // mot lan cho chac. Co gioi han 2 MB o tang controller.
            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                buffer.Position = 0;

                ZipArchive zip;
                try
                {
                    zip = new ZipArchive(buffer, ZipArchiveMode.Read);
                }
                catch (InvalidDataException)
                {
                    // Gap truong hop nay khi nguoi dung doi ten file .csv hoac
                    // .xls thanh .xlsx: phan mo rong dung ma ruot khong phai ZIP.
                    throw new XlsxFormatException(
                        "File khong phai dinh dang .xlsx. Neu day la file .xls hoac .csv, " +
                        "hay mo bang Excel va luu lai voi dinh dang Excel Workbook (*.xlsx).");
                }

                using (zip)
                {
                    string sheetPath, sheetName;
                    FindFirstSheet(zip, out sheetPath, out sheetName);

                    var shared = ReadSharedStrings(zip);
                    return new Sheet
                    {
                        Name = sheetName,
                        Rows = ReadSheetRows(zip, sheetPath, shared)
                    };
                }
            }
        }

        // ----------------------------------------------------------- tim sheet
        //
        // Khong lay thang 'xl/worksheets/sheet1.xml'. Ten file khong quyet dinh
        // thu tu tab: mot workbook da tung xoa roi them sheet co the co tab dau
        // tien tro toi sheet3.xml. Di theo dung duong Excel di -- workbook ->
        // r:id -> rels -> duong dan that -- moi chac doc dung sheet nguoi dung
        // nhin thay dau tien.
        private static void FindFirstSheet(ZipArchive zip, out string path, out string name)
        {
            var workbook = LoadXml(zip, "xl/workbook.xml");
            if (workbook == null)
                throw new XlsxFormatException("File .xlsx thieu xl/workbook.xml, khong doc duoc.");

            var sheet = workbook.Root
                .Elements(Main + "sheets")
                .Elements(Main + "sheet")
                .FirstOrDefault();
            if (sheet == null)
                throw new XlsxFormatException("File .xlsx khong co sheet nao.");

            name = (string)sheet.Attribute("name") ?? "Sheet1";
            var relId = (string)sheet.Attribute(DocRel + "id");

            var rels = LoadXml(zip, "xl/_rels/workbook.xml.rels");
            string target = null;
            if (rels != null && relId != null)
            {
                target = rels.Root.Elements(PkgRel + "Relationship")
                    .Where(r => (string)r.Attribute("Id") == relId)
                    .Select(r => (string)r.Attribute("Target"))
                    .FirstOrDefault();
            }

            // Thieu rels thi doan theo ten quen thuoc, con hon bao loi ngay.
            if (string.IsNullOrEmpty(target)) target = "worksheets/sheet1.xml";

            // Target thuong la duong dan tuong doi so voi xl/, doi khi tuyet doi.
            path = target.StartsWith("/")
                ? target.TrimStart('/')
                : "xl/" + target.TrimStart('.', '/');
        }

        // -------------------------------------------------- bang chuoi dung chung
        //
        // Excel khong luu chu trong o. Chu duoc dua het vao sharedStrings.xml,
        // trong o chi con so thu tu. Khong doc bang nay thi moi o chu tra ve
        // mot con so vo nghia.
        private static IList<string> ReadSharedStrings(ZipArchive zip)
        {
            var doc = LoadXml(zip, "xl/sharedStrings.xml");
            if (doc == null) return new List<string>();   // file toan so thi khong co bang nay

            // Mot <si> co the bi cat thanh nhieu doan <r> khi chu trong o co
            // phan duoc to mau hoac in dam khac nhau. Noi het cac <t> lai, neu
            // khong thi "Nguyen Van A" chi lay duoc "Nguyen".
            return doc.Root.Elements(Main + "si")
                      .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value)))
                      .ToList();
        }

        // ------------------------------------------------------------ doc o
        private static IList<IList<string>> ReadSheetRows(
            ZipArchive zip, string path, IList<string> shared)
        {
            var doc = LoadXml(zip, path);
            if (doc == null)
                throw new XlsxFormatException("Khong tim thay du lieu sheet trong file (" + path + ").");

            var rows = new List<IList<string>>();

            foreach (var xr in doc.Root.Elements(Main + "sheetData").Elements(Main + "row"))
            {
                if (rows.Count >= MaxRows)
                    throw new XlsxFormatException(
                        "File qua " + MaxRows + " dong, khong nhap duoc mot lan.");

                // Excel LUOC HAN o trong: mot dong chi co A va D se sinh dung
                // hai the <c>. Phai dat tung o vao dung cot theo thuoc tinh r
                // ("D7"), neu khong thi moi dong thieu o se lech sang trai va
                // ten khach hang roi vao cot bien so.
                var cells = new List<string>();
                foreach (var c in xr.Elements(Main + "c"))
                {
                    int col = ColumnIndex((string)c.Attribute("r"));
                    if (col < 0) col = cells.Count;          // thieu r thi xep tiep
                    while (cells.Count <= col) cells.Add("");
                    cells[col] = CellText(c, shared);
                }
                rows.Add(cells);
            }

            return rows;
        }

        private static string CellText(XElement c, IList<string> shared)
        {
            string type = (string)c.Attribute("t") ?? "n";

            switch (type)
            {
                case "s":       // so thu tu trong sharedStrings
                    var v = c.Element(Main + "v");
                    int idx;
                    if (v == null ||
                        !int.TryParse(v.Value, NumberStyles.Integer,
                                      CultureInfo.InvariantCulture, out idx) ||
                        idx < 0 || idx >= shared.Count)
                        return "";
                    return shared[idx];

                case "inlineStr":   // chu nam ngay trong o, khong qua bang chung
                    return string.Concat(c.Descendants(Main + "t").Select(t => t.Value));

                case "b":           // true/false luu la 1/0
                    var b = c.Element(Main + "v");
                    return b == null ? "" : (b.Value == "1" ? "1" : "0");

                default:            // "n" so, "str" ket qua cong thuc, "e" loi
                    var raw = c.Element(Main + "v");
                    return raw == null ? "" : raw.Value;
            }
        }

        // "AB12" -> 27. Cot Excel la he 26 chu cai va khong co chu so 0:
        // A=1 ... Z=26, AA=27. Tru 1 o cuoi de thanh chi so dem tu 0.
        private static int ColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;

            int n = 0;
            foreach (char ch in cellRef)
            {
                if (ch >= 'A' && ch <= 'Z') n = n * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') n = n * 26 + (ch - 'a' + 1);
                else break;     // toi phan chu so la het ten cot
            }
            return n - 1;
        }

        private static XDocument LoadXml(ZipArchive zip, string path)
        {
            // Ten muc trong ZIP phan biet hoa thuong theo chuan, nhung mot vai
            // cong cu xuat file lai viet hoa khac di. So khong phan biet cho chac.
            var entry = zip.Entries.FirstOrDefault(
                e => string.Equals(e.FullName, path, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return null;

            using (var s = entry.Open())
                return XDocument.Load(s);
        }
    }
}
