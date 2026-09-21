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
    //   Template  tải file Excel mẫu
    //   Preview   đọc file, lọc, BÁO TRƯỚC — chưa ghi gì
    //   Import    ghi thật, cả lô trong một giao dịch
    //
    // ===================== VÌ SAO ĐỌC XLSX CHỨ KHÔNG PHẢI CSV =====================
    // Khách quản lý thẻ bằng Excel và xuất ra .xlsx từ hệ thống DEC của toà nhà.
    // Bản trước đòi CSV 8 cột theo mẫu riêng, nghĩa là mỗi lần nhận danh sách
    // lại có người phải mở Excel, xoá 14 cột, đổi tên 8 cột còn lại rồi lưu sang
    // CSV. Một bước làm tay, không ai kiểm tra, và là chỗ dễ làm lệch dữ liệu
    // nhất trong cả quy trình.
    //
    // Nay đọc thẳng file khách xuất ra, khớp cột theo TÊN nên thêm hay bớt cột
    // không dùng cũng không ảnh hưởng.
    //
    // ===================== VÌ SAO TÁCH XEM TRƯỚC VÀ GHI =====================
    // `card_code` là khoá duy nhất. Nạp nhầm rồi thì gỡ ra không đơn giản: phải
    // biết đúng những dòng nào vừa vào, mà nếu một phần đã trùng sẵn thì không
    // phân biệt được dòng cũ với dòng mới.
    //
    // Nên bắt buộc đi qua hai bước: đọc file và báo cáo trước, người dùng nhìn
    // con số rồi mới bấm nạp. Cùng một bộ luật chạy ở cả hai bước
    // (CardImportParser), nên con số ở bước xem trước luôn đúng bằng con số thật
    // sự được ghi.
    public class CardsController : Controller
    {
        private static readonly ParkingCardRepository _repo   = new ParkingCardRepository();
        private static readonly CardImportParser      _parser = new CardImportParser();

        // Giới hạn cỡ file. File khách hiện tại 994 dòng ~ 65 KB, nên 8 MB đã rất
        // rộng tay. Có giới hạn để một file nhầm (ảnh, video) không bị nuốt vào
        // bộ nhớ — và .xlsx là file nén nên phải rộng hơn mức của CSV trước đây.
        private const int MaxBytes = 8 * 1024 * 1024;

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
                        card_type     = r.CardType,
                        vehicle_name  = r.VehicleName,
                        customer_type = r.CustomerType,
                        weight_class  = r.WeightClass,
                        weight_text   = r.WeightText,
                        plate         = r.Plate,
                        customer_name = r.CustomerName,
                        expiry_date   = r.ExpiryDate.HasValue
                                            ? r.ExpiryDate.Value.ToString("dd/MM/yyyy")
                                            : null,
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
        // Trả file mẫu TRẮNG để điền, đúng những cột mà bộ đọc thật sự dùng.
        // Sinh tại chỗ chứ không đọc file trong docs/: file mẫu phải luôn khớp
        // với bộ luật mà BẢN ĐANG CHẠY dùng. Đọc từ đĩa thì một lần deploy quên
        // chép file là mẫu và bộ lọc nói hai chuyện khác nhau.
        public ActionResult Template()
        {
            var rows = new List<IEnumerable<string>>
            {
                new[]
                {
                    "Mã định danh", "Tên định danh", "Loại", "Mã nhóm định danh",
                    "Tên phương tiện", "Phân loại tải trọng xe", "Biển số hiện tại",
                    "Ngày hết hạn", "Tên khách hàng"
                },
                // Điền sẵn mã nhóm vào dòng mẫu: bỏ trống cột này thì dòng bị coi
                // là không thuộc nhóm thẻ tháng ô tô và bị loại im lặng — lỗi khó
                // đoán nhất cho người lần đầu điền file.
                new[] { "", "", "Thẻ", CardImportParser.TargetGroup, "", "", "", "", "" }
            };

            return File(XlsxWriter.Build("Phương tiện trong hệ thống", rows),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "mau_import_the.xlsx");
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
                return Json2(400, new { error = "File qua lon (toi da 8 MB)." });

            XlsxReader.Sheet sheet;
            try
            {
                sheet = XlsxReader.ReadFirstSheet(file.InputStream);
            }
            catch (XlsxReader.XlsxFormatException ex)
            {
                // Lỗi định dạng đã có câu tiếng Việt giải thích cách sửa, đưa
                // nguyên văn cho người dùng.
                return Json2(400, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Json2(400, new { error = "Khong doc duoc file Excel: " + ex.Message });
            }

            var parsed = _parser.Parse(sheet, BuildLabel(file.FileName));
            if (parsed.FatalError != null)
                return Json2(400, new { error = parsed.FatalError });

            // Đối chiếu với thẻ ĐÃ CÓ trong CSDL. Làm sau khi parse vì đây là luật
            // phụ thuộc trạng thái hệ thống, không phải lỗi định dạng file — và nó
            // phải chạy ở CẢ hai bước, nếu không thì bước xem trước sẽ hứa nhiều
            // hơn bước ghi làm được.
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nos   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { _repo.LoadExistingKeys(codes, nos); }
            catch (Exception ex) { return Json2(500, new { error = "Khong doc duoc CSDL: " + ex.Message }); }

            var toInsert = new List<CardImportParser.Row>();
            var toUpdate = new List<CardImportParser.Row>();

            foreach (var r in parsed.Rows.Where(x => x.Ok))
            {
                if (codes.Contains(r.CardCode))
                {
                    // Thẻ đã có: cập nhật hồ sơ thay vì bỏ qua. 17 thẻ nạp ngày
                    // 17/09 đang không có biển số, tên khách hay hạng tải thật,
                    // mà file này có đủ — bỏ qua chúng là tự nguyện giữ dữ liệu cũ
                    // thiếu hơn trong khi dữ liệu đúng đang nằm ngay trong file.
                    toUpdate.Add(r);
                }
                else if (nos.Contains(r.CardNo))
                {
                    // Mã thẻ mới nhưng số thẻ đã thuộc một thẻ KHÁC. Không thêm
                    // được (vướng khoá duy nhất) mà cũng không cập nhật được: hai
                    // dòng khác nhau, không biết dòng nào đúng. Để người đối chiếu.
                    r.Reject = "ten dinh danh da thuoc mot the khac trong he thong";
                }
                else
                {
                    toInsert.Add(r);
                }
            }

            int written = 0, updated = 0;
            if (commit && (toInsert.Count > 0 || toUpdate.Count > 0))
            {
                try
                {
                    var res = _repo.WriteBatch(toInsert, toUpdate);
                    written = res.Inserted;
                    updated = res.Updated;
                }
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
                sheet_name = parsed.SheetName,
                label      = toInsert.Count > 0 ? toInsert[0].SourceLabel : BuildLabel(file.FileName),
                group      = CardImportParser.TargetGroup,

                total_data_rows  = parsed.TotalDataRows,
                other_group_rows = parsed.OtherGroupRows,
                read_rows        = parsed.Rows.Count,
                accepted         = toInsert.Count + toUpdate.Count,
                to_insert        = toInsert.Count,
                to_update        = toUpdate.Count,
                rejected         = parsed.Rejected.Count,
                guessed_weight   = toInsert.Concat(toUpdate).Count(r => r.WeightGuessed),
                written,
                updated,

                by_reason = byReason,
                rows = parsed.Rows.Select(r => new
                {
                    line          = r.LineNo,
                    card_code     = r.CardCode,
                    card_no       = r.CardNo,
                    vehicle_name  = r.VehicleName,
                    weight_class  = r.WeightClass,
                    weight_text   = r.WeightText,
                    plate         = r.Plate,
                    customer_name = r.CustomerName,
                    expiry_date   = r.ExpiryDate.HasValue
                                        ? r.ExpiryDate.Value.ToString("dd/MM/yyyy")
                                        : null,
                    ok            = r.Ok,
                    reject        = r.Reject
                }).ToArray()
            });
        }

        // Nhãn lô nhập = tên file + ngày. Dùng để lọc và gỡ theo lô, thứ cần nhất
        // khi một lô nhập sai và phải rút lại. Parser tự thêm hậu tố cho những
        // dòng phải đoán hạng tải, nên ở đây chỉ dựng phần gốc.
        private static string BuildLabel(string fileName)
        {
            string label = Path.GetFileNameWithoutExtension(fileName ?? "");
            if (string.IsNullOrWhiteSpace(label)) label = "IMPORT";
            return (label + " " + DateTime.Now.ToString("dd/MM")).Trim();
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
