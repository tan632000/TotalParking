using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace TotalParking.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult FloorPlan()
        {
            return View();
        }

        public ActionResult Zones(int? id)
        {
            if (id.HasValue)
            {
                ViewBag.ZoneId = id.Value;
                return View("ZoneDetail");
            }
            return View();
        }

        public ActionResult ZoneDetail(int? id)
        {
            return RedirectToAction("Zones", new { id = id ?? 1 });
        }

        public ActionResult Routing()
        {
            return View();
        }

        public ActionResult Alarms()
        {
            return View();
        }

        public ActionResult Maintenance()
        {
            return View();
        }

        public ActionResult Reports()
        {
            return View();
        }

        public ActionResult Cards()
        {
            return View();
        }

        public ActionResult Settings()
        {
            return View();
        }

        public ActionResult Remote()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}