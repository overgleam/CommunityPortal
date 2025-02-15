using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class BillingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
