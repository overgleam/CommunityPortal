using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CommunityPortal.Data;
using CommunityPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FeedbackController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // GET: Feedback
        [Authorize(Roles = "admin")] // Only admins can view feedback
        public IActionResult Index()
        {
            var feedbacks = _context.Feedbacks.Include(f => f.User).ToList();
            return View(feedbacks);
        }

        // GET: Feedback/Create
        [Authorize(Roles = "homeowners,staff")]
        public async Task<IActionResult> Create()
        {
            // Retrieve the current user
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            // Query the database for the current user's previous feedbacks,
            // ordering by submission date (descending).
            var userFeedback = await _context.Feedbacks
                                    .Where(f => f.UserId == user.Id)
                                    .OrderByDescending(f => f.CreatedAt)
                                    .ToListAsync();

            // Populate the ViewBag with the feedback history for the view.
            ViewBag.UserFeedback = userFeedback;

            return View();
        }

        // POST: Feedback/Create
        [HttpPost]
        [Authorize(Roles = "homeowners,staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ModelState.AddModelError("", "Feedback message cannot be empty.");
                return View();
            }

            var user = await _userManager.GetUserAsync(User);
            var feedback = new Feedback
            {
                UserId = user.Id,
                Message = message,
                CreatedAt = DateTime.Now,
                Status = FeedbackStatus.New // Default status
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            // Store submission success in TempData
            TempData["FeedbackSubmitted"] = true;

            return RedirectToAction("Create");
        }


        // POST: Feedback/UpdateStatus
        [HttpPost]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateStatus(int id, FeedbackStatus status)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return NotFound();
            }
            feedback.Status = status;
            _context.Update(feedback);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        //Feedback/Delete
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return NotFound();
            }

            // Check if the current user owns this feedback or is an admin
            var currentUser = await _userManager.GetUserAsync(User);
            if (feedback.UserId != currentUser.Id && !User.IsInRole("admin"))
            {
                return Forbid();
            }

            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            return Ok();
        }

    }
}