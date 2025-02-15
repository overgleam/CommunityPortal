using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class EventsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
