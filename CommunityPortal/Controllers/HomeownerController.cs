using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Homeowners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "homeowners")]
    public class HomeownerController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public HomeownerController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> HomeownerSettings()
        {
            var user = await _userManager.GetUserAsync(User);
            var homeowner = _context.Homeowners.FirstOrDefault(h => h.UserId == user.Id);

            var model = new HomeownerSettingsViewModel
            {
                FirstName = homeowner?.FirstName,
                LastName = homeowner?.LastName,
                BlockNumber = homeowner?.BlockNumber ?? 0,
                HouseNumber = homeowner?.HouseNumber ?? 0,
                Address = homeowner?.Address
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "homeowners")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HomeownerSettings(HomeownerSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var homeowner = _context.Homeowners.FirstOrDefault(h => h.UserId == user.Id);

                if (homeowner != null)
                {
                    homeowner.FirstName = model.FirstName;
                    homeowner.LastName = model.LastName;
                    homeowner.BlockNumber = model.BlockNumber;
                    homeowner.HouseNumber = model.HouseNumber;
                    homeowner.Address = model.Address;
                    await _context.SaveChangesAsync();
                    return RedirectToAction("HomeownerSettings");
                }
            }
            return View(model);
        }
    }
}
