using System.Web;
using System.Web.Mvc;

namespace TotalParking
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Gate ban quyen: khoa tang web khi license het han/thieu. Whitelist
            // Activation + Ingest; khong dung tien trinh nen (xem LicenseGateFilter).
            filters.Add(new TotalParking.Filters.LicenseGateFilter());
        }
    }
}
