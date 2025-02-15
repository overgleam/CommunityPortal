using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class AnnouncementController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
