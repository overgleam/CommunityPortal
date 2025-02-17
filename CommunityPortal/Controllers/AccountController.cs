using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Account;
using CommunityPortal.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace CommunityPortal.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
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
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = UserStatus.PendingApproval
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "homeowners");

                    var homeowner = new Homeowner
                    {
                        UserId = user.Id,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        BlockNumber = model.BlockNumber,
                        HouseNumber = model.HouseNumber,
                        Address = model.Address,
                        MoveInDate = model.MoveInDate,
                        TypeOfResidency = model.TypeOfResidency
                    };

                    _context.Homeowners.Add(homeowner);
                    await _context.SaveChangesAsync();

                    return RedirectToAction("Login", "Account");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

            }
            ModelState.AddModelError(string.Empty, "Registration failed. Please check your inputs.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    switch(user.Status)
                    {
                        case UserStatus.PendingApproval:
                            ModelState.AddModelError(string.Empty, "Your account is awaiting approval by an administrator.");
                            return View();
                        case UserStatus.Banned:
                            ModelState.AddModelError(string.Empty, "Your account has been banned.");
                            return View();
                        case UserStatus.Disabled:
                            ModelState.AddModelError(string.Empty, "Your account is disabled.");
                            return View();
                        case UserStatus.Approved:
                            // Proceed with login
                            break;
                        default:
                            ModelState.AddModelError(string.Empty, "Invalid account status.");
                            return View();
                    }
                }

                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    if (await _userManager.IsInRoleAsync(user, "admin") || await _userManager.IsInRoleAsync(user, "staff"))
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "homeowners"))
                    {
                        return RedirectToAction("Index", "Homeowner");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }

                if (result.IsLockedOut)
                {
                    return View("Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

    }
}
