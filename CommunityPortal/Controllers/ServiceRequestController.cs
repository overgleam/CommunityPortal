using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class ServiceRequestController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
