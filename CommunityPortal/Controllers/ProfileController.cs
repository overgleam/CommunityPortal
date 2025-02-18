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

        private async Task<ApplicationUser> GetUserAsync(string userId, bool isEditMode = false)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return await _userManager.GetUserAsync(User);
            }

            // For edit operations, check permissions
            if (isEditMode)
            {
                // Admins can edit all profiles
                if (User.IsInRole("admin"))
                {
                    return await _userManager.Users
                        .Include(u => u.Administrator)
                        .Include(u => u.Staff)
                        .Include(u => u.Homeowner)
                        .FirstOrDefaultAsync(u => u.Id == userId);
                }

                // Staff and Homeowners can only edit their own profiles
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
            var user = await _userManager.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == (userId ?? _userManager.GetUserId(User)));

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

        private void RemovePasswordValidationForAdmin(ModelStateDictionary modelState)
        {
            if (User.IsInRole("admin"))
            {
                modelState.Remove("Password");
                modelState.Remove("ConfirmPassword");
            }
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

            if (user.PhoneNumber != model.PhoneNumber)
            {
                var existingUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);

                if (existingUser != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This phone number is already in use.");
                    return View("AdminEditProfile", model);
                }

                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    ModelState.AddModelError("PhoneNumber", "Error updating phone number");
                    return View("AdminEditProfile", model);
                }
            }

            user.Administrator.FirstName = model.FirstName;
            user.Administrator.LastName = model.LastName;
            user.Administrator.Address = model.Address;

            if (!string.IsNullOrEmpty(model.Password) || !string.IsNullOrEmpty(model.ConfirmPassword))
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("Password", "Incorrect password.");
                    TempData["ErrorMessage"] = "Incorrect password.";
                    return View("AdminEditProfile", model);
                }
            }
            else
            {
                TempData["SuccessMessage"] = "Profile updated successfully.";
            }

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

            // Only check password if not admin
            if (!User.IsInRole("admin"))
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("Password", "Incorrect password.");
                    return View("StaffEditProfile", model);
                }
            }

            if (user.PhoneNumber != model.PhoneNumber)
            {
                var existingUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);

                if (existingUser != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This phone number is already in use.");
                    return View("StaffEditProfile", model);
                }

                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    ModelState.AddModelError("PhoneNumber", "Error updating phone number");
                    return View("StaffEditProfile", model);
                }
            }

            user.Staff.FirstName = model.FirstName;
            user.Staff.LastName = model.LastName;
            user.Staff.Department = model.Department;
            user.Staff.Position = model.Position;
            user.Staff.Address = model.Address;

            if (!string.IsNullOrEmpty(model.Password) || !string.IsNullOrEmpty(model.ConfirmPassword))
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("Password", "Incorrect password.");
                    TempData["ErrorMessage"] = "Incorrect password.";
                    return View("StaffEditProfile", model);
                }
            }
            else
            {
                TempData["SuccessMessage"] = "Profile updated successfully.";
            }

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

            // Only check password if not admin
            if (!User.IsInRole("admin"))
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("Password", "Incorrect password.");
                    return View("HomeownerEditProfile", model);
                }

            }

            if (user.PhoneNumber != model.PhoneNumber)
            {
                var existingUser = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.PhoneNumber == model.PhoneNumber);

                if (existingUser != null)
                {
                    ModelState.AddModelError("PhoneNumber", "This phone number is already in use.");
                    return View("HomeownerEditProfile", model);
                }

                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, model.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    ModelState.AddModelError("PhoneNumber", "Error updating phone number");
                    return View("HomeownerEditProfile", model);
                }
            }

            user.Homeowner.FirstName = model.FirstName;
            user.Homeowner.LastName = model.LastName;
            user.Homeowner.BlockNumber = model.BlockNumber;
            user.Homeowner.HouseNumber = model.HouseNumber;
            user.Homeowner.Address = model.Address;
            user.Homeowner.MoveInDate = model.MoveInDate;
            user.Homeowner.TypeOfResidency = model.TypeOfResidency;

            if (!string.IsNullOrEmpty(model.Password) || !string.IsNullOrEmpty(model.ConfirmPassword))
            {
                var isPasswordValid = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!isPasswordValid)
                {
                    ModelState.AddModelError("Password", "Incorrect password.");
                    TempData["ErrorMessage"] = "Incorrect password.";
                    return View("HomeownerEditProfile", model);
                }
            }
            else
            {
                TempData["SuccessMessage"] = "Profile updated successfully.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("ViewProfile", new { userId = user.Id });
        }
    }
}