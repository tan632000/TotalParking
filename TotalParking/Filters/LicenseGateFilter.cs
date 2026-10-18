using System;
using System.Web.Mvc;
using TotalParking.Services.License;

namespace TotalParking.Filters
{
    // Gate ban quyen cho TANG WEB MVC. Khi LicenseService bao Locked:
    //   - navigation (controller Home hoac Accept text/html) -> redirect /Activation
    //   - con lai (cac controller API JSON) -> HTTP 403
    // Khi Valid: khong lam gi.
    //
    // Whitelist theo TEN controller: Activation (de nhap license) va Ingest
    // (may-noi-may loopback /health /vehicle, nuoi automation). Kiem tra whitelist
    // TRUOC ca khi goi IsLocked() de 2 controller nay vao duoc ke ca luc danh gia loi.
    //
    // KHONG dung Request.IsAjaxRequest(): poll SCADA la fetch thuan, khong co
    // X-Requested-With (finding C2-04). Phan biet bang controller + Accept.
    //
    // Gate CHI o day (tang MVC). Cac tien trinh nen PlcHost/LedHost/PgsHost khoi
    // dong o Global.asax.cs:26-60, khong qua pipeline nay, nen KHONG bi khoa
    // (AC-06) — lop an toan vat ly nguyen ven.
    //
    // Ghi chu hieu nang: moi request goi GetStatus() (doc file + verify RSA). Tai
    // cua he SCADA nay thap nen chap nhan duoc; neu can co the cache ngan sau.
    public class LicenseGateFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var controller = (filterContext.RouteData.Values["controller"] as string) ?? "";

            if (controller.Equals("Activation", StringComparison.OrdinalIgnoreCase) ||
                controller.Equals("Ingest", StringComparison.OrdinalIgnoreCase))
                return;

            if (!new LicenseService().IsLocked())
                return;

            var accept = filterContext.HttpContext.Request.Headers["Accept"] ?? "";
            bool navigation = controller.Equals("Home", StringComparison.OrdinalIgnoreCase)
                              || accept.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0;

            if (navigation)
            {
                var url = new UrlHelper(filterContext.RequestContext).Action("Index", "Activation");
                filterContext.Result = new RedirectResult(url);
            }
            else
            {
                filterContext.Result = new HttpStatusCodeResult(403, "License required");
            }
        }
    }
}
