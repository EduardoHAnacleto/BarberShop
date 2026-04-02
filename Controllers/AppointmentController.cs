using Microsoft.AspNetCore.Mvc;

namespace BarberShop.Controllers
{
    public class AppointmentController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
