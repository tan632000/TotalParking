using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using TotalParking.Services.Led;
using TotalParking.Services.Plc;

namespace TotalParking
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Nạp cấu hình PLC. Vòng poll chỉ chạy khi plc:enabled = true, còn
            // công cụ nghiệm thu /PlcStatus/Read thì dùng được ngay cả khi tắt.
            // Xem Services/Plc/PlcHost.cs.
            PlcHost.Initialize();

            // Tầng LED. Cùng khuôn: luôn nạp danh mục bảng, chỉ chạy vòng đẩy
            // khi led:enabled. Khi tắt có trật tự, LedHost xoá bảng về trạng
            // thái trống — board giữ nội dung cũ vĩnh viễn và không có watchdog.
            LedHost.Initialize();
        }
    }
}
