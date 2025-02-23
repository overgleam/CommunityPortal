using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class BillingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
