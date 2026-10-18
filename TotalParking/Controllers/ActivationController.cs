using System;
using System.Web.Mvc;
using TotalParking.Services.License;

namespace TotalParking.Controllers
{
    // Man kich hoat ban quyen. Phai vao duoc KE CA khi he thong dang khoa:
    // LicenseGateFilter (task 04) whitelist controller nay theo ten.
    public class ActivationController : Controller
    {
        private readonly LicenseService _license = new LicenseService();

        [HttpGet]
        public ActionResult Index()
        {
            // Truy cap duoc MOI LUC: khi dang khoa de kich hoat, va khi con han de
            // GIA HAN SOM (tranh downtime). Khong con redirect ve Home khi hop le.
            var status = _license.GetStatus();
            ViewBag.Locked = status.IsLocked;
            ViewBag.Expiry = status.ExpiryUtc;
            return View();
        }

        // license la chuoi Base64/JSON (co dau {, "), khong phai HTML nguy hiem,
        // nen tat request validation de khoi bi chan.
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Index(string licenseText)
        {
            string error;
            // Thanh cong (kich hoat hoac gia han) -> ve Home voi han moi.
            if (_license.Activate(licenseText ?? string.Empty, out error))
                return RedirectToAction("Index", "Home");

            var status = _license.GetStatus();
            ViewBag.Error = error;
            ViewBag.Locked = status.IsLocked;
            ViewBag.Expiry = status.ExpiryUtc;
            return View();
        }
    }
}
