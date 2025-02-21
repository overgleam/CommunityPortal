using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models;
using CommunityPortal.Data;
using Microsoft.AspNetCore.Identity;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class ServiceRequestController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ServiceRequestController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: ServiceRequest
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var userRoles = await _userManager.GetRolesAsync(currentUser);
            
            // Temporary debug information
            ViewBag.UserRoles = string.Join(", ", userRoles);
            ViewBag.IsAuthenticated = User.Identity.IsAuthenticated;
            ViewBag.UserId = currentUser?.Id;
            
            var requests = new List<ServiceRequest>();

            if (User.IsInRole("admin"))
            {
                requests = await _context.ServiceRequests
                    .Include(s => s.ServiceCategory)
                    .Include(s => s.Homeowner)
                    .Include(s => s.AssignedStaff)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
            }
            else if (User.IsInRole("staff"))
            {
                requests = await _context.ServiceRequests
                    .Include(s => s.ServiceCategory)
                    .Include(s => s.Homeowner)
                    .Where(s => s.AssignedStaffId == currentUser.Id)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
            }
            else // homeowner
            {
                requests = await _context.ServiceRequests
                    .Include(s => s.ServiceCategory)
                    .Include(s => s.AssignedStaff)
                    .Where(s => s.HomeownerId == currentUser.Id)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
            }

            return View(requests);
        }

        // GET: ServiceRequest/Create
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
            return View();
        }

        // POST: ServiceRequest/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Create(ServiceRequest serviceRequest)
        {
            try
            {
                // Remove validation errors for navigation properties
                ModelState.Remove("Homeowner");
                ModelState.Remove("ServiceCategory");
                ModelState.Remove("HomeownerId");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = string.Join(", ", errors);
                    ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                    return View(serviceRequest);
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    ModelState.AddModelError("", "User not found.");
                    ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                    return View(serviceRequest);
                }

                // Set the required properties
                serviceRequest.HomeownerId = currentUser.Id;
                serviceRequest.Status = ServiceRequestStatus.Pending;
                serviceRequest.CreatedAt = DateTime.UtcNow;

                // Verify the service category exists
                var categoryExists = await _context.ServiceCategories.AnyAsync(c => c.Id == serviceRequest.ServiceCategoryId);
                if (!categoryExists)
                {
                    ModelState.AddModelError("ServiceCategoryId", "Invalid service category selected.");
                    ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                    return View(serviceRequest);
                }

                _context.Add(serviceRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Service request created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating service request: {ex.Message}");
                ViewBag.Categories = await _context.ServiceCategories.ToListAsync();
                return View(serviceRequest);
            }
        }

        // POST: ServiceRequest/AssignStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AssignStaff(int requestId, string staffId)
        {
            var request = await _context.ServiceRequests.FindAsync(requestId);
            if (request == null)
            {
                return NotFound();
            }

            request.AssignedStaffId = staffId;
            request.Status = ServiceRequestStatus.Assigned;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: ServiceRequest/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> UpdateStatus(int requestId, ServiceRequestStatus status, string? rejectionReason = null)
        {
            var request = await _context.ServiceRequests.FindAsync(requestId);
            if (request == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (request.AssignedStaffId != currentUser.Id)
            {
                return Forbid();
            }

            request.Status = status;
            if (status == ServiceRequestStatus.Rejected)
            {
                request.RejectionReason = rejectionReason;
            }
            else if (status == ServiceRequestStatus.Completed)
            {
                request.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: ServiceRequest/SubmitFeedback/5
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> SubmitFeedback(int id)
        {
            try
            {
                var request = await _context.ServiceRequests
                    .Include(s => s.Feedback)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (request == null)
                {
                    TempData["ErrorMessage"] = "Service request not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (request.Status != ServiceRequestStatus.Completed)
                {
                    TempData["ErrorMessage"] = "Feedback can only be submitted for completed service requests.";
                    return RedirectToAction(nameof(Index));
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (request.HomeownerId != currentUser.Id)
                {
                    TempData["ErrorMessage"] = "You can only submit feedback for your own service requests.";
                    return RedirectToAction(nameof(Index));
                }

                if (request.Feedback != null)
                {
                    TempData["ErrorMessage"] = "Feedback has already been submitted for this request.";
                    return RedirectToAction(nameof(Index));
                }

                return View(new ServiceFeedback { ServiceRequestId = id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error loading feedback form: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: ServiceRequest/SubmitFeedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> SubmitFeedback(ServiceFeedback feedback)
        {
            try
            {
                // Remove validation errors for navigation properties
                ModelState.Remove("Homeowner");
                ModelState.Remove("ServiceRequest");
                ModelState.Remove("HomeownerId");
                ModelState.Remove("Id");

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    TempData["ErrorMessage"] = string.Join(", ", errors);
                    return View(feedback);
                }

                var request = await _context.ServiceRequests
                    .Include(s => s.Feedback)
                    .FirstOrDefaultAsync(s => s.Id == feedback.ServiceRequestId);

                if (request == null)
                {
                    ModelState.AddModelError("", "Service request not found.");
                    return View(feedback);
                }

                if (request.Status != ServiceRequestStatus.Completed)
                {
                    ModelState.AddModelError("", "Feedback can only be submitted for completed service requests.");
                    return View(feedback);
                }

                var currentUser = await _userManager.GetUserAsync(User);
                if (request.HomeownerId != currentUser.Id)
                {
                    ModelState.AddModelError("", "You can only submit feedback for your own service requests.");
                    return View(feedback);
                }

                if (request.Feedback != null)
                {
                    ModelState.AddModelError("", "Feedback has already been submitted for this request.");
                    return View(feedback);
                }

                // Create new feedback object to avoid any potential Id issues
                var newFeedback = new ServiceFeedback
                {
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    ServiceRequestId = feedback.ServiceRequestId,
                    HomeownerId = currentUser.Id,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Add(newFeedback);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Thank you for your feedback!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error submitting feedback: {ex.Message}");
                return View(feedback);
            }
        }

        // GET: ServiceRequest/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.ServiceRequests
                .Include(s => s.ServiceCategory)
                .Include(s => s.Homeowner)
                .Include(s => s.AssignedStaff)
                .Include(s => s.Feedback)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("admin") && 
                request.HomeownerId != currentUser.Id && 
                request.AssignedStaffId != currentUser.Id)
            {
                return Forbid();
            }

            if (User.IsInRole("admin"))
            {
                ViewBag.StaffMembers = await _userManager.GetUsersInRoleAsync("staff");
            }

            return View(request);
        }
    }
}
