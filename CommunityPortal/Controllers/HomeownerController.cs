using CommunityPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "homeowners")]
    public class HomeownerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public HomeownerController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
