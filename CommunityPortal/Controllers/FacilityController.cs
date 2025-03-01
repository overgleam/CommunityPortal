using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Facility;
using Microsoft.AspNetCore.Identity;
using CommunityPortal.Services;
using System.Security.Claims;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class FacilityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly NotificationService _notificationService;

        public FacilityController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
            _notificationService = notificationService;
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
            // Save form values in TempData to persist them in case of errors
            TempData["ReservationDate"] = reservation.ReservationDate.ToString("yyyy-MM-dd");
            TempData["StartTime"] = reservation.StartTime.ToString("HH:mm");
            TempData["EndTime"] = reservation.EndTime.ToString("HH:mm");
            TempData["GuestCount"] = reservation.GuestCount.ToString();
            TempData["SpecialRequests"] = reservation.SpecialRequests;
            
            if (ModelState.IsValid)
            {
                // Get the facility
                var facility = await _context.Facilities
                    .FirstOrDefaultAsync(f => f.Id == reservation.FacilityId && !f.IsDeleted);
                
                if (facility == null)
                {
                    return NotFound();
                }
                
                // Get the current user
                var user = await _userManager.GetUserAsync(User);
                reservation.UserId = user.Id;
                reservation.CreatedAt = DateTime.UtcNow;
                reservation.Status = ReservationStatus.Pending;
                
                // Set full datetime for start and end times by combining reservation date with time
                var startTimeOfDay = reservation.StartTime.TimeOfDay;
                var endTimeOfDay = reservation.EndTime.TimeOfDay;
                
                reservation.StartTime = reservation.ReservationDate.Date.Add(startTimeOfDay);
                reservation.EndTime = reservation.ReservationDate.Date.Add(endTimeOfDay);
                
                // Validate operating hours - compare only the TimeOfDay part
                if (startTimeOfDay < facility.OpeningTime.TimeOfDay || endTimeOfDay > facility.ClosingTime.TimeOfDay)
                {
                    ModelState.AddModelError("", "Reservation time must be within facility operating hours.");
                    TempData["ErrorMessage"] = "Reservation time must be within facility operating hours.";
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }
                
                // Validate end time is after start time
                if (endTimeOfDay <= startTimeOfDay)
                {
                    ModelState.AddModelError("", "End time must be after start time.");
                    TempData["ErrorMessage"] = "End time must be after start time.";
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }
                
                // Validate maximum occupancy
                if (reservation.GuestCount > facility.MaximumOccupancy)
                {
                    ModelState.AddModelError("", $"The number of guests exceeds the maximum occupancy ({facility.MaximumOccupancy}).");
                    TempData["ErrorMessage"] = $"The number of guests exceeds the maximum occupancy ({facility.MaximumOccupancy}).";
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }
                
                // Calculate total price based on duration and hourly rate
                var duration = (endTimeOfDay - startTimeOfDay).TotalHours;
                reservation.TotalPrice = (decimal)duration * facility.PricePerHour;

                // Check for conflicts with existing reservations (only Approved and PaymentPending)
                var conflictingReservation = await _context.FacilityReservations
                    .Where(r => r.FacilityId == reservation.FacilityId &&
                               r.ReservationDate.Date == reservation.ReservationDate.Date &&
                               (r.Status == ReservationStatus.Approved || 
                                r.Status == ReservationStatus.PaymentPending) &&
                               !r.IsDeleted &&
                               ((r.StartTime <= reservation.StartTime && reservation.StartTime < r.EndTime) ||
                                (r.StartTime < reservation.EndTime && reservation.EndTime <= r.EndTime) ||
                                (reservation.StartTime <= r.StartTime && r.StartTime < reservation.EndTime) ||
                                (reservation.StartTime < r.EndTime && r.EndTime <= reservation.EndTime)))
                    .FirstOrDefaultAsync();

                if (conflictingReservation != null)
                {
                    string conflictMessage = $"The selected time slot conflicts with an existing reservation from {conflictingReservation.StartTime.ToString("h:mm tt")} to {conflictingReservation.EndTime.ToString("h:mm tt")}. Please select a different time.";
                    ModelState.AddModelError("", conflictMessage);
                    TempData["ErrorMessage"] = conflictMessage;
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }

                var hasBlackout = await _context.BlackoutDates
                    .AnyAsync(bd => bd.FacilityId == reservation.FacilityId &&
                                  reservation.ReservationDate.Date >= bd.StartDate.Date &&
                                  reservation.ReservationDate.Date <= bd.EndDate.Date);

                if (hasBlackout)
                {
                    ModelState.AddModelError("", "The facility is not available on the selected date due to maintenance or special event.");
                    TempData["ErrorMessage"] = "The facility is not available on the selected date due to maintenance or special event.";
                    return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
                }

                _context.Add(reservation);
                await _context.SaveChangesAsync();
                
                // Send notification to all admins
                var adminUsers = await _userManager.GetUsersInRoleAsync("admin");
                foreach (var admin in adminUsers)
                {
                    await _notificationService.CreateFacilityReservationNotificationAsync(
                        recipientId: admin.Id,
                        reservationId: reservation.Id,
                        facilityName: facility.Name,
                        status: "Requested",
                        senderId: user.Id
                    );
                }
                
                // Clear the form values from TempData after successful submission
                TempData.Remove("ReservationDate");
                TempData.Remove("StartTime");
                TempData.Remove("EndTime");
                TempData.Remove("GuestCount");
                TempData.Remove("SpecialRequests");
                
                TempData["SuccessMessage"] = "Reservation created successfully. An administrator will review your request.";
                return RedirectToAction(nameof(MyReservations));
            }

            TempData["ErrorMessage"] = "Please fix the errors and try again.";
            return RedirectToAction(nameof(Details), new { id = reservation.FacilityId });
        }

        // POST: /Facility/CancelReservation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var reservation = await _context.FacilityReservations
                .Include(r => r.Facility)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);

            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found or you don't have permission to cancel it.";
                return RedirectToAction(nameof(MyReservations));
            }

            if (reservation.Status != ReservationStatus.Pending && 
                reservation.Status != ReservationStatus.Approved &&
                reservation.Status != ReservationStatus.PaymentPending)
            {
                TempData["ErrorMessage"] = $"Cannot cancel this reservation with status: {reservation.Status}.";
                return RedirectToAction(nameof(MyReservations));
            }

            // Special check for reservations that are happening soon (within 24 hours)
            var reservationStart = reservation.StartTime;
            if (reservationStart.Date == DateTime.Today && 
                (DateTime.Now > reservationStart.AddHours(-2)))
            {
                TempData["ErrorMessage"] = "Reservations cannot be cancelled less than 2 hours before the start time.";
                return RedirectToAction(nameof(MyReservations));
            }

            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Your reservation for {reservation.Facility.Name} on {reservation.ReservationDate.ToShortDateString()} has been cancelled successfully.";
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
                // Set current date for OpeningTime and ClosingTime but keep the time component
                var today = DateTime.Today;
                facility.OpeningTime = today.Add(facility.OpeningTime.TimeOfDay);
                facility.ClosingTime = today.Add(facility.ClosingTime.TimeOfDay);
                
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
                    
                    // Set current date for OpeningTime and ClosingTime but keep the time component
                    var today = DateTime.Today;
                    facility.OpeningTime = today.Add(facility.OpeningTime.TimeOfDay);
                    facility.ClosingTime = today.Add(facility.ClosingTime.TimeOfDay);
                    
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
                    .ThenInclude(u => u.Homeowner)
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
            var reservation = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (reservation == null)
            {
                return NotFound();
            }

            if (status == ReservationStatus.Rejected && string.IsNullOrEmpty(rejectionReason))
            {
                ModelState.AddModelError("", "Rejection reason is required.");
                return RedirectToAction(nameof(ManageReservations));
            }

            // Save old status to check if it changed
            var oldStatus = reservation.Status;

            // If approving a reservation, set it to PaymentPending
            if (status == ReservationStatus.Approved && reservation.Status == ReservationStatus.Pending)
            {
                status = ReservationStatus.PaymentPending;
                
                // Find and reject all conflicting reservations
                var conflictingReservations = await _context.FacilityReservations
                    .Where(r => r.Id != reservation.Id &&
                            r.FacilityId == reservation.FacilityId &&
                            r.ReservationDate.Date == reservation.ReservationDate.Date &&
                            r.Status == ReservationStatus.Pending &&
                            !r.IsDeleted &&
                            ((r.StartTime <= reservation.StartTime && reservation.StartTime < r.EndTime) ||
                             (r.StartTime < reservation.EndTime && reservation.EndTime <= r.EndTime) ||
                             (reservation.StartTime <= r.StartTime && r.StartTime < reservation.EndTime) ||
                             (reservation.StartTime < r.EndTime && r.EndTime <= reservation.EndTime)))
                    .ToListAsync();

                foreach (var conflictingReservation in conflictingReservations)
                {
                    conflictingReservation.Status = ReservationStatus.Rejected;
                    conflictingReservation.RejectionReason = "Another reservation for this facility and time was approved.";
                    
                    // Send notifications for rejected reservations
                    if (conflictingReservation.User != null)
                    {
                        await _notificationService.CreateFacilityReservationNotificationAsync(
                            recipientId: conflictingReservation.UserId,
                            reservationId: conflictingReservation.Id,
                            facilityName: reservation.Facility.Name,
                            status: "Rejected",
                            senderId: User.FindFirstValue(ClaimTypes.NameIdentifier)
                        );
                    }
                }
            }

            reservation.Status = status;
            
            if (status == ReservationStatus.Rejected)
            {
                reservation.RejectionReason = rejectionReason;
            }
            
            await _context.SaveChangesAsync();
            
            // Send notification to the user if status changed
            if (oldStatus != status && reservation.User != null)
            {
                string statusText = status.ToString();
                if (status == ReservationStatus.PaymentPending)
                    statusText = "Approved (Payment Required)";
                
                await _notificationService.CreateFacilityReservationNotificationAsync(
                    recipientId: reservation.UserId,
                    reservationId: reservation.Id,
                    facilityName: reservation.Facility.Name,
                    status: statusText,
                    senderId: User.FindFirstValue(ClaimTypes.NameIdentifier)
                );
            }

            TempData["SuccessMessage"] = $"Reservation status updated to {status}.";
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

        // GET: /Facility/PaymentMethods
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> PaymentMethods()
        {
            var paymentMethods = await _context.PaymentMethods.ToListAsync();
            return View(paymentMethods);
        }

        // GET: /Facility/CreatePaymentMethod
        [Authorize(Roles = "admin")]
        public IActionResult CreatePaymentMethod()
        {
            return View();
        }

        // POST: /Facility/CreatePaymentMethod
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreatePaymentMethod(PaymentMethod paymentMethod, IFormFile? qrCodeImage)
        {
            if (ModelState.IsValid)
            {
                // Handle QR code image upload if provided
                if (qrCodeImage != null && qrCodeImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "payment_qr_codes");
                    Directory.CreateDirectory(uploadsFolder); // Ensure directory exists
                    
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(qrCodeImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await qrCodeImage.CopyToAsync(fileStream);
                    }
                    
                    paymentMethod.QRCodeFileName = uniqueFileName;
                }
                
                paymentMethod.CreatedAt = DateTime.UtcNow;
                _context.Add(paymentMethod);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(PaymentMethods));
            }
            
            return View(paymentMethod);
        }

        // POST: /Facility/TogglePaymentMethodStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> TogglePaymentMethodStatus(int id)
        {
            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            
            if (paymentMethod == null)
            {
                return NotFound();
            }
            
            paymentMethod.IsActive = !paymentMethod.IsActive;
            paymentMethod.UpdatedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(PaymentMethods));
        }

        // GET: /Facility/EditPaymentMethod/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditPaymentMethod(int id)
        {
            var paymentMethod = await _context.PaymentMethods.FindAsync(id);
            
            if (paymentMethod == null)
            {
                return NotFound();
            }
            
            return View(paymentMethod);
        }

        // POST: /Facility/EditPaymentMethod/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditPaymentMethod(int id, PaymentMethod paymentMethod, IFormFile? qrCodeImage)
        {
            if (id != paymentMethod.Id)
            {
                return NotFound();
            }
            
            if (ModelState.IsValid)
            {
                try
                {
                    var existingPaymentMethod = await _context.PaymentMethods.FindAsync(id);
                    
                    if (existingPaymentMethod == null)
                    {
                        return NotFound();
                    }
                    
                    // Handle QR code image upload if provided
                    if (qrCodeImage != null && qrCodeImage.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "payment_qr_codes");
                        Directory.CreateDirectory(uploadsFolder); // Ensure directory exists
                        
                        // Delete old file if exists
                        if (!string.IsNullOrEmpty(existingPaymentMethod.QRCodeFileName))
                        {
                            var oldFilePath = Path.Combine(uploadsFolder, existingPaymentMethod.QRCodeFileName);
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }
                        
                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(qrCodeImage.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await qrCodeImage.CopyToAsync(fileStream);
                        }
                        
                        existingPaymentMethod.QRCodeFileName = uniqueFileName;
                    }
                    
                    existingPaymentMethod.Name = paymentMethod.Name;
                    existingPaymentMethod.Type = paymentMethod.Type;
                    existingPaymentMethod.Details = paymentMethod.Details;
                    existingPaymentMethod.Instructions = paymentMethod.Instructions;
                    existingPaymentMethod.UpdatedAt = DateTime.UtcNow;
                    
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentMethodExists(paymentMethod.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(PaymentMethods));
            }
            return View(paymentMethod);
        }

        // POST: /Facility/UploadReceiptForReservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadReceiptForReservation(int id, IFormFile receiptFile, string paymentMethod)
        {
            // Get current user
            var user = await _userManager.GetUserAsync(User);
            
            // Get reservation
            var reservation = await _context.FacilityReservations
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
            
            if (reservation == null)
            {
                return NotFound();
            }
            
            // Ensure reservation is in PaymentPending status
            if (reservation.Status != ReservationStatus.PaymentPending)
            {
                ModelState.AddModelError("", "Receipt can only be uploaded for reservations that are awaiting payment.");
                return RedirectToAction(nameof(MyReservations));
            }
            
            // Handle receipt file upload
            if (receiptFile != null && receiptFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "receipts");
                Directory.CreateDirectory(uploadsFolder); // Ensure directory exists
                
                // Delete old file if exists
                if (!string.IsNullOrEmpty(reservation.ReceiptFileName))
                {
                    var oldFilePath = Path.Combine(uploadsFolder, reservation.ReceiptFileName);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
                
                var uniqueFileName = $"receipt_{reservation.Id}_{Guid.NewGuid()}_{Path.GetFileName(receiptFile.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await receiptFile.CopyToAsync(fileStream);
                }
                
                // Update reservation
                reservation.ReceiptFileName = uniqueFileName;
                reservation.ReceiptUploadDate = DateTime.UtcNow;
                reservation.PaymentMethod = paymentMethod;
                reservation.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
            }
            else
            {
                ModelState.AddModelError("", "Receipt file is required.");
                return RedirectToAction(nameof(MyReservations));
            }
            
            return RedirectToAction(nameof(MyReservations));
        }

        // GET: /Facility/PaymentDetails/5
        public async Task<IActionResult> PaymentDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            
            var reservation = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && 
                    (r.UserId == user.Id || User.IsInRole("admin") || User.IsInRole("homeowners")));
            
            if (reservation == null)
            {
                return NotFound();
            }
            
            var paymentMethods = await _context.PaymentMethods
                .Where(p => p.IsActive)
                .ToListAsync();
            
            ViewBag.PaymentMethods = paymentMethods;
            
            return View(reservation);
        }

        // POST: /Facility/VerifyPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> VerifyPayment(int id, bool isPaymentVerified, string verificationNotes)
        {
            var reservation = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction(nameof(ManageReservations));
            }
            
            var user = await _userManager.GetUserAsync(User);
            
            if (isPaymentVerified)
            {
                reservation.Status = ReservationStatus.Approved;
                reservation.IsPaid = true;
                reservation.PaymentVerificationDate = DateTime.UtcNow;
                reservation.PaymentVerificationNotes = verificationNotes;
                reservation.PaymentVerifiedByUserId = user.Id;
                
                // Find and reject all conflicting reservations
                var conflictingReservations = await _context.FacilityReservations
                    .Where(r => r.Id != reservation.Id &&
                            r.FacilityId == reservation.FacilityId &&
                            r.ReservationDate.Date == reservation.ReservationDate.Date &&
                            r.Status == ReservationStatus.Pending &&
                            !r.IsDeleted &&
                            ((r.StartTime <= reservation.StartTime && reservation.StartTime < r.EndTime) ||
                             (r.StartTime < reservation.EndTime && reservation.EndTime <= r.EndTime) ||
                             (reservation.StartTime <= r.StartTime && r.StartTime < reservation.EndTime) ||
                             (reservation.StartTime < r.EndTime && r.EndTime <= reservation.EndTime)))
                    .ToListAsync();
                    
                foreach (var conflictingReservation in conflictingReservations)
                {
                    conflictingReservation.Status = ReservationStatus.Rejected;
                    conflictingReservation.RejectionReason = "This time slot has been booked by another reservation.";
                    conflictingReservation.UpdatedAt = DateTime.UtcNow;
                }
                
                TempData["SuccessMessage"] = $"Payment verified successfully for reservation #{reservation.Id} by {reservation.User.Email}.";
            }
            else
            {
                // If payment is rejected, keep it in PaymentPending state but add notes
                // so user can reupload a correct receipt
                reservation.PaymentVerificationNotes = verificationNotes;
                // Clear the previous receipt so user can upload a new one
                if (!string.IsNullOrEmpty(reservation.ReceiptFileName))
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "receipts");
                    var oldFilePath = Path.Combine(uploadsFolder, reservation.ReceiptFileName);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                    reservation.ReceiptFileName = null;
                    reservation.ReceiptUploadDate = null;
                }
                TempData["SuccessMessage"] = $"Payment rejected for reservation #{reservation.Id}. User notified to upload new payment receipt.";
            }
            
            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(ManageReservations));
        }

        // POST: /Facility/AdminCancelReservation/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AdminCancelReservation(int id, string cancellationReason)
        {
            var reservation = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction(nameof(ManageReservations));
            }
            
            if (string.IsNullOrWhiteSpace(cancellationReason))
            {
                TempData["ErrorMessage"] = "Cancellation reason is required.";
                return RedirectToAction(nameof(ManageReservations));
            }
            
            // Admin can cancel any reservation that is not already cancelled, rejected, or completed
            if (reservation.Status == ReservationStatus.Cancelled ||
                reservation.Status == ReservationStatus.Rejected ||
                reservation.Status == ReservationStatus.Completed)
            {
                TempData["ErrorMessage"] = $"Cannot cancel this reservation due to its current status ({reservation.Status}).";
                return RedirectToAction(nameof(ManageReservations));
            }
            
            reservation.Status = ReservationStatus.Cancelled;
            reservation.RejectionReason = cancellationReason; // Reuse the rejection reason field for cancellation notes
            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Reservation #{reservation.Id} for {reservation.Facility.Name} by {reservation.User.Email} has been cancelled successfully.";
            return RedirectToAction(nameof(ManageReservations));
        }

        // POST: /Facility/MarkReservationComplete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> MarkReservationComplete(int id, string? completionNotes)
        {
            var reservation = await _context.FacilityReservations
                .Include(r => r.Facility)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);
            
            if (reservation == null)
            {
                TempData["ErrorMessage"] = "Reservation not found.";
                return RedirectToAction(nameof(ManageReservations));
            }
            
            // Verify the reservation is approved and paid
            if (reservation.Status != ReservationStatus.Approved || !reservation.IsPaid)
            {
                TempData["ErrorMessage"] = "Only approved and paid reservations can be marked as complete.";
                return RedirectToAction(nameof(ManageReservations));
            }
            
            reservation.Status = ReservationStatus.Completed;
            reservation.CompletionNotes = completionNotes;
            reservation.CompletedDate = DateTime.UtcNow;
            reservation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Reservation #{reservation.Id} for {reservation.Facility.Name} has been marked as complete.";
            return RedirectToAction(nameof(ManageReservations));
        }

        private bool FacilityExists(int id)
        {
            return _context.Facilities.Any(e => e.Id == id);
        }

        private bool PaymentMethodExists(int id)
        {
            return _context.PaymentMethods.Any(p => p.Id == id);
        }

        #endregion
    }
}
