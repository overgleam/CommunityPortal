using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models.Billing;
using CommunityPortal.Services;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly PdfService _pdfService;

        public BillingController(ApplicationDbContext context, 
            IWebHostEnvironment webHostEnvironment,
            PdfService pdfService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _pdfService = pdfService;
        }

        // GET: Billing
        public async Task<IActionResult> Index(int page = 1, string searchTerm = null, string sortBy = "BillingDate", 
            string sortDirection = "desc", string statusFilter = null, string dateFilter = null)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return RedirectToAction("AccessDenied", "Account");

            var userId = userIdClaim.Value;
            var user = await _context.Users.Include(u => u.Administrator).Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == userId);

            // If the user is a homeowner, show only their bills
            if (user.Homeowner != null)
            {
                return await HomeownerBillingDashboard(userId);
            }
            // If the user is an admin or staff, show all bills with filtering options
            else if (user.Administrator != null || user.Staff != null)
            {
                return await AdminBillingList(page, searchTerm, sortBy, sortDirection, statusFilter, dateFilter);
            }

            return RedirectToAction("AccessDenied", "Account");
        }

        // GET: Billing/HomeownerDashboard
        [Authorize(Roles = "homeowners")]
        private async Task<IActionResult> HomeownerBillingDashboard(string userId)
        {
            var homeowner = await _context.Homeowners
                .FirstOrDefaultAsync(h => h.UserId == userId);

            if (homeowner == null)
                return NotFound();

            var bills = await _context.Bills
                .Include(b => b.BillItems)
                .Include(b => b.Payments)
                .Where(b => b.HomeownerId == homeowner.UserId && b.IsActive)
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();

            // Get current bill (most recent unpaid or partially paid)
            var currentBill = bills
                .Where(b => b.Status == "Unpaid" || b.Status == "Partially Paid" || b.Status == "Overdue")
                .OrderBy(b => b.DueDate)
                .FirstOrDefault();

            var totalDue = bills
                .Where(b => b.Status == "Unpaid" || b.Status == "Partially Paid" || b.Status == "Overdue")
                .Sum(b => b.BalanceAmount);

            var totalPaid = bills
                .Sum(b => b.PaidAmount);

            var paidBills = bills.Count(b => b.Status == "Paid");
            var overdueBills = bills.Count(b => b.Status == "Overdue");

            var viewModel = new HomeownerBillingDashboardViewModel
            {
                RecentBills = bills.Take(5).ToList(),
                CurrentBill = currentBill,
                TotalDue = totalDue,
                TotalPaid = totalPaid,
                TotalBills = bills.Count,
                PaidBills = paidBills,
                OverdueBills = overdueBills
            };

            return View("HomeownerDashboard", viewModel);
        }

        // GET: Billing/AdminList
        [Authorize(Roles = "admin")]
        private async Task<IActionResult> AdminBillingList(int page = 1, string searchTerm = null, string sortBy = "BillingDate", 
            string sortDirection = "desc", string statusFilter = null, string dateFilter = null)
        {
            const int pageSize = 10;
            
            var query = _context.Bills
                .Include(b => b.Homeowner)
                .ThenInclude(h => h.User)
                .Include(b => b.BillItems)
                .Include(b => b.Payments)
                .Where(b => b.IsActive);

            // Apply search
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(b => 
                    b.Homeowner.FirstName.Contains(searchTerm) || 
                    b.Homeowner.LastName.Contains(searchTerm) ||
                    b.BillingPeriod.Contains(searchTerm) ||
                    b.Id.ToString().Contains(searchTerm));
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(b => b.Status == statusFilter);
            }

            // Apply date filter
            if (!string.IsNullOrEmpty(dateFilter))
            {
                DateTime now = DateTime.Now;
                
                switch(dateFilter)
                {
                    case "ThisMonth":
                        query = query.Where(b => b.BillingDate.Month == now.Month && b.BillingDate.Year == now.Year);
                        break;
                    case "Last3Months":
                        var threeMonthsAgo = now.AddMonths(-3);
                        query = query.Where(b => b.BillingDate >= threeMonthsAgo);
                        break;
                    case "ThisYear":
                        query = query.Where(b => b.BillingDate.Year == now.Year);
                        break;
                }
            }

            // Apply sorting
            switch (sortBy)
            {
                case "BillingDate":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(b => b.BillingDate)
                        : query.OrderByDescending(b => b.BillingDate);
                    break;
                case "DueDate":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(b => b.DueDate)
                        : query.OrderByDescending(b => b.DueDate);
                    break;
                case "Amount":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(b => b.TotalAmount)
                        : query.OrderByDescending(b => b.TotalAmount);
                    break;
                case "Status":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(b => b.Status)
                        : query.OrderByDescending(b => b.Status);
                    break;
                case "Homeowner":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(b => b.Homeowner.LastName)
                        : query.OrderByDescending(b => b.Homeowner.LastName);
                    break;
                default:
                    query = query.OrderByDescending(b => b.BillingDate);
                    break;
            }

            var totalItems = await query.CountAsync();
            var bills = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new BillListViewModel
            {
                Bills = bills,
                PaginationInfo = new PaginationInfo
                {
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalItems = totalItems
                },
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDirection = sortDirection,
                StatusFilter = statusFilter,
                DateFilter = dateFilter
            };

            return View("AdminList", viewModel);
        }

        // GET: Billing/BillDetails/5
        public async Task<IActionResult> BillDetails(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Homeowner)
                .ThenInclude(h => h.User)
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.FeeType)
                .Include(b => b.Payments)
                .ThenInclude(p => p.PaymentMethod)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bill == null)
                return NotFound();

            // Security check: Only admins/staff or the homeowner can view this bill
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return RedirectToAction("AccessDenied", "Account");

            var userId = userIdClaim.Value;
            var isAdmin = User.IsInRole("admin") || User.IsInRole("staff");

            if (!isAdmin && bill.HomeownerId != userId)
                return RedirectToAction("AccessDenied", "Account");

            var viewModel = new BillDetailsViewModel
            {
                Bill = bill,
                BillItems = bill.BillItems.ToList(),
                Payments = bill.Payments.ToList(),
                Homeowner = bill.Homeowner,
                TotalPaid = bill.PaidAmount,
                RemainingBalance = bill.BalanceAmount,
                BillingPeriod = bill.BillingPeriod
            };

            return View(viewModel);
        }

        // GET: Billing/CreateBill
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateBill()
        {
            var homeowners = await _context.Homeowners
                .Include(h => h.User)
                .Where(h => !h.User.IsDeleted)
                .Select(h => new SelectListItem
                {
                    Value = h.UserId,
                    Text = $"{h.FirstName} {h.LastName} - Block {h.BlockNumber}, House {h.HouseNumber}"
                })
                .ToListAsync();

            var feeTypes = await _context.FeeTypes
                .Where(ft => ft.IsActive)
                .Select(ft => new SelectListItem
                {
                    Value = ft.Id.ToString(),
                    Text = $"{ft.Name} - {ft.DefaultAmount:C2}"
                })
                .ToListAsync();

            // Create a model with default billing items based on required fee types
            var requiredFeeTypes = await _context.FeeTypes
                .Where(ft => ft.IsRequired && ft.IsActive)
                .ToListAsync();

            var billItems = requiredFeeTypes.Select(ft => new BillItemViewModel
            {
                FeeTypeId = ft.Id,
                Description = ft.Description,
                Amount = ft.DefaultAmount,
                FeeTypes = feeTypes
            }).ToList();

            var model = new CreateBillViewModel
            {
                Homeowners = homeowners,
                BillingDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(15),
                BillingPeriod = $"{DateTime.Today:MMMM yyyy}",
                BillItems = billItems
            };

            return View(model);
        }

        // POST: Billing/CreateBill
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateBill(CreateBillViewModel model)
        {
            // Remove validation errors for select lists
            ModelState.Remove("Homeowners");
            
            // Remove validation errors for FeeTypes in each bill item
            for (int i = 0; i < model.BillItems.Count; i++)
            {
                ModelState.Remove($"BillItems[{i}].FeeTypes");
            }
            
            if (!ModelState.IsValid)
            {
                model.Homeowners = await _context.Homeowners
                    .Include(h => h.User)
                    .Where(h => !h.User.IsDeleted)
                    .Select(h => new SelectListItem
                    {
                        Value = h.UserId,
                        Text = $"{h.FirstName} {h.LastName} - Block {h.BlockNumber}, House {h.HouseNumber}"
                    })
                    .ToListAsync();

                var feeTypes = await _context.FeeTypes
                    .Where(ft => ft.IsActive)
                    .Select(ft => new SelectListItem
                    {
                        Value = ft.Id.ToString(),
                        Text = $"{ft.Name} - {ft.DefaultAmount:C2}"
                    })
                    .ToListAsync();
                
                foreach (var billItem in model.BillItems)
                {
                    billItem.FeeTypes = feeTypes;
                }

                return View(model);
            }

            // Calculate total amount
            decimal totalAmount = model.BillItems.Sum(bi => bi.Amount);

            // Create new bill
            var bill = new Bill
            {
                HomeownerId = model.HomeownerId,
                BillingDate = model.BillingDate,
                DueDate = model.DueDate,
                BillingPeriod = model.BillingPeriod,
                TotalAmount = totalAmount,
                BalanceAmount = totalAmount,
                Status = "Unpaid",
                Notes = model.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            // Add bill items
            foreach (var item in model.BillItems)
            {
                var billItem = new BillItem
                {
                    BillId = bill.Id,
                    FeeTypeId = item.FeeTypeId,
                    Description = item.Description,
                    Amount = item.Amount,
                    Notes = item.Notes,
                    CreatedAt = DateTime.UtcNow
                };

                _context.BillItems.Add(billItem);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bill created successfully.";
            return RedirectToAction(nameof(BillDetails), new { id = bill.Id });
        }

        // GET: Billing/MakePayment/5
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> MakePayment(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Homeowner)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bill == null)
                return NotFound();

            // Security check: Only the homeowner can make payment for their own bill
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || bill.HomeownerId != userIdClaim.Value)
                return RedirectToAction("AccessDenied", "Account");

            var paymentMethods = await _context.PaymentMethods
                .Where(pm => pm.IsActive)
                .Select(pm => new SelectListItem
                {
                    Value = pm.Id.ToString(),
                    Text = $"{pm.Name} - {pm.Type}"
                })
                .ToListAsync();

            var model = new CreatePaymentViewModel
            {
                BillId = bill.Id,
                HomeownerId = bill.HomeownerId,
                Amount = bill.BalanceAmount,
                PaymentMethods = paymentMethods
            };

            return View(model);
        }

        // POST: Billing/MakePayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> MakePayment(CreatePaymentViewModel model)
        {
            // Remove validation errors for select lists
            ModelState.Remove("PaymentMethods");
            
            if (!ModelState.IsValid)
            {
                model.PaymentMethods = await _context.PaymentMethods
                    .Where(pm => pm.IsActive)
                    .Select(pm => new SelectListItem
                    {
                        Value = pm.Id.ToString(),
                        Text = $"{pm.Name} - {pm.Type}"
                    })
                    .ToListAsync();

                return View(model);
            }

            var bill = await _context.Bills.FindAsync(model.BillId);
            if (bill == null)
                return NotFound();

            // Validate the payment amount doesn't exceed the remaining balance
            if (model.Amount > bill.BalanceAmount)
            {
                ModelState.AddModelError("Amount", "Payment amount cannot exceed the remaining balance.");
                model.PaymentMethods = await _context.PaymentMethods
                    .Where(pm => pm.IsActive)
                    .Select(pm => new SelectListItem
                    {
                        Value = pm.Id.ToString(),
                        Text = $"{pm.Name} - {pm.Type}"
                    })
                    .ToListAsync();
                return View(model);
            }

            // Handle file upload for payment proof
            string paymentProofFileName = null;
            if (model.PaymentProofImage != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "payments");
                Directory.CreateDirectory(uploadsFolder); // Ensure directory exists
                
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.PaymentProofImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.PaymentProofImage.CopyToAsync(fileStream);
                }
                
                paymentProofFileName = uniqueFileName;
            }

            // Create payment record
            var payment = new Payment
            {
                BillId = model.BillId,
                HomeownerId = model.HomeownerId,
                PaymentDate = model.PaymentDate,
                Amount = model.Amount,
                PaymentMethodId = model.PaymentMethodId,
                TransactionReference = model.TransactionReference,
                Notes = model.Notes,
                Status = "Pending",
                PaymentProofFile = paymentProofFileName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment submitted successfully. It will be reviewed by an administrator.";
            return RedirectToAction(nameof(BillDetails), new { id = model.BillId });
        }

        // GET: Billing/EditBill/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditBill(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Homeowner)
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.FeeType)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bill == null)
                return NotFound();

            var homeowners = await _context.Homeowners
                .Include(h => h.User)
                .Where(h => !h.User.IsDeleted)
                .Select(h => new SelectListItem
                {
                    Value = h.UserId,
                    Text = $"{h.FirstName} {h.LastName} - Block {h.BlockNumber}, House {h.HouseNumber}",
                    Selected = h.UserId == bill.HomeownerId
                })
                .ToListAsync();

            var feeTypes = await _context.FeeTypes
                .Where(ft => ft.IsActive)
                .Select(ft => new SelectListItem
                {
                    Value = ft.Id.ToString(),
                    Text = $"{ft.Name} - {ft.DefaultAmount:C2}"
                })
                .ToListAsync();

            var billItems = bill.BillItems.Select(bi => new BillItemViewModel
            {
                Id = bi.Id,
                FeeTypeId = bi.FeeTypeId,
                Description = bi.Description,
                Amount = bi.Amount,
                Notes = bi.Notes,
                FeeTypes = feeTypes
            }).ToList();

            var model = new EditBillViewModel
            {
                Id = bill.Id,
                HomeownerId = bill.HomeownerId,
                BillingDate = bill.BillingDate,
                DueDate = bill.DueDate,
                BillingPeriod = bill.BillingPeriod,
                Notes = bill.Notes,
                Homeowners = homeowners,
                BillItems = billItems
            };

            return View(model);
        }

        // POST: Billing/EditBill/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditBill(EditBillViewModel model)
        {
            // Remove validation errors for select lists
            ModelState.Remove("Homeowners");
            
            // Remove validation errors for FeeTypes in each bill item
            for (int i = 0; i < model.BillItems.Count; i++)
            {
                ModelState.Remove($"BillItems[{i}].FeeTypes");
            }
            
            if (!ModelState.IsValid)
            {
                model.Homeowners = await _context.Homeowners
                    .Include(h => h.User)
                    .Where(h => !h.User.IsDeleted)
                    .Select(h => new SelectListItem
                    {
                        Value = h.UserId,
                        Text = $"{h.FirstName} {h.LastName} - Block {h.BlockNumber}, House {h.HouseNumber}",
                        Selected = h.UserId == model.HomeownerId
                    })
                    .ToListAsync();

                var feeTypes = await _context.FeeTypes
                    .Where(ft => ft.IsActive)
                    .Select(ft => new SelectListItem
                    {
                        Value = ft.Id.ToString(),
                        Text = $"{ft.Name} - {ft.DefaultAmount:C2}"
                    })
                    .ToListAsync();
                
                foreach (var billItem in model.BillItems)
                {
                    billItem.FeeTypes = feeTypes;
                }

                return View(model);
            }

            var bill = await _context.Bills
                .Include(b => b.BillItems)
                .FirstOrDefaultAsync(b => b.Id == model.Id);

            if (bill == null)
                return NotFound();

            // Calculate total amount
            decimal totalAmount = model.BillItems.Sum(bi => bi.Amount);

            // Determine how much has been paid already
            decimal paidAmount = bill.TotalAmount - bill.BalanceAmount;

            // Update bill
            bill.HomeownerId = model.HomeownerId;
            bill.BillingDate = model.BillingDate;
            bill.DueDate = model.DueDate;
            bill.BillingPeriod = model.BillingPeriod;
            bill.TotalAmount = totalAmount;
            bill.BalanceAmount = totalAmount - paidAmount; // Adjust balance based on what's been paid
            bill.Notes = model.Notes;
            bill.UpdatedAt = DateTime.UtcNow;

            // Update status based on payment amounts
            if (bill.BalanceAmount <= 0)
            {
                bill.Status = "Paid";
            }
            else if (paidAmount > 0)
            {
                bill.Status = "Partially Paid";
            }
            else
            {
                bill.Status = DateTime.Now > bill.DueDate ? "Overdue" : "Unpaid";
            }

            // Handle bill items
            // Remove items that are not in the edit model
            var existingItemIds = model.BillItems.Where(bi => bi.Id > 0).Select(bi => bi.Id).ToList();
            var itemsToRemove = bill.BillItems.Where(bi => !existingItemIds.Contains(bi.Id)).ToList();
            
            foreach (var item in itemsToRemove)
            {
                _context.BillItems.Remove(item);
            }

            // Update existing items and add new ones
            foreach (var itemModel in model.BillItems)
            {
                if (itemModel.Id > 0)
                {
                    // Update existing item
                    var existingItem = bill.BillItems.FirstOrDefault(bi => bi.Id == itemModel.Id);
                    if (existingItem != null)
                    {
                        existingItem.FeeTypeId = itemModel.FeeTypeId;
                        existingItem.Description = itemModel.Description;
                        existingItem.Amount = itemModel.Amount;
                        existingItem.Notes = itemModel.Notes;
                        existingItem.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Add new item
                    var newItem = new BillItem
                    {
                        BillId = bill.Id,
                        FeeTypeId = itemModel.FeeTypeId,
                        Description = itemModel.Description,
                        Amount = itemModel.Amount,
                        Notes = itemModel.Notes,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.BillItems.Add(newItem);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bill updated successfully.";
            return RedirectToAction(nameof(BillDetails), new { id = bill.Id });
        }

        // POST: Billing/DeleteBill/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteBill(int id)
        {
            var bill = await _context.Bills.FindAsync(id);
            if (bill == null)
                return NotFound();

            // Soft delete
            bill.IsActive = false;
            bill.DeletedAt = DateTime.UtcNow;
            
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Bill deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Billing/VerifyPayment/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> VerifyPayment(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Bill)
                .Include(p => p.Homeowner)
                .Include(p => p.PaymentMethod)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound();

            var viewModel = new VerifyPaymentViewModel
            {
                PaymentId = payment.Id,
                BillId = payment.BillId,
                HomeownerName = $"{payment.Homeowner.FirstName} {payment.Homeowner.LastName}",
                PaymentDate = payment.PaymentDate,
                Amount = payment.Amount,
                PaymentMethod = payment.PaymentMethod.Name,
                TransactionReference = payment.TransactionReference,
                PaymentProofFile = payment.PaymentProofFile,
                Status = payment.Status
            };

            return View(viewModel);
        }

        // POST: Billing/VerifyPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> VerifyPayment(int id, string notes)
        {
            var payment = await _context.Payments
                .Include(p => p.Bill)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound();

            // Update payment status
            payment.Status = "Verified";
            payment.Notes = string.IsNullOrEmpty(notes) ? payment.Notes : notes;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.VerifiedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            payment.VerifiedAt = DateTime.UtcNow;

            // Update bill status and amounts
            var bill = payment.Bill;
            bill.PaidAmount += payment.Amount;
            bill.BalanceAmount = bill.TotalAmount - bill.PaidAmount;
            
            if (bill.PaidAmount >= bill.TotalAmount)
            {
                bill.Status = "Paid";
                bill.PaidDate = DateTime.UtcNow;
            }
            else
            {
                bill.Status = "Partially Paid";
            }
            
            bill.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Payment verified successfully!";
            return RedirectToAction(nameof(BillDetails), new { id = payment.BillId });
        }

        // POST: Billing/RejectPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RejectPayment(int id, string rejectionReason)
        {
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound();

            payment.Status = "Rejected";
            payment.UpdatedAt = DateTime.UtcNow;
            payment.VerifiedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            payment.VerifiedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(rejectionReason))
            {
                payment.Notes = string.IsNullOrEmpty(payment.Notes) 
                    ? "Rejection Reason: " + rejectionReason 
                    : payment.Notes + "\n\nRejection Reason: " + rejectionReason;
            }

            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Payment has been rejected.";
            return RedirectToAction(nameof(BillDetails), new { id = payment.BillId });
        }

        // GET: Billing/DownloadBill/5
        public async Task<IActionResult> DownloadBill(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Homeowner)
                .ThenInclude(h => h.User)
                .Include(b => b.BillItems)
                .ThenInclude(bi => bi.FeeType)
                .Include(b => b.Payments)
                .ThenInclude(p => p.PaymentMethod)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (bill == null)
                return NotFound();

            // Security check: Only admins/staff or the homeowner can download this bill
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return RedirectToAction("AccessDenied", "Account");

            var userId = userIdClaim.Value;
            var isAdmin = User.IsInRole("admin") || User.IsInRole("staff");

            if (!isAdmin && bill.HomeownerId != userId)
                return RedirectToAction("AccessDenied", "Account");

            // Create view model for the PDF
            var viewModel = new BillDetailsViewModel
            {
                Bill = bill,
                Homeowner = bill.Homeowner,
                BillItems = bill.BillItems.ToList(),
                Payments = bill.Payments.ToList()
            };

            // Generate PDF using our service
            byte[] pdfBytes = _pdfService.GenerateBillPdf(viewModel);

            // Return the PDF as a file
            string fileName = $"Bill-{bill.Id}-{DateTime.Now:yyyyMMdd}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: Billing/AdminDashboard
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> AdminDashboard(string period = "ThisMonth")
        {
            DateTime startDate;
            DateTime endDate = DateTime.Now;
            string periodLabel;

            // Determine date range based on selected period
            switch (period)
            {
                case "Last30Days":
                    startDate = DateTime.Now.AddDays(-30);
                    periodLabel = "Last 30 Days";
                    break;
                case "Last3Months":
                    startDate = DateTime.Now.AddMonths(-3);
                    periodLabel = "Last 3 Months";
                    break;
                case "Last6Months":
                    startDate = DateTime.Now.AddMonths(-6);
                    periodLabel = "Last 6 Months";
                    break;
                case "ThisYear":
                    startDate = new DateTime(DateTime.Now.Year, 1, 1);
                    periodLabel = $"Year {DateTime.Now.Year}";
                    break;
                case "AllTime":
                    startDate = DateTime.MinValue;
                    periodLabel = "All Time";
                    break;
                default: // ThisMonth
                    startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                    periodLabel = $"{DateTime.Now.ToString("MMMM yyyy")}";
                    period = "ThisMonth";
                    break;
            }

            // Get all bills within the period
            var bills = await _context.Bills
                .Include(b => b.Payments)
                .Where(b => b.BillingDate >= startDate && b.BillingDate <= endDate && b.IsActive)
                .ToListAsync();

            // Calculate summary metrics
            decimal totalRevenue = bills.Sum(b => b.PaidAmount);
            decimal outstandingAmount = bills.Sum(b => b.BalanceAmount);
            decimal overdueAmount = bills
                .Where(b => b.Status == "Overdue")
                .Sum(b => b.BalanceAmount);

            int totalBills = bills.Count;
            int paidBills = bills.Count(b => b.Status == "Paid");
            int overdueBills = bills.Count(b => b.Status == "Overdue");

            // Get monthly revenue data for chart
            var monthlyRevenue = new List<MonthlyRevenueViewModel>();
            
            // Get last 6 months for the chart regardless of selected period
            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.Now.AddMonths(-i);
                var firstDayOfMonth = new DateTime(month.Year, month.Month, 1);
                var lastDayOfMonth = new DateTime(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month));
                
                var monthlyBills = await _context.Bills
                    .Include(b => b.Payments)
                    .Where(b => b.BillingDate >= firstDayOfMonth && 
                                b.BillingDate <= lastDayOfMonth && 
                                b.IsActive)
                    .ToListAsync();
                
                monthlyRevenue.Add(new MonthlyRevenueViewModel
                {
                    Month = month.ToString("MMM yyyy"),
                    Amount = monthlyBills.Sum(b => b.PaidAmount)
                });
            }

            // Get recent bills
            var recentBills = await _context.Bills
                .Include(b => b.Homeowner)
                .OrderByDescending(b => b.BillingDate)
                .Take(5)
                .ToListAsync();

            // Create the view model
            var viewModel = new AdminBillingDashboardViewModel
            {
                TotalRevenue = totalRevenue,
                OutstandingAmount = outstandingAmount,
                OverdueAmount = overdueAmount,
                TotalBills = totalBills,
                PaidBills = paidBills,
                OverdueBills = overdueBills,
                MonthlyRevenue = monthlyRevenue,
                RecentBills = recentBills,
                SelectedPeriod = period,
                PeriodLabel = periodLabel
            };

            return View(viewModel);
        }

        // GET: Billing/AllBills
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> AllBills(int page = 1)
        {
            const int pageSize = 10;
            
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return RedirectToAction("AccessDenied", "Account");

            var userId = userIdClaim.Value;
            
            var query = _context.Bills
                .Include(b => b.BillItems)
                .Include(b => b.Payments)
                .Where(b => b.HomeownerId == userId && b.IsActive)
                .OrderByDescending(b => b.BillingDate);

            var totalItems = await query.CountAsync();
            var bills = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new BillListViewModel
            {
                Bills = bills,
                PaginationInfo = new PaginationInfo
                {
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalItems = totalItems
                }
            };

            return View("HomeownerBillList", viewModel);
        }

        // POST: Billing/CancelPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> CancelPayment(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Bill)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound();
                
            // Security check: Only the homeowner who made the payment can cancel it
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || payment.HomeownerId != userIdClaim.Value)
                return RedirectToAction("AccessDenied", "Account");
                
            // Only pending payments can be cancelled
            if (payment.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Only pending payments can be cancelled.";
                return RedirectToAction(nameof(BillDetails), new { id = payment.BillId });
            }
            
            // Remove the payment record
            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Payment cancelled successfully.";
            return RedirectToAction(nameof(BillDetails), new { id = payment.BillId });
        }

        // FEE TYPES MANAGEMENT
        // GET: Billing/FeeTypes
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> FeeTypes()
        {
            var feeTypes = await _context.FeeTypes
                .Where(f => f.DeletedAt == null)
                .OrderBy(f => f.Category)
                .ThenBy(f => f.Name)
                .ToListAsync();

            return View(feeTypes);
        }

        // GET: Billing/CreateFeeType
        [Authorize(Roles = "admin")]
        public IActionResult CreateFeeType()
        {
            return View();
        }

        // POST: Billing/CreateFeeType
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> CreateFeeType(FeeType feeType)
        {
            if (ModelState.IsValid)
            {
                feeType.CreatedAt = DateTime.UtcNow;
                _context.Add(feeType);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Fee type created successfully!";
                return RedirectToAction(nameof(FeeTypes));
            }
            return View(feeType);
        }

        // GET: Billing/EditFeeType/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditFeeType(int id)
        {
            var feeType = await _context.FeeTypes.FindAsync(id);
            if (feeType == null || feeType.DeletedAt != null)
            {
                return NotFound();
            }
            return View(feeType);
        }

        // POST: Billing/EditFeeType/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> EditFeeType(int id, FeeType feeType)
        {
            if (id != feeType.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingFeeType = await _context.FeeTypes.FindAsync(id);
                    if (existingFeeType == null || existingFeeType.DeletedAt != null)
                    {
                        return NotFound();
                    }

                    existingFeeType.Name = feeType.Name;
                    existingFeeType.Description = feeType.Description;
                    existingFeeType.DefaultAmount = feeType.DefaultAmount;
                    existingFeeType.Category = feeType.Category;
                    existingFeeType.IsRecurring = feeType.IsRecurring;
                    existingFeeType.IsRequired = feeType.IsRequired;
                    existingFeeType.IsActive = feeType.IsActive;
                    existingFeeType.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Fee type updated successfully!";
                    return RedirectToAction(nameof(FeeTypes));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FeeTypeExists(feeType.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(feeType);
        }

        // POST: Billing/DeleteFeeType/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteFeeType(int id)
        {
            var feeType = await _context.FeeTypes.FindAsync(id);
            if (feeType == null)
            {
                return NotFound();
            }

            // Check if fee type is being used in bill items
            var hasAssociatedBillItems = await _context.BillItems.AnyAsync(b => b.FeeTypeId == id);
            if (hasAssociatedBillItems)
            {
                // Soft delete - mark as deleted but keep in database
                feeType.DeletedAt = DateTime.UtcNow;
                feeType.IsActive = false;
            }
            else
            {
                // Hard delete - remove from database
                _context.FeeTypes.Remove(feeType);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Fee type deleted successfully!";
            return RedirectToAction(nameof(FeeTypes));
        }

        private bool FeeTypeExists(int id)
        {
            return _context.FeeTypes.Any(e => e.Id == id);
        }

        // BILLING SETTINGS MANAGEMENT
        // GET: Billing/Settings
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Settings()
        {
            var settings = await _context.BillingSettings
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (settings == null)
            {
                // Create default settings if none exist
                settings = new BillingSettings
                {
                    Name = "Default Billing Settings",
                    Description = "System-generated default billing settings",
                    LateFeePercentage = 5,
                    LateFeeDays = 30,
                    BillingCycleDay = 1,
                    PaymentDueDays = 15,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                };

                _context.Add(settings);
                await _context.SaveChangesAsync();
            }

            return View(settings);
        }

        // POST: Billing/Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Settings(BillingSettings model)
        {
            // Remove ModelState validation for CreatedBy as it will be set in the controller
            ModelState.Remove("CreatedBy");
            
            if (ModelState.IsValid)
            {
                var existing = await _context.BillingSettings.FindAsync(model.Id);
                if (existing != null)
                {
                    // Update existing settings
                    existing.Name = model.Name;
                    existing.Description = model.Description;
                    existing.LateFeePercentage = model.LateFeePercentage;
                    existing.LateFeeDays = model.LateFeeDays;
                    existing.BillingCycleDay = model.BillingCycleDay;
                    existing.PaymentDueDays = model.PaymentDueDays;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                }
                else
                {
                    // Create new settings and make it active
                    model.CreatedAt = DateTime.UtcNow;
                    model.CreatedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    model.IsActive = true;
                    _context.Add(model);
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Billing settings updated successfully!";
                return RedirectToAction(nameof(Settings));
            }
            return View(model);
        }

        // PAYMENT VERIFICATION
        // GET: Billing/PaymentHistory
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> PaymentHistory(int page = 1, string searchTerm = null, string sortBy = "PaymentDate", 
            string sortDirection = "desc", string statusFilter = null)
        {
            const int pageSize = 10;
            
            var query = _context.Payments
                .Include(p => p.Homeowner)
                .ThenInclude(h => h.User)
                .Include(p => p.Bill)
                .Include(p => p.PaymentMethod)
                .Where(p => p.DeletedAt == null);

            // Apply search
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => 
                    p.Homeowner.FirstName.Contains(searchTerm) || 
                    p.Homeowner.LastName.Contains(searchTerm) ||
                    p.TransactionReference.Contains(searchTerm) ||
                    p.Id.ToString().Contains(searchTerm));
            }

            // Apply status filter
            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(p => p.Status == statusFilter);
            }

            // Apply sorting
            switch (sortBy)
            {
                case "PaymentDate":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(p => p.PaymentDate)
                        : query.OrderByDescending(p => p.PaymentDate);
                    break;
                case "Amount":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(p => p.Amount)
                        : query.OrderByDescending(p => p.Amount);
                    break;
                case "Status":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(p => p.Status)
                        : query.OrderByDescending(p => p.Status);
                    break;
                case "Homeowner":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(p => p.Homeowner.LastName)
                        : query.OrderByDescending(p => p.Homeowner.LastName);
                    break;
                case "VerifiedAt":
                    query = sortDirection == "asc" 
                        ? query.OrderBy(p => p.VerifiedAt)
                        : query.OrderByDescending(p => p.VerifiedAt);
                    break;
                default:
                    query = query.OrderByDescending(p => p.PaymentDate);
                    break;
            }

            var totalItems = await query.CountAsync();
            var payments = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var viewModel = new PaymentListViewModel
            {
                Payments = payments,
                PaginationInfo = new PaginationInfo
                {
                    CurrentPage = page,
                    ItemsPerPage = pageSize,
                    TotalItems = totalItems
                },
                SearchTerm = searchTerm,
                SortBy = sortBy,
                SortDirection = sortDirection,
                StatusFilter = statusFilter
            };

            return View(viewModel);
        }
    }
}
