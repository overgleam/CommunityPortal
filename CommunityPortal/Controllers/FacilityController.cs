using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class FacilityController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
