using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // API quản lý thẻ xe cho trang Home/Cards.
    //
    //   List      danh sách thẻ thật trong CSDL
    //   Template  tải file CSV mẫu
    //   Preview   đọc file, lọc, BÁO TRƯỚC — chưa ghi gì
    //   Import    ghi thật, cả lô trong một giao dịch
    //
    // ===================== VÌ SAO TÁCH XEM TRƯỚC VÀ GHI =====================
    // `card_code` là khoá duy nhất. Nạp nhầm rồi thì gỡ ra không đơn giản: phải
    // biết đúng những dòng nào vừa vào, mà nếu một phần đã trùng sẵn thì không
    // phân biệt được dòng cũ với dòng mới.
    //
    // Nên bắt buộc đi qua hai bước: đọc file và báo cáo trước, người dùng nhìn
    // con số rồi mới bấm nạp. Cùng một bộ luật chạy ở cả hai bước (CardCsvParser),
    // nên con số ở bước xem trước luôn đúng bằng con số thật sự được ghi.
    public class CardsController : Controller
    {
        private static readonly ParkingCardRepository _repo   = new ParkingCardRepository();
        private static readonly CardCsvParser         _parser = new CardCsvParser();

        // Giới hạn cỡ file. 470 thẻ hiện tại ~ 40 KB, nên 2 MB đã rất rộng tay.
        // Có giới hạn để một file nhầm (ảnh, video) không bị nuốt vào bộ nhớ.
        private const int MaxBytes = 2 * 1024 * 1024;

        // GET /Cards/List
        public ActionResult List()
        {
            try
            {
                var rows = _repo.GetAllRows();
                return Json2(200, new
                {
                    now   = DateTime.Now.ToString("HH:mm:ss"),
                    total = rows.Count,
                    active = rows.Count(r => r.IsActive),
                    labels = rows.Select(r => r.SourceLabel).Distinct().OrderBy(s => s).ToArray(),
                    cards = rows.Select(r => new
                    {
                        card_code     = r.CardCode,
                        card_no       = r.CardNo,
                        customer_type = r.CustomerType,
                        weight_class  = r.WeightClass,
                        source_label  = r.SourceLabel,
                        is_active     = r.IsActive,
                        created_at    = r.CreatedAt.ToString("dd/MM/yyyy")
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(500, new { error = ex.Message });
            }
        }

        // GET /Cards/Template
        //
        // Trả file mẫu TRẮNG để điền. Sinh tại chỗ chứ không đọc file trong docs/:
        // file mẫu phải luôn khớp với bộ luật mà BẢN ĐANG CHẠY dùng. Đọc từ đĩa thì
        // một lần deploy quên chép file là mẫu và bộ lọc nói hai chuyện khác nhau.
        public ActionResult Template()
        {
            var sb = new StringBuilder();
            sb.Append('﻿');   // BOM: Excel mở mới hiện đúng tiếng Việt
            sb.Append("ma_the,so_the,loai_khach,hang_tai,lo_nhap,kich_hoat,bien_so,chu_xe\r\n");
            sb.Append(",,,,,,,\r\n");

            return File(Encoding.UTF8.GetBytes(sb.ToString()),
                        "text/csv", "mau_import_the.csv");
        }

        // POST /Cards/Preview   (multipart, field "file")
        [HttpPost]
        public ActionResult Preview()
        {
            return Handle(commit: false);
        }

        // POST /Cards/Import    (multipart, field "file")
        [HttpPost]
        public ActionResult Import()
        {
            return Handle(commit: true);
        }

        private ActionResult Handle(bool commit)
        {
            var file = Request.Files != null && Request.Files.Count > 0 ? Request.Files[0] : null;
            if (file == null || file.ContentLength == 0)
                return Json2(400, new { error = "Chua chon file." });
            if (file.ContentLength > MaxBytes)
                return Json2(400, new { error = "File qua lon (toi da 2 MB)." });

            string text;
            try
            {
                // UTF-8 có phát hiện BOM. File Excel xuất ra hầu hết là UTF-8;
                // nếu gặp file ANSI thì dấu tiếng Việt sẽ hỏng, nhưng các cột mình
                // thật sự dùng (mã thẻ, số thẻ, mã tra cứu) đều là ASCII nên vẫn
                // nạp đúng — chỉ tên chủ xe hiển thị sai, mà cột đó không được lưu.
                using (var sr = new StreamReader(file.InputStream, new UTF8Encoding(true), true))
                    text = sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                return Json2(400, new { error = "Khong doc duoc file: " + ex.Message });
            }

            string label = Path.GetFileNameWithoutExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(label)) label = "IMPORT";
            label = (label + " " + DateTime.Now.ToString("dd/MM")).Trim();
            if (label.Length > 64) label = label.Substring(0, 64);

            var parsed = _parser.Parse(text, label);
            if (parsed.FatalError != null)
                return Json2(400, new { error = parsed.FatalError });

            // Loại thêm những dòng trùng với thẻ ĐÃ CÓ trong CSDL. Làm sau khi
            // parse vì đây là luật phụ thuộc trạng thái hệ thống, không phải lỗi
            // định dạng file — và nó phải chạy ở CẢ hai bước, nếu không thì bước
            // xem trước sẽ hứa nhiều hơn bước ghi làm được.
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nos   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { _repo.LoadExistingKeys(codes, nos); }
            catch (Exception ex) { return Json2(500, new { error = "Khong doc duoc CSDL: " + ex.Message }); }

            int already = 0;
            foreach (var r in parsed.Rows.Where(x => x.Ok))
            {
                if (codes.Contains(r.CardCode)) { r.Reject = "ma the da co trong he thong"; already++; }
                else if (nos.Contains(r.CardNo)) { r.Reject = "so the da co trong he thong"; already++; }
            }

            var accepted = parsed.Accepted;
            int written = 0;
            if (commit && accepted.Count > 0)
            {
                try { written = _repo.InsertBatch(accepted); }
                catch (Exception ex)
                {
                    return Json2(500, new
                    {
                        error = "Ghi CSDL that bai, KHONG dong nao duoc nap: " + ex.Message
                    });
                }
            }

            // Gom lý do bị bỏ để hiện bảng tóm tắt. Danh sách chi tiết vẫn trả về
            // đầy đủ để người dùng tải xuống và gửi lại cho bên cấp dữ liệu sửa.
            var byReason = parsed.Rejected
                .GroupBy(r => r.Reject)
                .Select(g => new { reason = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToArray();

            return Json2(200, new
            {
                committed  = commit,
                file_name  = file.FileName,
                label,
                read_rows  = parsed.Rows.Count,
                accepted   = accepted.Count,
                rejected   = parsed.Rejected.Count,
                already_in_db = already,
                written,
                by_reason  = byReason,
                rows = parsed.Rows.Select(r => new
                {
                    line          = r.LineNo,
                    card_code     = r.CardCode,
                    card_no       = r.CardNo,
                    customer_type = r.CustomerType,
                    weight_class  = r.WeightClass,
                    plate         = r.Plate,
                    owner         = r.Owner,
                    ok            = r.Ok,
                    reject        = r.Reject
                }).ToArray()
            });
        }

        // Cùng cách trả JSON với các controller giám sát khác: mã HTTP thật, và
        // không để MVC tự bọc thêm lớp nào.
        private ActionResult Json2(int status, object body)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(body, Formatting.Indented),
                           "application/json", Encoding.UTF8);
        }
    }
}
