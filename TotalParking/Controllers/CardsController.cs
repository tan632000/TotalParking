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
                    // Hai bảng tra cứu đi kèm để giao diện dựng ô chọn từ dữ liệu
                    // thật. Viết cứng id trong view thì thêm một hàng vào bảng tra
                    // cứu là view ghi sai id mà không có gì báo.
                    loai_khach = _repo.DanhSachTraCuu("customer_type")
                                      .Select(k => new { id = k.Key, ma = k.Value }).ToArray(),
                    hang_tai   = _repo.DanhSachTraCuu("weight_class")
                                      .Select(k => new { id = k.Key, ma = k.Value }).ToArray(),
                    cards = rows.Select(r => new
                    {
                        // card_id là thứ giao diện dùng để chỉ đúng dòng. Thiếu nó
                        // thì chỉ còn card_code, mà định danh bằng mã nhận từ thân
                        // POST là đường dẫn tới ghi đè nhầm thẻ của khách khác.
                        card_id       = r.CardId,
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
        // Độ dài cột theo đúng schema parking_card. Kiểm ở đây để người vận hành
        // nhận một câu tiếng Việt thay vì "Data too long for column 'plate' at
        // row 1" — sql_mode có STRICT_TRANS_TABLES nên CSDL ném lỗi chứ không cắt.
        private static readonly Dictionary<string, int> DoDaiCot = new Dictionary<string, int>
        {
            { "card_no", 16 }, { "card_type", 16 }, { "plate", 16 },
            { "weight_text", 32 }, { "vehicle_name", 64 }, { "customer_name", 128 }
        };

        private static string QuaDai(string ten, string giaTri)
        {
            int max;
            if (giaTri == null || !DoDaiCot.TryGetValue(ten, out max)) return null;
            return giaTri.Length > max
                ? string.Format("Trường {0} dài {1} ký tự, tối đa {2}.", ten, giaTri.Length, max)
                : null;
        }

        private static string Goi(string s)
        {
            if (s == null) return null;
            s = s.Trim();
            return s.Length == 0 ? null : s;
        }

        // POST /Cards/Update
        //   card_id, nguoi, card_no, card_type, vehicle_name, plate, customer_name,
        //   weight_text, expiry_date (dd/MM/yyyy), customer_type_id, weight_class_id
        //
        // KHÔNG nhận card_code. Xem ParkingCardRepository.CapNhat.
        [HttpPost]
        public ActionResult Update(int card_id, string nguoi)
        {
            if (string.IsNullOrWhiteSpace(nguoi))
                return Json2(400, new { error = "Thiếu tên người thao tác." });

            var cu = _repo.DocTheoId(card_id);
            if (cu == null) return Json2(404, new { error = "Không tìm thấy thẻ." });

            // Trường VẮNG MẶT trong form khác hẳn trường GỬI LÊN RỖNG.
            //
            // Goi() trả null cho cả hai, và CapNhat luôn đặt đủ 9 cột, nên nếu
            // lấy thẳng thì một POST thiếu vài khoá sẽ xoá trắng hồ sơ DEC của
            // thẻ khách — dữ liệu không nhập lại được từ trong ứng dụng. Vắng mặt
            // thì giữ nguyên giá trị cũ; chỉ khoá có mặt mới được ghi.
            Func<string, string, string> lay = (khoa, cu_) =>
                Request.Form.AllKeys.Contains(khoa) ? Goi(Request.Form[khoa]) : cu_;

            var e = new ParkingCardRepository.CardEdit
            {
                CardNo       = lay("card_no",       cu.CardNo),
                CardType     = lay("card_type",     cu.CardType),
                VehicleName  = lay("vehicle_name",  cu.VehicleName),
                Plate        = lay("plate",         cu.Plate),
                CustomerName = lay("customer_name", cu.CustomerName),
                WeightText   = lay("weight_text",   cu.WeightText)
            };

            if (string.IsNullOrEmpty(e.CardNo))
                return Json2(400, new { error = "Số thẻ không được để trống." });

            foreach (var cap in new[] {
                QuaDai("card_no", e.CardNo), QuaDai("card_type", e.CardType),
                QuaDai("plate", e.Plate), QuaDai("weight_text", e.WeightText),
                QuaDai("vehicle_name", e.VehicleName), QuaDai("customer_name", e.CustomerName) })
            {
                if (cap != null) return Json2(400, new { error = cap });
            }

            DateTime ngay;
            string exp = Request.Form.AllKeys.Contains("expiry_date")
                             ? Goi(Request.Form["expiry_date"]) : null;
            if (!Request.Form.AllKeys.Contains("expiry_date")) e.ExpiryDate = cu.ExpiryDate;
            else if (exp == null) e.ExpiryDate = null;
            else if (DateTime.TryParseExact(exp, "dd/MM/yyyy",
                         System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.None, out ngay))
                e.ExpiryDate = ngay;
            else
                return Json2(400, new { error = "Hạn dùng phải theo dạng dd/MM/yyyy." });

            int ctid, wcid;
            if (!int.TryParse(Request.Form["customer_type_id"], out ctid) ||
                !int.TryParse(Request.Form["weight_class_id"], out wcid))
                return Json2(400, new { error = "Thiếu loại khách hoặc hạng tải." });
            e.CustomerTypeId = ctid;
            e.WeightClassId  = wcid;

            try
            {
                int n = _repo.CapNhat(card_id, e);
                CardAdminLog.Ghi("SUA", card_id, cu.CardCode, nguoi, Request.UserHostAddress,
                    n > 0 ? "OK" : "KHONG_DOI",
                    string.Format("so_the {0}->{1}; bien_so {2}->{3}; khach {4}->{5}",
                        cu.CardNo, e.CardNo, cu.Plate ?? "-", e.Plate ?? "-",
                        cu.CustomerName ?? "-", e.CustomerName ?? "-"));

                return Json2(200, new { card_id, card_code = cu.CardCode, doi = n });
            }
            catch (MySqlConnector.MySqlException ex)
            {
                string cau = DichLoi(ex, e);
                CardAdminLog.Ghi("SUA", card_id, cu.CardCode, nguoi, Request.UserHostAddress,
                                 "TU_CHOI", cau);
                return Json2(409, new { error = cau });
            }
            catch (Exception ex)
            {
                CardAdminLog.Ghi("SUA", card_id, cu.CardCode, nguoi, Request.UserHostAddress,
                                 "LOI", ex.Message);
                return Json2(503, new { error = "Không ghi được: " + ex.Message });
            }
        }

        // Dịch mã lỗi CSDL thành câu người vận hành đọc được. Trả nguyên văn lỗi
        // MySQL là đẩy việc hiểu sang người không có cách nào hiểu.
        private static string DichLoi(MySqlConnector.MySqlException ex,
                                      ParkingCardRepository.CardEdit e)
        {
            switch (ex.Number)
            {
                case 1062:
                    return string.Format("Số thẻ {0} đã thuộc về một thẻ khác.", e.CardNo);
                case 1452:
                    return "Loại khách hoặc hạng tải không tồn tại trong danh mục.";
                case 1406:
                    return "Một trường nhập vào dài quá giới hạn của cột.";
                default:
                    return "Không ghi được (mã lỗi " + ex.Number + ").";
            }
        }

        // POST /Cards/SetActive   card_id, bat (0|1), nguoi
        [HttpPost]
        public ActionResult SetActive(int card_id, int bat, string nguoi)
        {
            if (string.IsNullOrWhiteSpace(nguoi))
                return Json2(400, new { error = "Thiếu tên người thao tác." });

            // Đọc TRƯỚC khi đổi: sau đó thì không còn giá trị cũ để ghi nhật ký.
            var cu = _repo.DocTheoId(card_id);
            if (cu == null) return Json2(404, new { error = "Không tìm thấy thẻ." });

            try
            {
                var kq = _repo.DatTrangThai(card_id, bat == 1);
                string thaoTac = bat == 1 ? "BAT_LAI" : "NGUNG_DUNG";

                if (kq == ParkingCardRepository.KetQuaDoiTrangThai.Ok)
                {
                    CardAdminLog.Ghi(thaoTac, card_id, cu.CardCode, nguoi,
                        Request.UserHostAddress, "OK",
                        "is_active " + (cu.IsActive ? "1" : "0") + "->" + bat);
                    return Json2(200, new { card_id, card_code = cu.CardCode, is_active = bat == 1 });
                }

                string cau = LyDo(kq);
                CardAdminLog.Ghi(thaoTac, card_id, cu.CardCode, nguoi,
                                 Request.UserHostAddress, "TU_CHOI", cau);
                return Json2(409, new { error = cau, ly_do = kq.ToString() });
            }
            catch (Exception ex)
            {
                CardAdminLog.Ghi(bat == 1 ? "BAT_LAI" : "NGUNG_DUNG", card_id, cu.CardCode,
                                 nguoi, Request.UserHostAddress, "LOI", ex.Message);
                return Json2(503, new { error = "Không đổi được: " + ex.Message });
            }
        }

        private static string LyDo(ParkingCardRepository.KetQuaDoiTrangThai kq)
        {
            switch (kq)
            {
                case ParkingCardRepository.KetQuaDoiTrangThai.DangTrongODo:
                    return "Thẻ này đang gắn với một xe trong ô đỗ. Tắt thẻ thì tài xế " +
                           "quẹt ở cổng sẽ bị từ chối và không lấy được xe ra.";
                case ParkingCardRepository.KetQuaDoiTrangThai.CoPhienDangMo:
                    return "Thẻ này đang có phiên gửi xe chưa kết thúc.";
                case ParkingCardRepository.KetQuaDoiTrangThai.VuaQuet:
                    return "Thẻ này vừa được quẹt trong 24 giờ qua. Chờ qua 24 giờ " +
                           "hoặc kiểm tra lại xe của khách trước khi tắt.";
                case ParkingCardRepository.KetQuaDoiTrangThai.KhongThay:
                    return "Không tìm thấy thẻ.";
                case ParkingCardRepository.KetQuaDoiTrangThai.KhongDoiDuoc:
                    return "Trạng thái thẻ vừa thay đổi bởi một thao tác khác. " +
                           "Tải lại danh sách rồi thử lại.";
                default:
                    return "Không đổi được trạng thái thẻ.";
            }
        }

        // POST /Cards/Delete   card_id, nguoi
        //
        // Xoá hẳn dòng thẻ. Chỉ được khi thẻ đã ngừng dùng; phiên gửi xe cũ được
        // giữ lại nhưng gỡ liên kết — xem ParkingCardRepository.Xoa.
        [HttpPost]
        public ActionResult Delete(int card_id, string nguoi)
        {
            if (string.IsNullOrWhiteSpace(nguoi))
                return Json2(400, new { error = "Thiếu tên người thao tác." });

            // Đọc TRƯỚC khi xoá: sau đó không còn gì để ghi vào nhật ký.
            var cu = _repo.DocTheoId(card_id);
            if (cu == null) return Json2(404, new { error = "Không tìm thấy thẻ." });

            // Ghi đủ hồ sơ vào nhật ký vì đây là bản ghi duy nhất còn lại của thẻ.
            string hoSo = string.Format("so_the {0}; bien_so {1}; khach {2}; lo {3}",
                cu.CardNo, cu.Plate ?? "-", cu.CustomerName ?? "-", cu.SourceLabel ?? "-");

            try
            {
                int soPhien;
                var kq = _repo.Xoa(card_id, out soPhien);
                if (kq == ParkingCardRepository.KetQuaXoa.Ok)
                {
                    // Số phiên bị gỡ liên kết phải vào nhật ký: đó là cách duy nhất
                    // để sau này biết các phiên card_id NULL đó từng thuộc thẻ nào.
                    CardAdminLog.Ghi("XOA", card_id, cu.CardCode, nguoi,
                                     Request.UserHostAddress, "OK",
                                     hoSo + "; phien_go_lien_ket " + soPhien);
                    return Json2(200, new { card_id, card_code = cu.CardCode, da_xoa = true,
                                            phien_go_lien_ket = soPhien });
                }

                string cau = LyDoXoa(kq);
                CardAdminLog.Ghi("XOA", card_id, cu.CardCode, nguoi,
                                 Request.UserHostAddress, "TU_CHOI", cau + " | " + hoSo);
                return Json2(409, new { error = cau, ly_do = kq.ToString() });
            }
            catch (Exception ex)
            {
                CardAdminLog.Ghi("XOA", card_id, cu.CardCode, nguoi,
                                 Request.UserHostAddress, "LOI", ex.Message);
                return Json2(503, new { error = "Không xoá được: " + ex.Message });
            }
        }

        private static string LyDoXoa(ParkingCardRepository.KetQuaXoa kq)
        {
            switch (kq)
            {
                case ParkingCardRepository.KetQuaXoa.ChuaNgungDung:
                    return "Thẻ đang bật. Bấm \"Ngừng dùng\" trước rồi mới xoá.";
                case ParkingCardRepository.KetQuaXoa.DangTrongODo:
                    return "Mã thẻ này vẫn đang nằm trong thanh ghi một ô đỗ.";
                case ParkingCardRepository.KetQuaXoa.CoPhienDangMo:
                    return "Thẻ này đang có phiên gửi xe chưa kết thúc.";
                case ParkingCardRepository.KetQuaXoa.VuaQuet:
                    return "Thẻ này vừa được quẹt trong 24 giờ qua.";
                case ParkingCardRepository.KetQuaXoa.CoLichSu:
                    return "Thẻ vẫn còn được dữ liệu khác tham chiếu tới nên chưa xoá được. " +
                           "Thẻ vẫn ở trạng thái ngừng dùng và bị từ chối ở cổng.";
                case ParkingCardRepository.KetQuaXoa.KhongThay:
                    return "Không tìm thấy thẻ (có thể vừa bị xoá bởi thao tác khác).";
                default:
                    return "Không xoá được thẻ. Tải lại danh sách rồi thử lại.";
            }
        }

        private ActionResult Json2(int status, object body)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return Content(JsonConvert.SerializeObject(body, Formatting.Indented),
                           "application/json", Encoding.UTF8);
        }
    }
}
