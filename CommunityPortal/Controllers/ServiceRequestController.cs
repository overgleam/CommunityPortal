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
                    .Include(s => s.StaffAssignments)
                        .ThenInclude(sa => sa.Staff)
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
            }
            else if (User.IsInRole("staff"))
            {
                requests = await _context.ServiceRequests
                    .Include(s => s.ServiceCategory)
                    .Include(s => s.Homeowner)
                    .Include(s => s.StaffAssignments)
                    .Where(s => s.StaffAssignments.Any(sa => sa.StaffId == currentUser.Id))
                    .OrderByDescending(s => s.CreatedAt)
                    .ToListAsync();
            }
            else // homeowner
            {
                requests = await _context.ServiceRequests
                    .Include(s => s.ServiceCategory)
                    .Include(s => s.StaffAssignments)
                        .ThenInclude(sa => sa.Staff)
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
        public async Task<IActionResult> AssignStaff(int requestId, List<string> staffIds)
        {
            var request = await _context.ServiceRequests
                .Include(s => s.StaffAssignments)
                .FirstOrDefaultAsync(s => s.Id == requestId);

            if (request == null)
            {
                return NotFound();
            }

            // Remove any unaccepted assignments
            var unacceptedAssignments = request.StaffAssignments
                .Where(sa => !sa.IsAccepted)
                .ToList();

            foreach (var assignment in unacceptedAssignments)
            {
                _context.ServiceStaffAssignments.Remove(assignment);
            }

            // Add new assignments
            foreach (var staffId in staffIds)
            {
                if (!request.StaffAssignments.Any(sa => sa.StaffId == staffId))
                {
                    request.StaffAssignments.Add(new ServiceStaffAssignment
                    {
                        ServiceRequestId = requestId,
                        StaffId = staffId,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }

            request.Status = ServiceRequestStatus.Assigned;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // POST: ServiceRequest/RemoveStaffAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RemoveStaffAssignment(int requestId, string staffId)
        {
            var assignment = await _context.ServiceStaffAssignments
                .FirstOrDefaultAsync(sa => sa.ServiceRequestId == requestId && sa.StaffId == staffId);

            if (assignment == null)
            {
                return NotFound();
            }

            _context.ServiceStaffAssignments.Remove(assignment);

            var request = await _context.ServiceRequests
                .Include(s => s.StaffAssignments)
                .FirstOrDefaultAsync(s => s.Id == requestId);

            // If no staff members are assigned or accepted, set status back to Pending
            if (!request.StaffAssignments.Any(sa => sa.StaffId != staffId) || 
                !request.StaffAssignments.Any(sa => sa.StaffId != staffId && sa.IsAccepted))
            {
                request.Status = ServiceRequestStatus.Pending;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Staff member removed successfully.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // POST: ServiceRequest/AcceptAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "staff")]
        public async Task<IActionResult> AcceptAssignment(int requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var assignment = await _context.ServiceStaffAssignments
                .FirstOrDefaultAsync(sa => sa.ServiceRequestId == requestId && sa.StaffId == currentUser.Id);

            if (assignment == null)
            {
                return NotFound();
            }

            assignment.IsAccepted = true;
            assignment.AcceptedAt = DateTime.UtcNow;

            var request = await _context.ServiceRequests.FindAsync(requestId);
            request.Status = ServiceRequestStatus.InProgress;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // POST: ServiceRequest/MarkUnavailable
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> MarkUnavailable(int requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound("User not found.");
            }

            var assignment = await _context.ServiceStaffAssignments
                .FirstOrDefaultAsync(sa => sa.ServiceRequestId == requestId && sa.StaffId == currentUser.Id);

            if (assignment == null)
            {
                TempData["ErrorMessage"] = "You are not assigned to this service request.";
                return RedirectToAction(nameof(Details), new { id = requestId });
            }

            if (assignment.IsAccepted)
            {
                TempData["ErrorMessage"] = "Cannot mark as unavailable after accepting the request.";
                return RedirectToAction(nameof(Details), new { id = requestId });
            }

            assignment.IsUnavailable = true;

            var request = await _context.ServiceRequests
                .Include(s => s.StaffAssignments)
                .FirstOrDefaultAsync(s => s.Id == requestId);

            if (request == null)
            {
                return NotFound("Service request not found.");
            }

            // If all assigned staff members are unavailable, set status back to Pending
            if (!request.StaffAssignments.Any(sa => sa.StaffId != currentUser.Id && !sa.IsUnavailable))
            {
                request.Status = ServiceRequestStatus.Pending;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "You have been marked as unavailable for this request.";
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // POST: ServiceRequest/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> UpdateStatus(int requestId, ServiceRequestStatus status, string? rejectionReason = null)
        {
            var request = await _context.ServiceRequests
                .Include(s => s.StaffAssignments)
                .FirstOrDefaultAsync(s => s.Id == requestId);

            if (request == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("admin") && !request.StaffAssignments.Any(sa => sa.StaffId == currentUser.Id && sa.IsAccepted))
            {
                return Forbid();
            }

            request.Status = status;
            if (status == ServiceRequestStatus.Rejected)
            {
                request.RejectionReason = rejectionReason;
                request.RejectedAt = DateTime.UtcNow;
            }
            else if (status == ServiceRequestStatus.Completed)
            {
                request.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = requestId });
        }

        // POST: ServiceRequest/CancelRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> CancelRequest(int requestId, string cancellationReason)
        {
            var request = await _context.ServiceRequests.FindAsync(requestId);
            if (request == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (request.HomeownerId != currentUser.Id)
            {
                return Forbid();
            }

            if (request.Status != ServiceRequestStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending requests can be cancelled.";
                return RedirectToAction(nameof(Details), new { id = requestId });
            }

            request.Status = ServiceRequestStatus.Cancelled;
            request.CancellationReason = cancellationReason;
            request.CancelledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Service request cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: ServiceRequest/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var request = await _context.ServiceRequests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            _context.ServiceRequests.Remove(request);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Service request deleted successfully.";
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
                .Include(s => s.StaffAssignments)
                    .ThenInclude(sa => sa.Staff)
                .Include(s => s.Feedback)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("admin") && 
                request.HomeownerId != currentUser.Id && 
                !request.StaffAssignments.Any(sa => sa.StaffId == currentUser.Id))
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
