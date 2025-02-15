using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class FeedbackController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
