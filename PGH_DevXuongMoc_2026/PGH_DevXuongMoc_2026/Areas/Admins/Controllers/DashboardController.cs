using Microsoft.AspNetCore.Mvc;

namespace PGH_DevXuongMoc_2026.Areas.Admins.Controllers
{
    public class DashboardController : Controller
    {
        [Area("Admins")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
