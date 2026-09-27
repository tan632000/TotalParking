using System;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using Newtonsoft.Json;
using TotalParking.Services;

namespace TotalParking.Controllers
{
    // Số liệu cho trang Báo cáo & thống kê. CHỈ ĐỌC — không có đường nào từ đây
    // ghi xuống PLC hay đổi dữ liệu.
    public class BaoCaoController : Controller
    {
        private static readonly BaoCaoRepository _repo = new BaoCaoRepository();

        // GET /BaoCao/LichSuQuetThe?limit=50
        public ActionResult LichSuQuetThe(int? limit)
        {
            try
            {
                var ds = _repo.LichSuQuetThe(limit ?? 50);
                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    total = ds.Count,
                    tim_thay  = ds.Count(x => x.TimThay == true),
                    khong_thay = ds.Count(x => x.TimThay == false),
                    items = ds.Select(x => new
                    {
                        luc      = x.Luc.ToString("dd-MM-yyyy HH:mm:ss"),
                        block_no = x.BlockNo,
                        zone_id  = x.ZoneId,
                        ma_the   = x.MaThe,
                        so_the   = x.SoThe,
                        bien_so  = x.BienSo,
                        ten_xe   = x.TenXe,
                        // null = chua tra loi, KHAC voi "khong tim thay". Mot
                        // luot treo bi doc thanh "xe khong co trong bai" se lam
                        // nguoi truc di tim mot chiec xe dang nam yen o do.
                        tim_thay   = x.TimThay,
                        block_tra_ve = x.BlockTraVe,
                        ly_do      = x.LyDo
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoCao/NguyenNhanLoi
        public ActionResult NguyenNhanLoi()
        {
            try
            {
                var ds = _repo.NguyenNhanLoi();
                int tong = ds.Sum(x => x.SoLan);

                // Tỉ lệ và luỹ kế tính Ở ĐÂY chứ không để giao diện tự tính: hai
                // bảng cùng dữ liệu mà mỗi bên làm tròn một kiểu thì tổng sẽ
                // không ra 100% và người đọc mất niềm tin vào cả trang.
                double luyKe = 0;
                var items = ds.Select(x =>
                {
                    double ti = tong == 0 ? 0 : (double)x.SoLan * 100 / tong;
                    luyKe += ti;
                    return new
                    {
                        ma_loi   = x.MaLoi,
                        muc_do   = x.MucDo,
                        so_lan   = x.SoLan,
                        ti_le    = Math.Round(ti, 1),
                        luy_ke   = Math.Round(luyKe, 1),
                        gan_nhat = x.GanNhat.ToString("dd-MM-yyyy HH:mm")
                    };
                }).ToArray();

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    tong,
                    items
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
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
