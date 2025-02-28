using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models.Billing;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BillingController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
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

            return View("AccessDenied");
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
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
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> VerifyPayment(int id, string notes)
        {
            var payment = await _context.Payments
                .Include(p => p.Bill)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
                return NotFound();

            // Update payment status
            payment.Status = "Verified";
            payment.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(notes))
            {
                payment.Notes = string.IsNullOrEmpty(payment.Notes) ? notes : payment.Notes + "\n\nVerification Note: " + notes;
            }

            // Update bill balance
            var bill = payment.Bill;
            bill.PaidAmount += payment.Amount;
            bill.BalanceAmount -= payment.Amount;

            // Update bill status
            if (bill.BalanceAmount <= 0)
            {
                bill.Status = "Paid";
            }
            else
            {
                bill.Status = "Partially Paid";
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment verified successfully.";
            return RedirectToAction(nameof(BillDetails), new { id = payment.BillId });
        }

        // POST: Billing/RejectPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> RejectPayment(int id, string rejectionReason)
        {
            var payment = await _context.Payments.FindAsync(id);

            if (payment == null)
                return NotFound();

            // Update payment status
            payment.Status = "Rejected";
            payment.UpdatedAt = DateTime.UtcNow;
            
            if (!string.IsNullOrEmpty(rejectionReason))
            {
                payment.Notes = string.IsNullOrEmpty(payment.Notes) ? 
                    "Rejection Reason: " + rejectionReason : 
                    payment.Notes + "\n\nRejection Reason: " + rejectionReason;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment rejected.";
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

            // Generate PDF file name
            string fileName = $"Bill_{bill.Id}_{bill.BillingPeriod.Replace(" ", "_")}.pdf";

            // Set up the directory
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "temp", "bills");
            Directory.CreateDirectory(uploadsFolder); // Ensure directory exists

            string filePath = Path.Combine(uploadsFolder, fileName);

            // Generate PDF using a third-party library (Implement this method separately)
            await GenerateBillPdf(bill, filePath);

            // Return the PDF file for download
            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            
            // Delete the temporary file
            System.IO.File.Delete(filePath);

            return File(fileBytes, "application/pdf", fileName);
        }

        // Helper method to generate PDF
        private async Task GenerateBillPdf(Bill bill, string filePath)
        {
            // This is a placeholder for PDF generation code
            // In a real implementation, you would use a PDF library like iTextSharp, PDFsharp, or DinkToPdf

            // For demonstration purposes, let's create a simple HTML file and convert it to PDF
            string htmlContent = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <title>Bill #{bill.Id}</title>
                <style>
                    body {{ font-family: Arial, sans-serif; margin: 20px; }}
                    h1 {{ color: #333366; }}
                    table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
                    th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                    th {{ background-color: #f2f2f2; }}
                    .total {{ font-weight: bold; }}
                    .header {{ display: flex; justify-content: space-between; }}
                    .bill-info {{ margin-bottom: 20px; }}
                    .payment-info {{ margin-top: 30px; }}
                </style>
            </head>
            <body>
                <div class='header'>
                    <h1>Community Portal</h1>
                    <div>
                        <h2>BILL</h2>
                        <p>Bill #: {bill.Id}</p>
                    </div>
                </div>

                <div class='bill-info'>
                    <p><strong>Homeowner:</strong> {bill.Homeowner.FirstName} {bill.Homeowner.LastName}</p>
                    <p><strong>Address:</strong> Block {bill.Homeowner.BlockNumber}, House {bill.Homeowner.HouseNumber}</p>
                    <p><strong>Billing Period:</strong> {bill.BillingPeriod}</p>
                    <p><strong>Bill Date:</strong> {bill.BillingDate.ToString("MMMM dd, yyyy")}</p>
                    <p><strong>Due Date:</strong> {bill.DueDate.ToString("MMMM dd, yyyy")}</p>
                </div>

                <table>
                    <tr>
                        <th>Description</th>
                        <th>Fee Type</th>
                        <th>Amount</th>
                    </tr>";

            foreach (var item in bill.BillItems)
            {
                htmlContent += $@"
                    <tr>
                        <td>{item.Description}</td>
                        <td>{item.FeeType.Name}</td>
                        <td>₱{item.Amount.ToString("N2")}</td>
                    </tr>";
            }

            htmlContent += $@"
                    <tr class='total'>
                        <td colspan='2' style='text-align: right;'>Total:</td>
                        <td>₱{bill.TotalAmount.ToString("N2")}</td>
                    </tr>
                    <tr class='total'>
                        <td colspan='2' style='text-align: right;'>Amount Paid:</td>
                        <td>₱{bill.PaidAmount.ToString("N2")}</td>
                    </tr>
                    <tr class='total'>
                        <td colspan='2' style='text-align: right;'>Balance:</td>
                        <td>₱{bill.BalanceAmount.ToString("N2")}</td>
                    </tr>
                </table>

                <div class='payment-info'>
                    <h3>Payment Information</h3>
                    <p>Please make payment on or before the due date to avoid late fees.</p>
                    <p>A 5% penalty will be applied for payments made after 30 days from the due date.</p>
                    <p>For questions or concerns, please contact the administration office.</p>
                </div>
                
                <div style='margin-top: 50px; text-align: center;'>
                    <p>This is a computer-generated document. No signature required.</p>
                </div>
            </body>
            </html>";

            // For simplicity, we'll just write the HTML content to a file
            // In a real implementation, you would convert this HTML to PDF
            await System.IO.File.WriteAllTextAsync(filePath.Replace(".pdf", ".html"), htmlContent);

            // In a real implementation, you would call your PDF generation library here
            // For example: ConvertHtmlToPdf(htmlContent, filePath);
            
            // This is just a placeholder - in a real app you'd need to use a proper PDF generation library
            // For now, we'll just rename the HTML file to PDF for demonstration
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
            
            System.IO.File.Move(filePath.Replace(".pdf", ".html"), filePath);
        }

        // GET: Billing/AdminDashboard
        [Authorize(Roles = "admin,staff")]
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
    }
}
