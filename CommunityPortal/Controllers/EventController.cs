using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models.Event;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class EventController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const string EventImagesFolder = "event-images";

        public EventController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Event
        public IActionResult Index()
        {
            return View();
        }

        // GET: Event/Create
        [Authorize(Roles = "admin")]
        public IActionResult Create()
        {
            var model = new EventViewModel
            {
                StartDateTime = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 1),
                EndDateTime = DateTime.Now.Date.AddHours(DateTime.Now.Hour + 2)
            };
            return View(model);
        }

        // POST: Event/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create(EventViewModel model)
        {
            if (ModelState.IsValid)
            {
                string imageUrl = await SaveEventImage(model.ImageFile);

                var @event = new Event
                {
                    Title = model.Title,
                    Description = model.Description,
                    StartDateTime = model.StartDateTime,
                    EndDateTime = model.EndDateTime,
                    Location = model.Location,
                    MaxAttendees = model.MaxAttendees,
                    RequiresRegistration = model.RequiresRegistration,
                    RegistrationInstructions = model.RegistrationInstructions,
                    Status = model.Status,
                    IsHighPriority = model.IsHighPriority,
                    ImageUrl = imageUrl,
                    CreatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Events.Add(@event);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Event created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Event/Edit/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .Include(e => e.Creator)
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

            if (@event == null)
            {
                return NotFound();
            }

            var viewModel = new EventViewModel
            {
                Id = @event.Id,
                Title = @event.Title,
                Description = @event.Description,
                StartDateTime = @event.StartDateTime,
                EndDateTime = @event.EndDateTime,
                Location = @event.Location,
                MaxAttendees = @event.MaxAttendees,
                RequiresRegistration = @event.RequiresRegistration,
                RegistrationInstructions = @event.RegistrationInstructions,
                Status = @event.Status,
                CancellationReason = @event.CancellationReason,
                IsHighPriority = @event.IsHighPriority,
                ExistingImageUrl = @event.ImageUrl
            };

            return View(viewModel);
        }

        // POST: Event/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(int id, EventViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var @event = await _context.Events.FindAsync(id);
                    if (@event == null || @event.IsDeleted)
                    {
                        return NotFound();
                    }

                    // Handle image update
                    string imageUrl = @event.ImageUrl;
                    if (model.ImageFile != null)
                    {
                        // Delete old image if it exists
                        if (!string.IsNullOrEmpty(@event.ImageUrl))
                        {
                            DeleteEventImage(@event.ImageUrl);
                        }
                        imageUrl = await SaveEventImage(model.ImageFile);
                    }

                    @event.Title = model.Title;
                    @event.Description = model.Description;
                    @event.StartDateTime = model.StartDateTime;
                    @event.EndDateTime = model.EndDateTime;
                    @event.Location = model.Location;
                    @event.MaxAttendees = model.MaxAttendees;
                    @event.RequiresRegistration = model.RequiresRegistration;
                    @event.RegistrationInstructions = model.RegistrationInstructions;
                    @event.Status = model.Status;
                    @event.CancellationReason = model.CancellationReason;
                    @event.IsHighPriority = model.IsHighPriority;
                    @event.ImageUrl = imageUrl;
                    @event.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Event updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(id))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }
            return View(model);
        }

        // POST: Event/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            try
            {
                @event.IsDeleted = true;
                @event.DeletedAt = DateTime.UtcNow;

                // Delete the event image if it exists
                if (!string.IsNullOrEmpty(@event.ImageUrl))
                {
                    DeleteEventImage(@event.ImageUrl);
                }

                await _context.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = true, message = "Event deleted successfully!" });
                }

                TempData["SuccessMessage"] = "Event deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Error deleting event." });
                }

                TempData["ErrorMessage"] = "Error deleting event.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Event/GetEvents
        [HttpGet]
        public async Task<JsonResult> GetEvents(DateTime? start, DateTime? end)
        {
            var events = await _context.Events
                .Include(e => e.Creator)
                .Where(e => !e.IsDeleted)
                .Select(e => new
                {
                    id = e.Id,
                    title = e.Title,
                    description = e.Description,
                    start = e.StartDateTime.ToString("o"),
                    end = e.EndDateTime.ToString("o"),
                    location = e.Location,
                    maxAttendees = e.MaxAttendees,
                    requiresRegistration = e.RequiresRegistration,
                    registrationInstructions = e.RegistrationInstructions,
                    status = e.Status.ToString(),
                    statusText = e.StatusText,
                    statusBadgeClass = e.StatusBadgeClass,
                    isHighPriority = e.IsHighPriority,
                    imageUrl = e.ImageUrl,
                    createdBy = e.Creator.UserName,
                    backgroundColor = e.CalendarEventColor,
                    borderColor = e.CalendarEventColor,
                    textColor = e.Status == EventStatus.Postponed ? "#000" : "#fff",
                    className = e.IsHighPriority ? "high-priority-event" : ""
                })
                .ToListAsync();

            return Json(events);
        }

        private async Task<string> SaveEventImage(IFormFile imageFile)
        {
            if (imageFile == null) return null;

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, EventImagesFolder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(imageFile.FileName)}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return $"/{EventImagesFolder}/{uniqueFileName}";
        }

        private void DeleteEventImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return;

            string fileName = Path.GetFileName(imageUrl);
            string filePath = Path.Combine(_webHostEnvironment.WebRootPath, EventImagesFolder, fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id && !e.IsDeleted);
        }
    }
} 