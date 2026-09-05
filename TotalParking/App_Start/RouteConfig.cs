using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace TotalParking
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Endpoint cho app Camera AI. Phải nằm ở gốc site và phải đăng ký trước route
            // Default, xem docs/camera-led-routing-design.md §3.2.
            routes.MapRoute(
                name: "ingest-health",
                url: "health",
                defaults: new { controller = "Ingest", action = "Health" }
            );

            routes.MapRoute(
                name: "ingest-vehicle",
                url: "vehicle",
                defaults: new { controller = "Ingest", action = "Vehicle" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
