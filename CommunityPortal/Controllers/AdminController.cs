using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Models;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using System.Data;
using CommunityPortal.Models.Admin;
using CommunityPortal.Models.Enums;
using CommunityPortal.Models.ServiceRequest;

namespace CommunityPortal.Controllers
{
    [Authorize(Roles = "admin, staff")]
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
                .Where(u => u.Administrator == null && !u.IsDeleted)
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

            // Soft delete the user
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.Status = UserStatus.Disabled;

            // Update the user
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to remove user.";
                return RedirectToAction("ApproveUsers");
            }

            TempData["SuccessMessage"] = "User has been successfully removed.";
            return RedirectToAction("ApproveUsers");
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult CreateStaff()
        {
            ViewBag.Departments = DepartmentPositions.GetAllDepartments();
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
                ModelState.AddModelError(string.Empty, result.Errors.First().Description);
            }
            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public IActionResult GetPositionsForDepartment(string department)
        {
            var positions = DepartmentPositions.GetPositionsForDepartment(department);
            return Json(positions);
        }

        // GET: Admin/ServiceCategories
        public async Task<IActionResult> ServiceCategories()
        {
            var categories = await _context.ServiceCategories
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        // GET: Admin/DeletedServiceCategories
        public async Task<IActionResult> DeletedServiceCategories()
        {
            var categories = await _context.ServiceCategories
                .Where(c => c.IsDeleted)
                .OrderBy(c => c.Name)
                .ToListAsync();
            return View(categories);
        }

        // GET: Admin/CreateServiceCategory
        public IActionResult CreateServiceCategory()
        {
            return View();
        }

        // POST: Admin/CreateServiceCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateServiceCategory(ServiceCategory category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Service category created successfully.";
                return RedirectToAction(nameof(ServiceCategories));
            }
            return View(category);
        }

        // GET: Admin/EditServiceCategory/5
        public async Task<IActionResult> EditServiceCategory(int id)
        {
            var category = await _context.ServiceCategories
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            return View(category);
        }

        // POST: Admin/EditServiceCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditServiceCategory(int id, ServiceCategory category)
        {
            if (id != category.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingCategory = await _context.ServiceCategories
                        .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

                    if (existingCategory == null)
                    {
                        return NotFound();
                    }

                    existingCategory.Name = category.Name;
                    existingCategory.Description = category.Description;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Service category updated successfully.";
                    return RedirectToAction(nameof(ServiceCategories));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceCategoryExists(category.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(category);
        }

        // POST: Admin/DeleteServiceCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteServiceCategory(int id)
        {
            var category = await _context.ServiceCategories
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            // Check if there are any active service requests using this category
            var hasActiveRequests = await _context.ServiceRequests
                .AnyAsync(sr => sr.ServiceCategoryId == id && 
                    (sr.Status == ServiceRequestStatus.Pending || 
                     sr.Status == ServiceRequestStatus.Assigned || 
                     sr.Status == ServiceRequestStatus.InProgress));

            if (hasActiveRequests)
            {
                TempData["ErrorMessage"] = "Cannot delete category with active service requests.";
                return RedirectToAction(nameof(ServiceCategories));
            }

            // Soft delete
            category.IsDeleted = true;
            category.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Service category deleted successfully.";
            return RedirectToAction(nameof(ServiceCategories));
        }

        // POST: Admin/RestoreServiceCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreServiceCategory(int id)
        {
            var category = await _context.ServiceCategories
                .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted);

            if (category == null)
            {
                return NotFound();
            }

            category.IsDeleted = false;
            category.DeletedAt = null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Service category restored successfully.";
            return RedirectToAction(nameof(DeletedServiceCategories));
        }

        private bool ServiceCategoryExists(int id)
        {
            return _context.ServiceCategories.Any(e => e.Id == id);
        }

        // GET: /Admin/DeletedUsers
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeletedUsers()
        {
            var users = await _userManager.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .Where(u => u.IsDeleted)
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

        // POST: /Admin/RestoreUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> RestoreUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("DeletedUsers");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("DeletedUsers");
            }

            user.IsDeleted = false;
            user.DeletedAt = null;
            user.Status = UserStatus.PendingApproval;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to restore user.";
                return RedirectToAction("DeletedUsers");
            }

            TempData["SuccessMessage"] = "User has been successfully restored.";
            return RedirectToAction("DeletedUsers");
        }
    }
}
