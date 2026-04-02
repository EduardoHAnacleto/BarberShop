using Microsoft.AspNetCore.Mvc;

namespace BarberShop.Controllers
{
    public class ServiceController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
