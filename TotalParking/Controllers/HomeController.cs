using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace TotalParking.Controllers
{
    public class HomeController : Controller
    {
        private ActionResult GetScadaView()
        {
            string filePath = Server.MapPath("~/SCADALayout/index.html");
            if (!System.IO.File.Exists(filePath))
            {
                return HttpNotFound("SCADA Layout index.html file is missing.");
            }
            return File(filePath, "text/html");
        }

        public ActionResult Index()
        {
            return GetScadaView();
        }

        public ActionResult FloorPlan()
        {
            return GetScadaView();
        }

        public ActionResult Zones()
        {
            return GetScadaView();
        }

        public ActionResult Routing()
        {
            return GetScadaView();
        }

        public ActionResult Alarms()
        {
            return GetScadaView();
        }

        public ActionResult Maintenance()
        {
            return GetScadaView();
        }

        public ActionResult Reports()
        {
            return GetScadaView();
        }

        public ActionResult Cards()
        {
            return GetScadaView();
        }

        public ActionResult Settings()
        {
            return GetScadaView();
        }

        public ActionResult Remote()
        {
            return GetScadaView();
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