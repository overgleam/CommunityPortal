using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Models;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using System.Data;
using CommunityPortal.Models.Admin;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> AdminSettings()
        {
            var user = await _userManager.GetUserAsync(User);
            var admin = _context.Admins.FirstOrDefault(a => a.UserId == user.Id);

            var model = new AdminSettingsViewModel
            {
                FirstName = admin?.FirstName,
                LastName = admin?.LastName,
                Address = admin?.Address
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminSettings(AdminSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                var admin = _context.Admins.FirstOrDefault(a => a.UserId == user.Id);

                if (admin != null)
                {
                    // Update profile information
                    admin.FirstName = model.FirstName;
                    admin.LastName = model.LastName;
                    admin.Address = model.Address;

                    // Handle Password Change
                    if (!string.IsNullOrEmpty(model.CurrentPassword) || !string.IsNullOrEmpty(model.NewPassword))
                    {
                        // First verify the current password is correct
                        var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
                        
                        if (!isCurrentPasswordValid)
                        {
                            ModelState.AddModelError("CurrentPassword", "Current password is incorrect");
                            TempData["ErrorMessage"] = "Current password is incorrect.";
                            return View(model);
                        }

                        var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                        if (!changePasswordResult.Succeeded)
                        {
                            foreach (var error in changePasswordResult.Errors)
                            {
                                ModelState.AddModelError(string.Empty, error.Description);
                            }
                            TempData["ErrorMessage"] = "Failed to update password. Please check the requirements.";
                            return View(model);
                        }
                        else
                        {
                            TempData["SuccessMessage"] = "Profile and password updated successfully.";
                        }
                    }
                    else
                    {
                        TempData["SuccessMessage"] = "Profile updated successfully.";
                    }

                    await _context.SaveChangesAsync();
                    return RedirectToAction("AdminSettings");
                }
            }
            TempData["ErrorMessage"] = "Failed to update profile. Please check your inputs.";
            return View(model);
        }

        // GET: /Admin/ApproveUsers
        public async Task<IActionResult> ApproveUsers()
        {
            var users = await _userManager.Users
                .Include(u => u.Administrator)
                .Where(u => u.Administrator == null)
                .ToListAsync();

            var model = new ApproveUsersViewModel
            {
                Users = users
            };

            return View(model);
        }

        // POST: /Admin/ApproveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.Enable = true;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View("ApproveUsers", new ApproveUsersViewModel { Users = new List<ApplicationUser> { user } });
            }

            return RedirectToAction(nameof(ApproveUsers));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.Enable = false;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return RedirectToAction(nameof(ApproveUsers));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("ApproveUsers", new ApproveUsersViewModel { Users = new List<ApplicationUser> { user } });
        }
    }


}
