using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Models;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using System.Data;
using CommunityPortal.Models.Admin;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
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

        // Helper method to update a user's status.
        private async Task<IActionResult> ChangeUserStatus(string userId, UserStatus status)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("ApproveUsers");
            }

            user.Status = status;
            await _userManager.UpdateAsync(user);
            TempData["SuccessMessage"] = "User status updated successfully.";
            return RedirectToAction("ApproveUsers");
        }

        // POST: /Admin/ApproveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public Task<IActionResult> ApproveUser(string userId) =>
            ChangeUserStatus(userId, UserStatus.Approved);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> DisableUser(string userId) =>
            ChangeUserStatus(userId, UserStatus.Disabled);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> BanUser(string userId) =>
            ChangeUserStatus(userId, UserStatus.Banned);

        // POST: /Admin/UnbanUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> UnbanUser(string userId) =>
            ChangeUserStatus(userId, UserStatus.PendingApproval);

        // Helper method to remove all related chat messages from a given user.
        private async Task RemoveUserChatMessages(string userId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.SenderId == userId || m.RecipientId == userId)
                .ToListAsync();

            if (messages.Any())
            {
                _context.ChatMessages.RemoveRange(messages);
                await _context.SaveChangesAsync();
            }
        }

        // POST: /Admin/RemoveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("ApproveUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("ApproveUsers");
            }

            // Remove associated chat messages.
            await RemoveUserChatMessages(userId);

            // Remove the user's roles.
            var roles = await _userManager.GetRolesAsync(user);
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, roles);
            if (!removeRolesResult.Succeeded)
            {
                return RedirectToAction("ApproveUsers");
            }

            // Delete the user.
            await _userManager.DeleteAsync(user);
            return RedirectToAction("ApproveUsers");
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
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
                var existingUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);

                if (existingUser != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This phone number is already in use.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    Status = UserStatus.PendingApproval,
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
                        Department = model.Department,
                        Address = model.Address,
                        Position = model.Position,
                    };

                    _context.Staffs.Add(staff);
                    await _context.SaveChangesAsync();

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
