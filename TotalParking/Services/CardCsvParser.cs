using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TotalParking.Services
{
    // Đọc file CSV danh sách thẻ và LỌC trước khi cho phép ghi.
    //
    // ===================== VÌ SAO TÁCH RIÊNG =====================
    // Cùng một bộ luật phải chạy ở hai chỗ: lúc XEM TRƯỚC và lúc GHI THẬT. Nếu
    // viết hai lần thì bản xem trước và bản ghi sẽ lệch nhau ngay lần sửa đầu,
    // và người dùng sẽ thấy "17 dòng hợp lệ" rồi nhận về 15 dòng trong DB.
    //
    // ===================== BẢY LUẬT =====================
    // Mỗi dòng trượt luật nào thì bị bỏ kèm lý do cụ thể, các dòng còn lại vẫn
    // nạp. Không bỏ cả file vì một dòng hỏng: file khách xuất ra luôn có vài dòng
    // thiếu dữ liệu, bắt họ sửa sạch mới cho nhập là đẩy việc sang chỗ không có
    // công cụ để làm.
    public class CardCsvParser
    {
        public const string DefaultCustomerType = "XT";
        public const string DefaultWeightClass  = "2200KG";

        private static readonly string[] CustomerTypes = { "VANG", "XT", "GHI" };
        private static readonly string[] WeightClasses = { "THUONG", "2200KG", "2600KG" };

        // Giá trị nhỏ hơn ngưỡng này không phải mã thẻ mà là thanh ghi nội bộ.
        // Cùng ngưỡng đang dùng ở v_slot_taken và CarLocatorService — ba nơi phải
        // khớp nhau, nếu không thì thẻ nạp được nhưng tìm xe lại không thấy.
        private const long MinCardValue = 65535;

        public class Row
        {
            public int    LineNo      { get; set; }
            public string CardCode    { get; set; }
            public string CardNo      { get; set; }
            public string CustomerType{ get; set; }
            public string WeightClass { get; set; }
            public string SourceLabel { get; set; }
            public bool   IsActive    { get; set; }
            public string Plate       { get; set; }
            public string Owner       { get; set; }

            // Null = hợp lệ. Khác null = lý do bị bỏ, hiển thị nguyên văn cho người dùng.
            public string Reject      { get; set; }
            public bool   Ok { get { return Reject == null; } }
        }

        public class Result
        {
            public IList<Row> Rows        = new List<Row>();
            public string     FatalError;          // hỏng tới mức không đọc được dòng nào
            public IList<Row> Accepted { get { return Rows.Where(r => r.Ok).ToList(); } }
            public IList<Row> Rejected { get { return Rows.Where(r => !r.Ok).ToList(); } }
        }

        // defaultLabel: dùng khi cột lo_nhap để trống — thường là "tên file + ngày".
        public Result Parse(string text, string defaultLabel)
        {
            var res = new Result();
            if (string.IsNullOrWhiteSpace(text))
            {
                res.FatalError = "File rong.";
                return res;
            }

            // BOM của UTF-8: Excel luôn thêm, và nếu không cắt thì tên cột đầu tiên
            // mang theo ba byte vô hình và mọi phép so tên cột đều trượt.
            text = text.TrimStart('﻿');

            var lines = text.Split(new[] { '\n' }, StringSplitOptions.None)
                            .Select(l => l.TrimEnd('\r'))
                            .ToList();

            int headerIdx = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
            if (headerIdx < 0)
            {
                res.FatalError = "File khong co dong nao.";
                return res;
            }

            var header = SplitCsv(lines[headerIdx])
                            .Select(h => h.Trim().ToLowerInvariant()).ToList();

            int iCode  = header.IndexOf("ma_the");
            int iNo    = header.IndexOf("so_the");
            int iType  = header.IndexOf("loai_khach");
            int iWc    = header.IndexOf("hang_tai");
            int iLabel = header.IndexOf("lo_nhap");
            int iAct   = header.IndexOf("kich_hoat");
            int iPlate = header.IndexOf("bien_so");
            int iOwner = header.IndexOf("chu_xe");

            if (iCode < 0 || iNo < 0)
            {
                res.FatalError =
                    "Thieu cot bat buoc. Dong tieu de phai co it nhat 'ma_the' va 'so_the'. " +
                    "Doc duoc: " + string.Join(", ", header);
                return res;
            }

            // Bắt trùng NGAY TRONG FILE, không chỉ trùng với DB. Một file có hai
            // dòng cùng mã thì dòng sau sẽ ghi đè dòng trước mà không ai biết.
            var seenCode = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenNo   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = headerIdx + 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var f = SplitCsv(lines[i]);
                Func<int, string> get = idx =>
                    idx >= 0 && idx < f.Count ? (f[idx] ?? "").Trim() : "";

                var row = new Row
                {
                    LineNo       = i + 1,
                    CardCode     = get(iCode).ToLowerInvariant(),
                    CardNo       = get(iNo),
                    CustomerType = get(iType).ToUpperInvariant(),
                    WeightClass  = get(iWc).ToUpperInvariant(),
                    SourceLabel  = get(iLabel),
                    Plate        = get(iPlate),
                    Owner        = get(iOwner)
                };

                string act = get(iAct);
                row.IsActive = act.Length == 0 || act == "1" ||
                               act.Equals("true", StringComparison.OrdinalIgnoreCase);

                if (row.SourceLabel.Length == 0) row.SourceLabel = defaultLabel ?? "IMPORT";
                if (row.SourceLabel.Length > 64)  row.SourceLabel = row.SourceLabel.Substring(0, 64);

                if (row.CustomerType.Length == 0) row.CustomerType = DefaultCustomerType;
                if (row.WeightClass.Length  == 0) row.WeightClass  = DefaultWeightClass;

                row.Reject = Validate(row, seenCode, seenNo);
                if (row.Ok)
                {
                    seenCode.Add(row.CardCode);
                    seenNo.Add(row.CardNo);
                }
                res.Rows.Add(row);
            }

            if (res.Rows.Count == 0) res.FatalError = "Khong co dong du lieu nao sau dong tieu de.";
            return res;
        }

        private static string Validate(Row r, HashSet<string> seenCode, HashSet<string> seenNo)
        {
            // 1. co ma the
            if (r.CardCode.Length == 0) return "thieu ma the";

            // 2. dung 8 ky tu hex
            if (r.CardCode.Length != 8 || !r.CardCode.All(Uri.IsHexDigit))
                return "ma the phai la 8 ky tu hex";

            // 3. KHAC bien so cua chinh dong do
            //
            // Luat de bat nhat va quan trong nhat. File khach gui 17/09 co 12/38
            // dong bi dan bien so vao o ma the, trong do SAU dong toan ky tu hex
            // nen khong the phat hien bang mat:
            //     ma_the = 30E83499   bien_so = 30E83499   <- la bien so
            //     ma_the = A0FF0790   bien_so = 30M03135   <- ma the that
            if (r.Plate.Length > 0 &&
                string.Equals(r.CardCode, r.Plate.Replace("-", "").Replace(" ", ""),
                              StringComparison.OrdinalIgnoreCase))
                return "ma the trung bien so - nhieu kha nang dan nham";

            // 4. gia tri du lon
            long val;
            if (!long.TryParse(r.CardCode, NumberStyles.HexNumber,
                               CultureInfo.InvariantCulture, out val))
                return "khong doc duoc ma the";
            if (val <= MinCardValue) return "gia tri qua nho, khong phai ma the";

            // 5. so the
            if (r.CardNo.Length == 0)  return "thieu so the";
            if (r.CardNo.Length > 16)  return "so the dai qua 16 ky tu";

            // 6. trung trong chinh file nay
            if (seenCode.Contains(r.CardCode)) return "ma the trung voi dong truoc trong file";
            if (seenNo.Contains(r.CardNo))     return "so the trung voi dong truoc trong file";

            // 7. ma tra cuu hop le
            if (!CustomerTypes.Contains(r.CustomerType))
                return "loai khach phai la VANG / XT / GHI";
            if (!WeightClasses.Contains(r.WeightClass))
                return "hang tai phai la THUONG / 2200KG / 2600KG";

            return null;
        }

        // Tách CSV có hỗ trợ dấu nháy kép, vì tên người và địa chỉ hay chứa dấu phẩy.
        // Không dùng String.Split(',') cho việc này: một dòng như
        //     61d41330,S.06575,XT,2200KG,"Lo A, dot 2",1,,
        // sẽ bị cắt thành 9 trường thay vì 8, và mọi cột sau đó lệch một ô.
        private static IList<string> SplitCsv(string line)
        {
            var outp = new List<string>();
            var sb   = new StringBuilder();
            bool q   = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q)
                {
                    if (c == '"')
                    {
                        // "" bên trong vùng nháy = một dấu nháy thật
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else q = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') q = true;
                    else if (c == ',') { outp.Add(sb.ToString()); sb.Length = 0; }
                    else sb.Append(c);
                }
            }
            outp.Add(sb.ToString());
            return outp;
        }
    }
}
