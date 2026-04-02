using Microsoft.AspNetCore.Mvc;

namespace BarberShop.Controllers
{
    public class WorkerService : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
