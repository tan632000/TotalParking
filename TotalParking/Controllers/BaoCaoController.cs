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

        // GET /BaoCao/DoTinCayKhoi?ngay=30
        //
        // Tra loi "khoi nao can di xem truoc". Xem ghi chu tai BaoCaoRepository
        // ve vi sao tach ba con so thay vi gop thanh mot thang diem.
        public ActionResult DoTinCayKhoi(int? ngay)
        {
            try
            {
                int soNgay = ngay ?? 30;
                var ds = _repo.DoTinCay(soNgay);

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    so_ngay = soNgay,
                    // Bon con so cho bon the o dau trang.
                    dang_mat   = ds.Count(x => x.DangMo),
                    chap_chon  = ds.Count(x => x.Dang == "chap_chon"),
                    so_khoi    = ds.Count,
                    tong_gio   = Math.Round(ds.Sum(x => (double)x.TongPhut) / 60, 1),
                    items = ds.Select(x => new
                    {
                        block_no     = x.BlockNo,
                        zone_id      = x.ZoneId,
                        ip           = x.Ip,
                        so_lan       = x.SoLan,
                        tong_phut    = x.TongPhut,
                        lan_lau_nhat = x.LanLauNhat,
                        dang_mo      = x.DangMo,
                        gan_nhat     = x.GanNhat.ToString("dd-MM-yyyy HH:mm"),
                        dang         = x.Dang,
                        goi_y        = x.GoiY
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoCao/SucKhoeVongDoc
        //
        // Mot khoi co the KET NOI BINH THUONG ma thanh ghi o khong duoc cap nhat.
        // Day la hong kieu khac voi mat ket noi, nen do rieng.
        public ActionResult SucKhoeVongDoc()
        {
            try
            {
                var k = _repo.SucKhoeO();
                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    tong_o    = k.TongO,
                    tong_khoi = k.TongKhoi,
                    o_qua_han = k.OQuaHan,
                    o_chua_doc = k.OChuaDoc,
                    quet_gan_nhat = k.QuetGanNhat.HasValue
                        ? k.QuetGanNhat.Value.ToString("dd-MM-yyyy HH:mm:ss") : null,
                    // Bao nhieu phut truoc: mot moc thoi gian tuyet doi khong noi
                    // ngay duoc la vong quet con song hay da chet tu lau.
                    quet_cach_day_phut = k.QuetGanNhat.HasValue
                        ? (int?)(DateTime.Now - k.QuetGanNhat.Value).TotalMinutes : null,
                    khoi = k.Khoi.Select(x => new
                    {
                        block_no = x.BlockNo,
                        zone_id  = x.ZoneId,
                        so_o     = x.SoO,
                        chua_doc = x.ChuaDoc,
                        qua_han  = x.QuaHan,
                        cu_nhat_phut = x.CuNhatPhut
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoCao/XuHuongTraCuu?ngay=14
        public ActionResult XuHuongTraCuu(int? ngay)
        {
            try
            {
                int soNgay = ngay ?? 14;
                var ds = _repo.XuHuongTraCuu(soNgay);
                int tongTraCuu = ds.Sum(x => x.TraCuu);
                int tongThay   = ds.Sum(x => x.Thay);

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    so_ngay = soNgay,
                    tong_tra_cuu = tongTraCuu,
                    tong_thay    = tongThay,
                    tong_xe_vao  = ds.Sum(x => x.XeVao),
                    ti_le_thay   = tongTraCuu == 0 ? (double?)null
                                                   : Math.Round((double)tongThay * 100 / tongTraCuu, 1),
                    items = ds.Select(x => new
                    {
                        ngay        = x.Ngay.ToString("dd-MM"),
                        tra_cuu     = x.TraCuu,
                        thay        = x.Thay,
                        khong_thay  = x.KhongThay,
                        chua_tra_loi = x.ChuaTraLoi,
                        xe_vao      = x.XeVao,
                        ti_le_thay  = x.TiLeThay.HasValue ? (double?)Math.Round(x.TiLeThay.Value, 1) : null
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoCao/XeVaoTheoThu?ngay=30
        //
        // KHÔNG phải "mật độ đỗ xe %". Hệ thống không lưu lịch sử sức chứa nên
        // tỉ lệ lấp đầy của một ngày trong quá khứ là không tính được. Đây là
        // số xe vào bãi, trung bình mỗi ngày của từng thứ.
        public ActionResult XeVaoTheoThu(int? ngay)
        {
            try
            {
                int soNgay = ngay ?? 30;
                var ds = _repo.XeVaoTheoNgayTrongTuan(soNgay);

                // Cột cao nhất làm mốc 100%. Thanh ngang chỉ để so sánh các thứ
                // với nhau, nên tỉ lệ này là tương đối — nói rõ ở nhãn giao diện
                // để không ai đọc nhầm thành tỉ lệ lấp đầy bãi.
                double dinh = 0;
                foreach (var x in ds) if (x.TrungBinh > dinh) dinh = x.TrungBinh;

                string[] ten = { "", "Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư",
                                 "Thứ Năm", "Thứ Sáu", "Thứ Bảy" };

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    so_ngay = soNgay,
                    tong_xe = ds.Sum(x => x.TongXe),
                    dinh = Math.Round(dinh, 1),
                    items = ds.Select(x => new
                    {
                        thu      = x.Thu,
                        ten      = x.Thu >= 1 && x.Thu <= 7 ? ten[x.Thu] : "?",
                        so_ngay  = x.SoNgay,
                        tong_xe  = x.TongXe,
                        trung_binh = Math.Round(x.TrungBinh, 1),
                        phan_tram  = dinh == 0 ? 0 : (int)Math.Round(x.TrungBinh * 100 / dinh)
                    }).ToArray()
                });
            }
            catch (Exception ex)
            {
                return Json2(503, new { error = ex.Message });
            }
        }

        // GET /BaoCao/PhanLoaiXe?ngay=30
        public ActionResult PhanLoaiXe(int? ngay)
        {
            try
            {
                int soNgay = ngay ?? 30;
                var ds = _repo.PhanLoaiXe(soNgay);
                int tong = ds.Sum(x => x.SoXe);

                return Json2(200, new
                {
                    now = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"),
                    so_ngay = soNgay,
                    tong,
                    // Tổng số xe bị từ chối, đếm độc lập với nhãn phân loại của
                    // camera: camera xếp nhóm quá khổ nhiều hơn hẳn số lần bãi
                    // thật sự từ chối.
                    tong_tu_choi = ds.Sum(x => x.SoTuChoi),
                    items = ds.Select(x => new
                    {
                        nhan       = x.Nhan,
                        so_xe      = x.SoXe,
                        so_tu_choi = x.SoTuChoi,
                        ti_le      = tong == 0 ? 0 : Math.Round((double)x.SoXe * 100 / tong, 1),
                        cao_min    = x.CaoMin,
                        cao_max    = x.CaoMax
                    }).ToArray()
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
