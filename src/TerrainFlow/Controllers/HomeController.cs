using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;

namespace TerrainFlow.Controllers
{
    public class HomeController : Controller
    {

        public async Task<IActionResult> Index()
        {
            var x = HttpContext.User.Identity;
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Error()
        {
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
