using Microsoft.AspNetCore.Mvc;

namespace Blog.Backend.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
