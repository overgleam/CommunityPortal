using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    public class FacilityController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
