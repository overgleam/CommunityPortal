using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CommunityPortal.Data;
using CommunityPortal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models.Staffs;
using CommunityPortal.Models.Admin;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "staff")]
    public class StaffController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public StaffController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /Staff/Settings
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var staff = await _context.Staffs.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (staff == null)
            {
                return NotFound("Admin profile not found.");
            }

            var model = new StaffSettingsViewModel
            {
                FirstName = staff.FirstName,
                LastName = staff.LastName,
                Department = staff.Department,
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(StaffSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                var staff = await _context.Staffs.FirstOrDefaultAsync(a => a.UserId == user.Id);
                if (staff == null)
                {
                    return NotFound("Admin profile not found.");
                }

                // Update profile information
                staff.FirstName = model.FirstName;
                staff.LastName = model.LastName;
                staff.Department = model.Department;

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
                return RedirectToAction("Settings");
            }
            TempData["ErrorMessage"] = "Failed to update profile. Please check your inputs.";
            return View(model);
        }
    }
}