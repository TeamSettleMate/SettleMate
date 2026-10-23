using Microsoft.AspNetCore.Mvc;

namespace SettleMate.Controllers
{
    public class testingController1 : Controller
    {
        public IActionResult Index()
        {
            return View("hello");
        }
    }
}
