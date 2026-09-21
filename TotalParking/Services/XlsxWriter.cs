using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TotalParking.Services
{
    // Sinh file .xlsx mot sheet, chi co chu, khong dinh dang.
    //
    // ======================= VI SAO KHONG DUNG CSV NUA =======================
    // File mau truoc day la CSV. Khach lam viec bang Excel, va Excel mo CSV
    // tieng Viet rat de ra chu hong neu khong dung BOM -- da tung phai them BOM
    // bang tay chinh vi vay. Mot khi bo doc da hieu .xlsx thi file mau cung nen
    // la .xlsx: cung mot dinh dang o ca hai dau, khong con cho nao de lech.
    //
    // ======================= VI SAO DU MOT FILE TOI GIAN =======================
    // Mot .xlsx hop le can it hon nhieu so voi cai Excel thuong xuat ra: bon
    // file XML mo ta cau truc va mot file sheet. Chua can sharedStrings.xml lan
    // styles.xml vi chu duoc viet thang vao o (t="inlineStr") va khong o nao
    // duoc to mau. Excel, LibreOffice va XlsxReader trong project doc duoc het.
    public static class XlsxWriter
    {
        // Tao mot workbook mot sheet tu cac dong chu. Tra ve byte de controller
        // truyen thang vao File(...) ma khong phai ghi ra dia.
        public static byte[] Build(string sheetName, IEnumerable<IEnumerable<string>> rows)
        {
            if (string.IsNullOrWhiteSpace(sheetName)) sheetName = "Sheet1";

            using (var ms = new MemoryStream())
            {
                // Dong ZipArchive TRUOC khi doc ms.ToArray(): thu muc trung tam
                // cua file ZIP chi duoc ghi luc Dispose, doc som thi ra mot file
                // ZIP cat doi ma khong bao loi gi.
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    Put(zip, "[Content_Types].xml", ContentTypes);
                    Put(zip, "_rels/.rels", RootRels);
                    Put(zip, "xl/workbook.xml", Workbook(sheetName));
                    Put(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
                    Put(zip, "xl/worksheets/sheet1.xml", Sheet(rows));
                }
                return ms.ToArray();
            }
        }

        private static void Put(ZipArchive zip, string path, string xml)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var s = entry.Open())
            using (var w = new StreamWriter(s, new UTF8Encoding(false)))
                w.Write(xml);
        }

        private const string Head = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";
        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsPkgRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string NsDocRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private const string ContentTypes = Head +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "</Types>";

        private const string RootRels = Head +
            "<Relationships xmlns=\"" + NsPkgRel + "\">" +
            "<Relationship Id=\"rId1\" Type=\"" + NsDocRel + "/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        private const string WorkbookRels = Head +
            "<Relationships xmlns=\"" + NsPkgRel + "\">" +
            "<Relationship Id=\"rId1\" Type=\"" + NsDocRel + "/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "</Relationships>";

        private static string Workbook(string sheetName)
        {
            // Ten sheet cua Excel: toi da 31 ky tu, khong duoc chua : \ / ? * [ ]
            var clean = new StringBuilder();
            foreach (char c in sheetName)
                if (":\\/?*[]".IndexOf(c) < 0) clean.Append(c);
            var name = clean.Length == 0 ? "Sheet1" : clean.ToString();
            if (name.Length > 31) name = name.Substring(0, 31);

            return Head +
                "<workbook xmlns=\"" + NsMain + "\" xmlns:r=\"" + NsDocRel + "\">" +
                "<sheets><sheet name=\"" + Esc(name) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                "</workbook>";
        }

        private static string Sheet(IEnumerable<IEnumerable<string>> rows)
        {
            var sb = new StringBuilder();
            sb.Append(Head)
              .Append("<worksheet xmlns=\"").Append(NsMain).Append("\"><sheetData>");

            int rowNo = 0;
            foreach (var row in rows ?? new List<IEnumerable<string>>())
            {
                rowNo++;
                sb.Append("<row r=\"").Append(rowNo).Append("\">");
                int col = 0;
                foreach (var cell in row ?? new List<string>())
                {
                    string refName = ColumnName(col) + rowNo;
                    col++;
                    if (string.IsNullOrEmpty(cell)) continue;   // o trong thi bo han the

                    // t="inlineStr" = chu nam ngay trong o. Moi gia tri deu ghi
                    // la chu, ke ca chuoi toan chu so: mot ma the nhu 61d41330
                    // hay bien so 30A83260 ma de Excel tu doan kieu thi no doi
                    // thanh so va mat chu so 0 o dau.
                    sb.Append("<c r=\"").Append(refName).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                      .Append(Esc(cell))
                      .Append("</t></is></c>");
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        // 0 -> "A", 25 -> "Z", 26 -> "AA". Nguoc voi XlsxReader.ColumnIndex.
        private static string ColumnName(int index)
        {
            var sb = new StringBuilder();
            index++;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                index = (index - 1) / 26;
            }
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            return (s ?? "")
                .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
