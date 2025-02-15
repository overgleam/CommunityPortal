using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Models;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using System.Data;
using CommunityPortal.Models.Admin;
using CommunityPortal.Models.Enums;
using CommunityPortal.Models.Staffs;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "admin, staff")]
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

        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var admin = await _context.Admins.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (admin == null)
            {
                return NotFound("Admin profile not found.");
            }

            var model = new AdminSettingsViewModel
            {
                FirstName = admin.FirstName,
                LastName = admin.LastName,
                Address = admin.Address
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(AdminSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound("User not found.");
                }

                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.UserId == user.Id);
                if (admin == null)
                {
                    return NotFound("Admin profile not found.");
                }

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
                return RedirectToAction("Settings");
            }
            TempData["ErrorMessage"] = "Failed to update profile. Please check your inputs.";
            return View(model);
        }

        // GET: /Admin/ApproveUsers
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ApproveUsers()
        {
            var users = await _userManager.Users
                .Include(u => u.Administrator)
                .Where(u => u.Administrator == null)
                .ToListAsync();

            var model = new ApproveUsersViewModel
            {
                Users = new List<UserWithRoleViewModel>()
            };  

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.FirstOrDefault() ?? "Unknown";

                model.Users.Add(new UserWithRoleViewModel
                {
                    User = user,
                    Role = role
                });
            }

            return View(model);
        }

        // GET: /Admin/GetUserDetails
        public async Task<IActionResult> GetUserDetails(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("User ID is required.");
            }

            var user = await _userManager.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            return PartialView("_UserDetails", user);
        }

        // POST: /Admin/ApproveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> ApproveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ApproveUsers");
            }

            user.Status = UserStatus.Approved;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User approved successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to approve user.";
            }

            return RedirectToAction("ApproveUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ApproveUsers");
            }

            user.Status = UserStatus.Disabled;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User disabled successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to disable user.";
            }

            return RedirectToAction("ApproveUsers");
        }

        // POST: /Admin/RemoveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ApproveUsers");
            }

            // Remove user roles
            var roles = await _userManager.GetRolesAsync(user);
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, roles);
            if (!removeRolesResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to remove user roles.";
                return RedirectToAction("ApproveUsers");
            }

            // Remove user
            var deleteResult = await _userManager.DeleteAsync(user);
            if (deleteResult.Succeeded)
            {
                TempData["SuccessMessage"] = "User removed successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to remove user.";
            }

            return RedirectToAction("ApproveUsers");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ApproveUsers");
            }

            user.Status = UserStatus.Banned;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User banned successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to ban user.";
            }

            return RedirectToAction("ApproveUsers");
        }

        // POST: /Admin/UnbanUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnbanUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] = "Invalid user ID.";
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("ApproveUsers");
            }

            // Update the user's status to Approved or any other appropriate status
            user.Status = UserStatus.Approved;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "User unbanned successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to unban user.";
            }

            return RedirectToAction("ApproveUsers");
        }

        [HttpGet]
        public IActionResult CreateStaff()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(StaffCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    Status = UserStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "staff");

                    var staff = new Staff
                    {
                        UserId = user.Id,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Department = model.Department
                    };

                    _context.Staffs.Add(staff);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Staff account created successfully!";
                    return RedirectToAction("ApproveUsers");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

    }
}
