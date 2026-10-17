using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using MySqlConnector;
using Newtonsoft.Json;
using TotalParking.Services;
using TotalParking.Services.Plc;

namespace TotalParking.Controllers
{
    // Số liệu cho trang Bảo trì: bấm một khối thì xem lỗi và động cơ của khối đó.
    // CHỈ ĐỌC — không có đường nào từ đây ghi xuống PLC hay đổi dữ liệu.
    public class BaoTriController : Controller
    {
        private const string DinhDang = "yyyy-MM-dd HH:mm:ss";

        private static readonly CanhBaoRepository  _canhBao = new CanhBaoRepository();
        private static readonly MotorRunRepository _dongCo  = new MotorRunRepository();

        // GET /BaoTri/LoiTheoKhoi — số cảnh báo đang mở theo khối, để tô thẻ khối.
        public ActionResult LoiTheoKhoi()
        {
            try
            {
                var dem = _canhBao.DemDangMoTheoKhoi();
                return Json2(200, new
                {
                    now   = DateTime.Now.ToString("HH:mm:ss"),
                    items = dem.OrderBy(p => p.Key)
                               .Select(p => new { block_no = p.Key, dang_mo = p.Value })
                               .ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoTri/Loi?block=12&limit=20
        public ActionResult Loi(string block, int? limit)
        {
            int khoi; int slot; int ma; string loi;
            if (!DocKhoi(block, out khoi, out slot, out ma, out loi)) return Json2(ma, new { error = loi });

            int n = Math.Max(1, Math.Min(limit ?? 20, 200));
            try
            {
                return Json2(200, new
                {
                    now      = DateTime.Now.ToString("HH:mm:ss"),
                    block_no = khoi,
                    dang_mo  = _canhBao.DangMoTheoKhoi(khoi).Select(Dong).ToArray(),
                    lich_su  = _canhBao.GanNhatTheoKhoi(khoi, n).Select(Dong).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoTri/DongCo?block=12&limit=50
        //
        // Danh sách động cơ lấy từ bảng bit của NOTE FINAL theo loại khối, không
        // lấy từ motor_run: động cơ chưa chạy lần nào vẫn phải hiện với số 0, nếu
        // không người bảo trì sẽ tưởng khối đó thiếu động cơ.
        public ActionResult DongCo(string block, int? limit)
        {
            int khoi; int slot; int ma; string loi;
            if (!DocKhoi(block, out khoi, out slot, out ma, out loi)) return Json2(ma, new { error = loi });

            DongCoDetector.LoaiKhoi loai;
            if (!DongCoDetector.BangBit.TryGetValue(slot, out loai))
                return Json2(400, new { error = "Khoi " + khoi + " co " + slot + " o, khong co trong NOTE FINAL (3/5/6/10)." });

            int n = Math.Max(1, Math.Min(limit ?? 50, 500));
            IList<TongHopDongCo> tongHop = new List<TongHopDongCo>();
            IList<LuotChay> lichSu = new List<LuotChay>();
            bool coDuLieu = true;
            string lyDo = null;

            try
            {
                tongHop = _dongCo.TongHop(khoi);
                lichSu  = _dongCo.LichSu(khoi, n);
            }
            catch (MySqlException ex) when (ex.Number == 1146)
            {
                // Bảng chưa tạo (62_motor_run.sql cần tài khoản quản trị): trả 200
                // để trang vẫn hiện danh sách động cơ, kèm lý do vì sao toàn số 0.
                coDuLieu = false;
                lyDo = "Chưa có bảng motor_run";
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }

            var theoMa = tongHop.ToDictionary(t => t.MotorCode);

            // Thời gian hoạt động = số lần × thời gian chuẩn của loại (người dùng
            // chốt 16/10/2026). Null khi loại không có thời gian chuẩn (nam châm)
            // hoặc mã không thuộc loại nào — 0 sẽ nói sai là "đã biết, bằng không".
            var dongCo = loai.DongCo.Select(dc =>
            {
                TongHopDongCo t;
                theoMa.TryGetValue(dc.Ma, out t);
                var phanLoai = DongCoDetector.PhanLoai(dc.Ma);
                int homNay = t == null ? 0 : t.HomNay;
                int tong   = t == null ? 0 : t.Tong;
                int? chuan = phanLoai == null ? null : phanLoai.ChuanGiay;
                return new
                {
                    motor_code             = dc.Ma,
                    bit                    = dc.Bit.ToString(),
                    loai                   = phanLoai == null ? null : phanLoai.Ma,
                    ten_loai               = phanLoai == null ? null : phanLoai.Ten,
                    thoi_gian_chuan_giay   = chuan,
                    hom_nay                = homNay,
                    tong                   = tong,
                    thoi_gian_hom_nay_giay = chuan.HasValue ? (long?)homNay * chuan.Value : null,
                    thoi_gian_tong_giay    = chuan.HasValue ? (long?)tong * chuan.Value : null,
                    lan_cuoi               = t != null && t.LanCuoi.HasValue ? t.LanCuoi.Value.ToString(DinhDang) : null
                };
            }).ToArray();

            // Một dòng cho mỗi loại CÓ MẶT trong khối, theo thứ tự trượt, nâng,
            // hàng rào, nam châm.
            var theoLoai = DongCoDetector.CacLoai
                .Where(l => dongCo.Any(d => d.loai == l.Ma))
                .Select(l =>
                {
                    var cung = dongCo.Where(d => d.loai == l.Ma).ToArray();
                    int soHomNay = cung.Sum(d => d.hom_nay);
                    int soTong   = cung.Sum(d => d.tong);
                    return new
                    {
                        loai                   = l.Ma,
                        ten_loai               = l.Ten,
                        thoi_gian_chuan_giay   = l.ChuanGiay,
                        so_dong_co             = cung.Length,
                        so_lan_hom_nay         = soHomNay,
                        so_lan_tong            = soTong,
                        thoi_gian_hom_nay_giay = l.ChuanGiay.HasValue ? (long?)soHomNay * l.ChuanGiay.Value : null,
                        thoi_gian_tong_giay    = l.ChuanGiay.HasValue ? (long?)soTong * l.ChuanGiay.Value : null
                    };
                }).ToArray();

            return Json2(200, new
            {
                now        = DateTime.Now.ToString("HH:mm:ss"),
                block_no   = khoi,
                loai_khoi  = slot,
                co_du_lieu = coDuLieu,
                ly_do      = lyDo,
                theo_loai  = theoLoai,
                tong_hop   = dongCo,
                lich_su = lichSu.Select(x =>
                {
                    var phanLoai = DongCoDetector.PhanLoai(x.MotorCode);
                    return new
                    {
                        motor_code      = x.MotorCode,
                        loai            = phanLoai == null ? null : phanLoai.Ma,
                        ten_loai        = phanLoai == null ? null : phanLoai.Ten,
                        bat_dau         = x.BatDau.ToString(DinhDang),
                        ket_thuc        = x.KetThuc.ToString(DinhDang),
                        thoi_luong_giay = Math.Round(x.ThoiLuongMs / 1000.0, 1)
                    };
                }).ToArray()
            });
        }

        // Khối hợp lệ = số nguyên VÀ là khối cơ khí đang dùng. Trang chỉ vẽ khối
        // cơ khí, và khối đỗ nền không có PLC lẫn động cơ.
        //
        // ma = 400 khi người gọi gửi sai, 503 khi chính máy chủ không đọc được DB —
        // trả 400 cho lỗi DB là đổ lỗi cho người dùng về một sự cố của hệ thống.
        private bool DocKhoi(string block, out int khoi, out int slot, out int ma, out string loi)
        {
            slot = 0; loi = null; ma = 400;
            if (!int.TryParse(block, out khoi) || khoi <= 0)
            {
                loi = "Thieu hoac sai tham so block.";
                return false;
            }

            IDictionary<int, int> loaiKhoi;
            try { loaiKhoi = _dongCo.DocLoaiKhoi(); }
            catch (Exception ex) { ma = 503; loi = "Khong doc duoc danh sach khoi: " + ex.Message; return false; }

            if (!loaiKhoi.TryGetValue(khoi, out slot))
            {
                loi = "Khoi " + khoi + " khong phai khoi co khi dang dung.";
                return false;
            }
            return true;
        }

        private static object Dong(CanhBao c)
        {
            return new
            {
                id           = c.Id,
                ma_loi       = c.MaLoi,
                muc_do       = c.MucDo,
                nguon        = c.Nguon,
                mo_ta        = c.MoTa,
                xay_ra_luc   = c.XayRaLuc.ToString(DinhDang),
                het_luc      = c.HetLuc.HasValue ? c.HetLuc.Value.ToString(DinhDang) : null,
                xac_nhan_boi = c.XacNhanBoi
            };
        }

        private ActionResult Json2(int status, object payload)
        {
            Response.StatusCode = status;
            Response.TrySkipIisCustomErrors = true;
            return new ContentResult
            {
                ContentType     = "application/json",
                Content         = JsonConvert.SerializeObject(payload),
                ContentEncoding = Encoding.UTF8
            };
        }
    }
}
