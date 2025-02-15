using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class ForumController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
