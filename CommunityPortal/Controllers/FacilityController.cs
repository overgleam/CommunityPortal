using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Facility;
using Microsoft.AspNetCore.Identity;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class FacilityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public FacilityController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: /Facility
        public async Task<IActionResult> Index(string searchString, FacilityType? type, DateTime? date)
        {
            var query = _context.Facilities
                .Include(f => f.BlackoutDates)
                .Include(f => f.Reservations)
                .Where(f => !f.IsDeleted);

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(f => f.Name.Contains(searchString) || f.Description.Contains(searchString));
            }

            if (type.HasValue)
            {
                query = query.Where(f => f.Type == type.Value);
            }

            var facilities = await query.ToListAsync();

            if (date.HasValue)
            {
                facilities = facilities.Where(f => 
                    !f.BlackoutDates.Any(bd => date.Value >= bd.StartDate && date.Value <= bd.EndDate) &&
                    !f.Reservations.Any(r => r.ReservationDate.Date == date.Value.Date &&
                                           r.Status == ReservationStatus.Approved))
                    .ToList();
            }

            ViewBag.Types = Enum.GetValues(typeof(FacilityType)).Cast<FacilityType>();
            return View(facilities);
        }

        // GET: /Facility/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var facility = await _context.Facilities
                .Include(f => f.BlackoutDates)
                .Include(f => f.Reservations)
                .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);

            if (facility == null)
            {
                return NotFound();
            }

            return View(facility);
        }

        // GET: /Facility/MyReservations
        public async Task<IActionResult> MyReservations()
        {
            var user = await _userManager.GetUserAsync(User);
            var reservations = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Where(r => r.UserId == user.Id && !r.IsDeleted)
                .OrderByDescending(r => r.ReservationDate)
                .ToListAsync();

            return View(reservations);
        }

        // POST: /Facility/Reserve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(FacilityReservation reservation)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                reservation.UserId = user.Id;
                reservation.CreatedAt = DateTime.UtcNow;
                reservation.Status = ReservationStatus.Pending;

                // Check for conflicts
                var hasConflict = await _context.FacilityReservations
                    .AnyAsync(r => r.FacilityId == reservation.FacilityId &&
                                 r.ReservationDate.Date == reservation.ReservationDate.Date &&
                                 r.Status == ReservationStatus.Approved &&
                                 ((r.StartTime <= reservation.StartTime && reservation.StartTime < r.EndTime) ||
                                  (r.StartTime < reservation.EndTime && reservation.EndTime <= r.EndTime)));

                if (hasConflict)
                {
                    ModelState.AddModelError("", "The selected time slot is already booked.");
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }

                var hasBlackout = await _context.BlackoutDates
                    .AnyAsync(bd => bd.FacilityId == reservation.FacilityId &&
                                  reservation.ReservationDate.Date >= bd.StartDate.Date &&
                                  reservation.ReservationDate.Date <= bd.EndDate.Date);

                if (hasBlackout)
                {
                    ModelState.AddModelError("", "The facility is not available on the selected date due to maintenance or special event.");
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }

                _context.Add(reservation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MyReservations));
            }

            return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
        }

        // POST: /Facility/CancelReservation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var reservation = await _context.FacilityReservations
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status != ReservationStatus.Pending && 
                reservation.Status != ReservationStatus.Approved)
            {
                return BadRequest("Cannot cancel this reservation.");
            }

            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyReservations));
        }

        #region Admin Actions

        // GET: /Facility/Manage
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Manage()
        {
            var facilities = await _context.Facilities
                .Include(f => f.BlackoutDates)
                .Include(f => f.Reservations)
                .Where(f => !f.IsDeleted)
                .ToListAsync();

            return View(facilities);
        }

        // GET: /Facility/Create
        [Authorize(Roles = "admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Facility/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Create(Facility facility, IFormFile? image)
        {
            if (ModelState.IsValid)
            {
                if (image != null && image.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "facilities");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }

                    facility.ImageUrl = "/uploads/facilities/" + uniqueFileName;
                }

                facility.CreatedAt = DateTime.UtcNow;
                _context.Add(facility);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Manage));
            }
            return View(facility);
        }

        // GET: /Facility/Edit/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var facility = await _context.Facilities.FindAsync(id);
            if (facility == null || facility.IsDeleted)
            {
                return NotFound();
            }

            return View(facility);
        }

        // POST: /Facility/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Edit(int id, Facility facility, IFormFile? image)
        {
            if (id != facility.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingFacility = await _context.Facilities.FindAsync(id);
                    if (existingFacility == null || existingFacility.IsDeleted)
                    {
                        return NotFound();
                    }

                    if (image != null && image.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "facilities");
                        Directory.CreateDirectory(uploadsFolder);
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + image.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await image.CopyToAsync(fileStream);
                        }

                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(existingFacility.ImageUrl))
                        {
                            var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, 
                                existingFacility.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        facility.ImageUrl = "/uploads/facilities/" + uniqueFileName;
                    }
                    else
                    {
                        facility.ImageUrl = existingFacility.ImageUrl;
                    }

                    facility.UpdatedAt = DateTime.UtcNow;
                    _context.Entry(existingFacility).CurrentValues.SetValues(facility);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FacilityExists(facility.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Manage));
            }
            return View(facility);
        }

        // POST: /Facility/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var facility = await _context.Facilities.FindAsync(id);
            if (facility == null)
            {
                return NotFound();
            }

            facility.IsDeleted = true;
            facility.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }

        // GET: /Facility/ManageReservations
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> ManageReservations()
        {
            var reservations = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Include(r => r.User)
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reservations);
        }

        // POST: /Facility/UpdateReservationStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateReservationStatus(int id, ReservationStatus status, string? rejectionReason)
        {
            var reservation = await _context.FacilityReservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            if (status == ReservationStatus.Rejected && string.IsNullOrEmpty(rejectionReason))
            {
                ModelState.AddModelError("", "Rejection reason is required.");
                return RedirectToAction(nameof(ManageReservations));
            }

            reservation.Status = status;
            reservation.RejectionReason = rejectionReason;
            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ManageReservations));
        }

        // POST: /Facility/AddBlackoutDate
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AddBlackoutDate(BlackoutDate blackoutDate)
        {
            if (ModelState.IsValid)
            {
                _context.Add(blackoutDate);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Manage));
        }

        // POST: /Facility/RemoveBlackoutDate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RemoveBlackoutDate(int id)
        {
            var blackoutDate = await _context.BlackoutDates.FindAsync(id);
            if (blackoutDate != null)
            {
                _context.BlackoutDates.Remove(blackoutDate);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Manage));
        }

        private bool FacilityExists(int id)
        {
            return _context.Facilities.Any(e => e.Id == id);
        }

        #endregion
    }
}
