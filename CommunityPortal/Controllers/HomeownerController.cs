using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Homeowners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeownerController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IWebHostEnvironment webHostEnvironment, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            var homeowner = _context.Homeowners.FirstOrDefault(h => h.UserId == user.Id);

            var model = new HomeownerSettingsViewModel
            {
                FirstName = homeowner.FirstName,
                LastName = homeowner.LastName,
                BlockNumber = homeowner?.BlockNumber ?? 0,
                HouseNumber = homeowner?.HouseNumber ?? 0,
                Address = homeowner.Address,
                ProfilePicturePath = homeowner.ProfilePicturePath // Populate the ProfilePicturePath
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "homeowners")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(HomeownerSettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound("User not found.");
                }
                var homeowner = _context.Homeowners.FirstOrDefault(h => h.UserId == user.Id);
                if (homeowner == null)
                {
                    return NotFound("Homeowner profile not found.");
                }

                if (homeowner != null)
                {
                    // Update profile information
                    homeowner.FirstName = model.FirstName;
                    homeowner.LastName = model.LastName;
                    homeowner.BlockNumber = model.BlockNumber;
                    homeowner.HouseNumber = model.HouseNumber;
                    homeowner.Address = model.Address;

                    // Handle Password Change
                    if (!string.IsNullOrEmpty(model.CurrentPassword) || !string.IsNullOrEmpty(model.NewPassword))
                    {
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

                    // Handle Profile Picture Upload
                    if (model.ProfilePicture != null)
                    {
                        // Define upload path
                        string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");

                        // Ensure directory exists
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        // Generate unique filename
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfilePicture.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Save the file
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.ProfilePicture.CopyToAsync(fileStream);
                        }

                        // Save file path in database
                        homeowner.ProfilePicturePath = "/uploads/" + uniqueFileName;
                        Console.WriteLine("Profile picture saved at: " + homeowner.ProfilePicturePath);
                    }

                    // Save the changes to the database
                    await _context.SaveChangesAsync();

                    // Retrieve the updated model for passing to the view
                    model.ProfilePicturePath = homeowner.ProfilePicturePath; // Ensure the updated profile picture path is set

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return View(model); // Return the model with the updated data
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update profile. Please check your inputs.";
                }
            }

            return View(model);
        }
    }
}
