using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // API cảnh báo và sự cố thiết bị.
    //
    // ===================== VÌ SAO DANH SÁCH BỊ CHẶN =====================
    // Xem chú thích ở CanhBaoRepository: không có cơ chế giữ-xoá nào trong dự án,
    // nên bảng chỉ lớn lên. Endpoint này trả tất cả cảnh báo CHƯA xác nhận cộng
    // tối đa 200 dòng đã xác nhận gần nhất — đủ cho màn hình vận hành, và không
    // bao giờ thành một phản hồi vài chục MB.
    //
    // ===================== xac_nhan_boi KHÔNG PHẢI DANH TÍNH =====================
    // Hệ thống chưa có đăng nhập. Tên gửi lên là người vận hành TỰ KHAI, nên nó
    // được lưu kèm địa chỉ máy khách và không được trình bày ở đâu như một danh
    // tính đã xác thực. Bất kỳ ai trên mạng OT cũng gọi được endpoint này với tên
    // người khác — ghi rõ ở đây để không ai dựa vào cột đó như bằng chứng.
    public class AlarmController : Controller
    {
        private static readonly CanhBaoRepository _repo = new CanhBaoRepository();

        // GET /Alarm/List
        public ActionResult List()
        {
            try
            {
                var ds = _repo.Doc();
                return Json2(200, new
                {
                    now = DateTime.Now.ToString("HH:mm:ss"),
                    gioi_han_da_xac_nhan = CanhBaoRepository.GioiHanDaXacNhan,
                    tong = ds.Count,
                    chua_xac_nhan = ds.Count(c => c.ChuaXacNhan),
                    dang_mo       = ds.Count(c => c.DangMo),
                    canh_bao = ds.Select(c => new
                    {
                        id        = c.Id,
                        nguon     = c.Nguon,     // khớp data-type của bộ lọc
                        muc_do    = c.MucDo,     // khớp data-severity của bộ lọc
                        ma_loi    = c.MaLoi,
                        block_no  = c.BlockNo,
                        zone_id   = c.ZoneId,
                        thiet_bi  = c.ThietBi,
                        mo_ta     = c.MoTa,
                        xay_ra_luc = c.XayRaLuc.ToString("yyyy-MM-dd HH:mm:ss"),
                        het_luc    = c.HetLuc.HasValue
                            ? c.HetLuc.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                        dang_mo       = c.DangMo,
                        chua_xac_nhan = c.ChuaXacNhan,
                        xac_nhan_boi  = c.XacNhanBoi,
                        xac_nhan_ip   = c.XacNhanIp,
                        xac_nhan_luc  = c.XacNhanLuc.HasValue
                            ? c.XacNhanLuc.Value.ToString("yyyy-MM-dd HH:mm:ss") : null
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                // Trả lỗi có nội dung thay vì 500 trắng: giao diện phải phân biệt
                // được "không có cảnh báo nào" với "không đọc được cảnh báo".
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /Alarm/Summary
        //
        // ===================== KÍCH THƯỚC PHẢI CỐ ĐỊNH =====================
        // Header nằm trong layout dùng chung của 22 trang. Nếu nó poll
        // /Alarm/List thì mỗi chu kỳ kéo cả danh sách chỉ để lấy hai con số —
        // đo được 7.950 byte cho 16 cảnh báo, và phần chưa xác nhận không có
        // trần nên con số đó chỉ lớn lên đúng lúc hệ thống đang có sự cố.
        //
        // Vì vậy endpoint này KHÔNG được trả kèm mảng, kể cả "vài dòng gần
        // nhất cho tiện". Thêm một mảng vào đây là xoá bỏ toàn bộ lý do nó tồn
        // tại, và phép kiểm kich_thuoc_khong_tang_theo_so_canh_bao sẽ bắt.
        public ActionResult Summary()
        {
            try
            {
                var t = _repo.TomTat();
                return Json2(200, new
                {
                    now           = DateTime.Now.ToString("HH:mm:ss"),
                    chua_xac_nhan = t.ChuaXacNhan,
                    dang_mo       = t.DangMo,
                    muc_cao_nhat  = t.MucCaoNhat
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // POST /Alarm/Ack   body: id=123&nguoi=Nguyen Van A
        [HttpPost]
        public ActionResult Ack(long id, string nguoi)
        {
            if (string.IsNullOrWhiteSpace(nguoi))
                return Json2(400, new { error = "Thieu ten nguoi xac nhan." });

            try
            {
                bool duoc = _repo.XacNhan(id, nguoi.Trim(), Request.UserHostAddress);
                if (duoc) return Json2(200, new { id, xac_nhan_boi = nguoi.Trim() });

                // 409 chứ không 200: đã có người xác nhận trước, và người bấm sau
                // cần biết điều đó thay vì tưởng mình vừa xử lý xong.
                return Json2(409, new
                {
                    error = "Canh bao nay da duoc xac nhan truoc do.",
                    id
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // Mã tra cứu của sự cố do người vận hành bấm báo.
        public const string MaLoiSuCoKhan = "SU-CO-KHAN";

        // POST /Alarm/SuCoKhan   body: khoi=Block A-01&zoneId=2
        //
        // ===================== NÚT NÀY KHÔNG DỪNG MÁY =====================
        // Nó ghi một dòng vào CSDL, thế thôi. Dừng khẩn thật là việc của nút cơ
        // khí ngoài hiện trường. Nhãn trên giao diện phải nói đúng điều đó, nếu
        // không thì việc ghi được CSDL còn làm tệ hơn nút cũ: nó tạo cảm giác hệ
        // thống vừa "nhận lệnh".
        //
        // ===================== VÌ SAO block_no ĐỂ NULL =====================
        // Trang OperationControl chọn khối từ zoneBlocksMap — một danh sách tên
        // viết cứng ("Block A-01"), không phải block_no thật của 112 khối. Ánh xạ
        // tên đó sang một con số là bịa. Tên hiện trên màn hình được lưu vào
        // thiet_bi, đúng như những gì người bấm nhìn thấy.
        //
        // ===================== KHÔNG DÙNG KHOÁ CHỐNG TRÙNG =====================
        // Khác cảnh báo PLC: ở đó một sự cố kéo dài bị vòng 30 giây quét lại liên
        // tục nên phải chặn trùng. Ở đây mỗi lần bấm là một lần người báo, và mỗi
        // lần báo là một sự kiện riêng cần có dấu vết. Dùng khoá thì lần báo thứ
        // hai trên cùng khối sẽ bị nuốt im lặng cho tới khi ai đó đóng dòng cũ.
        [HttpPost]
        public ActionResult SuCoKhan(string khoi, int? zoneId)
        {
            if (string.IsNullOrWhiteSpace(khoi))
                return Json2(400, new { error = "Thieu ten khoi." });

            string ten = khoi.Trim();
            if (ten.Length > 64)
                return Json2(400, new { error = "Ten khoi qua dai." });

            try
            {
                long id = _repo.Ghi(new CanhBao
                {
                    Nguon   = "operation",
                    MucDo   = "critical",
                    MaLoi   = MaLoiSuCoKhan,
                    BlockNo = null,
                    ZoneId  = zoneId,
                    ThietBi = ten,
                    MoTa    = "Người vận hành báo sự cố khẩn tại " + ten +
                              ". Đây là ghi nhận, hệ thống không gửi lệnh dừng nào."
                });

                return Json2(200, new { id, khoi = ten, zone_id = zoneId });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
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
