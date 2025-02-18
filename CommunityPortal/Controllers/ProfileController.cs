using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Gets the user with all associated roles included.
        /// When in edit mode, it ensures non-admins can only edit their own profile.
        /// </summary>
        private async Task<ApplicationUser> GetUserAsync(string userId, bool isEditMode = false)
        {
            if (string.IsNullOrEmpty(userId))
            {
                // Get the current user including role details.
                return await _userManager.Users
                    .Include(u => u.Administrator)
                    .Include(u => u.Staff)
                    .Include(u => u.Homeowner)
                    .FirstOrDefaultAsync(u => u.Id == _userManager.GetUserId(User));
            }

            if (isEditMode)
            {
                // Admins can edit any profile.
                if (User.IsInRole("admin"))
                {
                    return await _userManager.Users
                        .Include(u => u.Administrator)
                        .Include(u => u.Staff)
                        .Include(u => u.Homeowner)
                        .FirstOrDefaultAsync(u => u.Id == userId);
                }

                // Other users can only edit their own profiles.
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser?.Id != userId)
                {
                    return null;
                }
            }

            // For view operations, all authenticated users can view profiles
            return await _userManager.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        /// <summary>
        /// Returns the correct view and view model depending on the user's role.
        /// </summary>
        private IActionResult GetProfileView(ApplicationUser user)
        {
            if (user.Administrator != null)
            {
                var model = new AdminProfileViewModel
                {
                    User = user,
                    FirstName = user.Administrator.FirstName,
                    LastName = user.Administrator.LastName,
                    Address = user.Administrator.Address
                };
                return View("AdminViewProfile", model);
            }
            else if (user.Staff != null)
            {
                var model = new StaffProfileViewModel
                {
                    User = user,
                    FirstName = user.Staff.FirstName,
                    LastName = user.Staff.LastName,
                    Department = user.Staff.Department,
                    Position = user.Staff.Position,
                    Address = user.Staff.Address
                };
                return View("StaffViewProfile", model);
            }
            else if (user.Homeowner != null)
            {
                var model = new HomeownerProfileViewModel
                {
                    User = user,
                    FirstName = user.Homeowner.FirstName,
                    LastName = user.Homeowner.LastName,
                    BlockNumber = user.Homeowner.BlockNumber,
                    HouseNumber = user.Homeowner.HouseNumber,
                    Address = user.Homeowner.Address,
                    MoveInDate = user.Homeowner.MoveInDate,
                    TypeOfResidency = user.Homeowner.TypeOfResidency
                };
                return View("HomeownerViewProfile", model);
            }
            else
            {
                return NotFound("User role not found.");
            }
        }

        // GET: /Profile/View/{userId?}
        public async Task<IActionResult> ViewProfile(string userId = null)
        {
            var user = await GetUserAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            return GetProfileView(user);
        }

        // GET: /Profile/Edit/{userId?}
        public async Task<IActionResult> Edit(string userId = null)
        {
            var user = await GetUserAsync(userId, true);
            if (user == null)
            {
                return Forbid();
            }

            if (user.Administrator != null)
            {
                var model = new AdminProfileEditViewModel
                {
                    User = user,
                    FirstName = user.Administrator.FirstName,
                    LastName = user.Administrator.LastName,
                    Address = user.Administrator.Address,
                    PhoneNumber = user.PhoneNumber
                };
                return View("AdminEditProfile", model);
            }
            else if (user.Staff != null)
            {
                var model = new StaffProfileEditViewModel
                {
                    User = user,
                    FirstName = user.Staff.FirstName,
                    LastName = user.Staff.LastName,
                    Department = user.Staff.Department,
                    Position = user.Staff.Position,
                    Address = user.Staff.Address,
                    PhoneNumber = user.PhoneNumber
                };
                return View("StaffEditProfile", model);
            }
            else if (user.Homeowner != null)
            {
                var model = new HomeownerProfileEditViewModel
                {
                    User = user,
                    FirstName = user.Homeowner.FirstName,
                    LastName = user.Homeowner.LastName,
                    BlockNumber = user.Homeowner.BlockNumber,
                    HouseNumber = user.Homeowner.HouseNumber,
                    Address = user.Homeowner.Address,
                    MoveInDate = user.Homeowner.MoveInDate,
                    TypeOfResidency = user.Homeowner.TypeOfResidency,
                    PhoneNumber = user.PhoneNumber
                };
                return View("HomeownerEditProfile", model);
            }
            else
            {
                return NotFound("User role not found.");
            }
        }

        /// <summary>
        /// Removes password validation from the model state for admin users.
        /// </summary>
        private IActionResult RemovePasswordValidationForAdmin(ModelStateDictionary modelState)
        {
            if (User.IsInRole("admin"))
            {
                modelState.Remove("Password");
                modelState.Remove("ConfirmPassword");
            }
            return null;
        }

        /// <summary>
        /// Helper to update the phone number if it has changed.
        /// Returns an IActionResult with an error view if any issues occur, otherwise null.
        /// </summary>
        private async Task<IActionResult> UpdatePhoneNumberIfChanged(ApplicationUser user, string newPhoneNumber, string viewName, object model)
        {
            if (user.PhoneNumber != newPhoneNumber)
            {
                var existingUser = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == newPhoneNumber);
                if (existingUser != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This phone number is already in use.");
                    return View(viewName, model);
                }
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, newPhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    ModelState.AddModelError("PhoneNumber", "Error updating phone number");
                    return View(viewName, model);
                }
            }
            return null;
        }

        /// <summary>
        /// Helper to validate the provided password.
        /// If 'requirePassword' is true and the password is not provided, an error is added.
        /// Returns an IActionResult with an error view if validation fails, otherwise null.
        /// </summary>
        private async Task<IActionResult> ValidatePassword(ApplicationUser user, string password, bool requirePassword, string viewName, object model)
        {
            if (requirePassword || !string.IsNullOrEmpty(password))
            {
                if (string.IsNullOrEmpty(password))
                {
                    ModelState.AddModelError("Password", "Password is required.");
                    return View(viewName, model);
                }
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("Password", "Incorrect password.");
                    return View(viewName, model);
                }
            }
            return null;
        }

        // POST: /Profile/EditAdminProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditAdminProfile(AdminProfileEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("AdminEditProfile", model);
            }

            var user = await GetUserAsync(model.User.Id, true);
            if (user == null || user.Administrator == null)
            {
                return NotFound("Admin user not found.");
            }

            // Update phone number (if needed)
            var phoneResult = await UpdatePhoneNumberIfChanged(user, model.PhoneNumber, "AdminEditProfile", model);
            if (phoneResult != null) return phoneResult;

            // Update admin details
            user.Administrator.FirstName = model.FirstName;
            user.Administrator.LastName = model.LastName;
            user.Administrator.Address = model.Address;

            // For admins, password confirmation is optional.
            var passwordResult = await ValidatePassword(user, model.Password, false, "AdminEditProfile", model);
            if (passwordResult != null) return passwordResult;

            TempData["SuccessMessage"] = "Profile updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("ViewProfile", new { userId = user.Id });
        }

        // POST: /Profile/EditStaffProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStaffProfile(StaffProfileEditViewModel model)
        {
            RemovePasswordValidationForAdmin(ModelState);
            if (!ModelState.IsValid)
            {
                return View("StaffEditProfile", model);
            }

            var user = await GetUserAsync(model.User.Id, true);
            if (user == null || user.Staff == null)
            {
                return NotFound("Staff user not found.");
            }

            // For non-admins, password is required.
            var passwordResult = await ValidatePassword(user, model.Password, !User.IsInRole("admin"), "StaffEditProfile", model);
            if (passwordResult != null) return passwordResult;

            var phoneResult = await UpdatePhoneNumberIfChanged(user, model.PhoneNumber, "StaffEditProfile", model);
            if (phoneResult != null) return phoneResult;

            // Update staff details.
            user.Staff.FirstName = model.FirstName;
            user.Staff.LastName = model.LastName;
            user.Staff.Department = model.Department;
            user.Staff.Position = model.Position;
            user.Staff.Address = model.Address;

            TempData["SuccessMessage"] = "Profile updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("ViewProfile", new { userId = user.Id });
        }

        // POST: /Profile/EditHomeownerProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHomeownerProfile(HomeownerProfileEditViewModel model)
        {
            RemovePasswordValidationForAdmin(ModelState);
            if (!ModelState.IsValid)
            {
                return View("HomeownerEditProfile", model);
            }

            var user = await GetUserAsync(model.User.Id, true);
            if (user == null || user.Homeowner == null)
            {
                return NotFound("Homeowner user not found.");
            }

            // For non-admins, require validating the password.
            var passwordResult = await ValidatePassword(user, model.Password, !User.IsInRole("admin"), "HomeownerEditProfile", model);
            if (passwordResult != null) return passwordResult;

            var phoneResult = await UpdatePhoneNumberIfChanged(user, model.PhoneNumber, "HomeownerEditProfile", model);
            if (phoneResult != null) return phoneResult;

            // Update homeowner details.
            user.Homeowner.FirstName = model.FirstName;
            user.Homeowner.LastName = model.LastName;
            user.Homeowner.BlockNumber = model.BlockNumber;
            user.Homeowner.HouseNumber = model.HouseNumber;
            user.Homeowner.Address = model.Address;
            user.Homeowner.MoveInDate = model.MoveInDate;
            user.Homeowner.TypeOfResidency = model.TypeOfResidency;

            TempData["SuccessMessage"] = "Profile updated successfully.";
            await _context.SaveChangesAsync();
            return RedirectToAction("ViewProfile", new { userId = user.Id });
        }

        // GET: /Profile/ChangePassword/{userId?}
        public async Task<IActionResult> ChangePassword(string userId = null)
        {
            var user = await GetUserAsync(userId, true);
            if (user == null)
            {
                return Forbid();
            }

            var model = new ChangePasswordViewModel
            {
                UserId = user.Id
            };

            return View(model);
        }

        // POST: /Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await GetUserAsync(model.UserId, true);
            if (user == null)
            {
                return Forbid();
            }

            // Verify current password.
            var isCurrentPasswordValid = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
            if (!isCurrentPasswordValid)
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(model);
            }

            // Change password.
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Password changed successfully.";
                return RedirectToAction("ViewProfile", new { userId = user.Id });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }
    }
}