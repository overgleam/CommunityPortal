using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.GatePass;
using CommunityPortal.Models.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
using CommunityPortal.Services;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class GatePassController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public GatePassController(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // GET: GatePass
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var homeowner = await _context.Homeowners.FirstOrDefaultAsync(h => h.UserId == userId);

            if (homeowner == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var gatePasses = await _context.GatePasses
                .Where(g => g.HomeownerId == userId && !g.IsDeleted)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            var viewModels = gatePasses.Select(g => new GatePassViewModel
            {
                Id = g.Id,
                VisitorName = g.VisitorName,
                Purpose = g.Purpose,
                VisitDate = g.VisitDate,
                ExpectedArrivalTime = g.ExpectedArrivalTime,
                NumberOfVisitors = g.NumberOfVisitors,
                VisitorVehicleDetails = g.VisitorVehicleDetails,
                ContactNumber = g.ContactNumber,
                PassNumber = g.PassNumber,
                ExpirationDate = g.ExpirationDate,
                Status = g.Status,
                AdminNotes = g.AdminNotes,
                HomeownerId = g.HomeownerId,
                CreatedAt = g.CreatedAt
            }).ToList();

            return View(viewModels);
        }

        // GET: GatePass/Create
        [Authorize(Roles = "homeowners")]
        public IActionResult Create()
        {
            return View(new GatePassViewModel   
            {
                VisitDate = DateTime.Today.AddDays(1),
                ExpectedArrivalTime = DateTime.Today.AddDays(1).AddHours(9) // 9:00 AM on the next day
            });
        }

        // POST: GatePass/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Create(GatePassViewModel model)
        {
            // Remove validation for fields that are set by the system
            ModelState.Remove("PassNumber");
            ModelState.Remove("AdminNotes");
            ModelState.Remove("HomeownerId");
            ModelState.Remove("HomeownerName");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var homeowner = await _context.Homeowners.FirstOrDefaultAsync(h => h.UserId == userId);

            if (homeowner == null)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var gatePass = new GatePass
            {
                HomeownerId = userId,
                VisitorName = model.VisitorName,
                Purpose = model.Purpose,
                VisitDate = model.VisitDate,
                ExpectedArrivalTime = model.ExpectedArrivalTime,
                NumberOfVisitors = model.NumberOfVisitors,
                VisitorVehicleDetails = model.VisitorVehicleDetails,
                ContactNumber = model.ContactNumber,
                Status = GatePassStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.Add(gatePass);
            await _context.SaveChangesAsync();

            // Notify admins about the new gate pass
            var admins = await _context.UserRoles
                .Where(ur => ur.RoleId == _context.Roles.Where(r => r.Name == "admin").Select(r => r.Id).FirstOrDefault())
                .Select(ur => ur.UserId)
                .ToListAsync();

            foreach (var adminId in admins)
            {
                await _notificationService.CreateNotificationAsync(
                    adminId,
                    "New Gate Pass Request",
                    $"A new gate pass has been requested by {User.Identity.Name} for visitor {model.VisitorName}.",
                    $"/GatePass/Details/{gatePass.Id}",
                    NotificationType.General,
                    userId);
            }

            TempData["SuccessMessage"] = "Gate pass request created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: GatePass/Details/5
        [Authorize(Roles = "homeowners,admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gatePass = await _context.GatePasses
                .Include(g => g.Homeowner)
                .ThenInclude(h => h.User)
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (gatePass == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("admin");

            // Only allow homeowners to view their own gate passes
            if (!isAdmin && gatePass.HomeownerId != userId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var viewModel = new GatePassViewModel
            {
                Id = gatePass.Id,
                VisitorName = gatePass.VisitorName,
                Purpose = gatePass.Purpose,
                VisitDate = gatePass.VisitDate,
                ExpectedArrivalTime = gatePass.ExpectedArrivalTime,
                NumberOfVisitors = gatePass.NumberOfVisitors,
                VisitorVehicleDetails = gatePass.VisitorVehicleDetails,
                ContactNumber = gatePass.ContactNumber,
                PassNumber = gatePass.PassNumber,
                ExpirationDate = gatePass.ExpirationDate,
                Status = gatePass.Status,
                AdminNotes = gatePass.AdminNotes,
                HomeownerId = gatePass.HomeownerId,
                HomeownerName = gatePass.Homeowner.User.FullName,
                CreatedAt = gatePass.CreatedAt
            };

            return View(viewModel);
        }

        // POST: GatePass/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "homeowners")]
        public async Task<IActionResult> Cancel(int id)
        {
            var gatePass = await _context.GatePasses.FindAsync(id);
            if (gatePass == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (gatePass.HomeownerId != userId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            if (gatePass.Status != GatePassStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending gate pass requests can be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            gatePass.Status = GatePassStatus.Cancelled;
            gatePass.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Notify admins about the cancelled gate pass
            var admins = await _context.UserRoles
                .Where(ur => ur.RoleId == _context.Roles.Where(r => r.Name == "admin").Select(r => r.Id).FirstOrDefault())
                .Select(ur => ur.UserId)
                .ToListAsync();

            foreach (var adminId in admins)
            {
                await _notificationService.CreateNotificationAsync(
                    adminId,
                    "Gate Pass Cancelled",
                    $"Gate pass for visitor {gatePass.VisitorName} has been cancelled by the homeowner.",
                    $"/GatePass/Details/{id}",
                    NotificationType.General);
            }

            TempData["SuccessMessage"] = "Gate pass request cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: GatePass/Admin
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Admin(string statusFilter)
        {
            var query = _context.GatePasses
                .Include(g => g.Homeowner)
                .ThenInclude(h => h.User)
                .Where(g => !g.IsDeleted);

            if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<GatePassStatus>(statusFilter, out var status))
            {
                query = query.Where(g => g.Status == status);
            }

            var gatePasses = await query
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            var viewModels = gatePasses.Select(g => new GatePassAdminViewModel
            {
                Id = g.Id,
                HomeownerName = g.Homeowner.User.FullName,
                BlockNumber = g.Homeowner.BlockNumber,
                HouseNumber = g.Homeowner.HouseNumber,
                VisitorName = g.VisitorName,
                Purpose = g.Purpose,
                VisitDate = g.VisitDate,
                ExpectedArrivalTime = g.ExpectedArrivalTime,
                NumberOfVisitors = g.NumberOfVisitors,
                VisitorVehicleDetails = g.VisitorVehicleDetails,
                ContactNumber = g.ContactNumber,
                PassNumber = g.PassNumber,
                ExpirationDate = g.ExpirationDate,
                Status = g.Status,
                AdminNotes = g.AdminNotes,
                HomeownerId = g.HomeownerId,
                CreatedAt = g.CreatedAt
            }).ToList();

            ViewBag.StatusFilter = statusFilter;
            ViewBag.Statuses = Enum.GetValues(typeof(GatePassStatus))
                .Cast<GatePassStatus>()
                .Select(s => s.ToString())
                .ToList();

            return View(viewModels);
        }

        // GET: GatePass/Approve/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Approve(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var gatePass = await _context.GatePasses
                .Include(g => g.Homeowner)
                .ThenInclude(h => h.User)
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (gatePass == null)
            {
                return NotFound();
            }

            if (gatePass.Status != GatePassStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending gate pass requests can be approved.";
                return RedirectToAction(nameof(Admin));
            }

            var model = new GatePassAdminViewModel
            {
                Id = gatePass.Id,
                HomeownerName = gatePass.Homeowner.User.FullName,
                BlockNumber = gatePass.Homeowner.BlockNumber,
                HouseNumber = gatePass.Homeowner.HouseNumber,
                VisitorName = gatePass.VisitorName,
                Purpose = gatePass.Purpose,
                VisitDate = gatePass.VisitDate,
                ExpectedArrivalTime = gatePass.ExpectedArrivalTime,
                NumberOfVisitors = gatePass.NumberOfVisitors,
                VisitorVehicleDetails = gatePass.VisitorVehicleDetails,
                ContactNumber = gatePass.ContactNumber,
                PassNumber = GeneratePassNumber(),
                ExpirationDate = gatePass.VisitDate.AddHours(12),
                Status = gatePass.Status,
                CreatedAt = gatePass.CreatedAt,
                HomeownerId = gatePass.HomeownerId
            };

            return View(model);
        }

        // POST: GatePass/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Approve(int id, GatePassAdminViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            // Remove validation for fields that are set by the system
            ModelState.Remove("PassNumber");
            ModelState.Remove("AdminNotes");
            ModelState.Remove("HomeownerId");
            ModelState.Remove("HomeownerName");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var gatePass = await _context.GatePasses
                .Include(g => g.Homeowner)
                .ThenInclude(h => h.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (gatePass == null)
            {
                return NotFound();
            }

            if (gatePass.Status != GatePassStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending gate pass requests can be approved.";
                return RedirectToAction(nameof(Admin));
            }

            var adminName = User.FindFirstValue(ClaimTypes.Name);

            gatePass.Status = GatePassStatus.Approved;
            gatePass.PassNumber = model.PassNumber;
            gatePass.ExpirationDate = model.ExpirationDate;
            gatePass.AdminNotes = model.AdminNotes;
            gatePass.ApprovedBy = adminName;
            gatePass.ApprovedDate = DateTime.UtcNow;
            gatePass.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Generate PDF
            var pdfBytes = await GenerateGatePassPdf(gatePass);
            gatePass.PdfPath = await SavePdfToFile(pdfBytes, gatePass.PassNumber);
            await _context.SaveChangesAsync();

            // Notify the homeowner that their gate pass was approved
            await _notificationService.CreateNotificationAsync(
                gatePass.HomeownerId,
                "Gate Pass Approved",
                $"Your gate pass for visitor {gatePass.VisitorName} has been approved. Pass Number: {gatePass.PassNumber}",
                $"/GatePass/Details/{id}",
                NotificationType.General,
                User.FindFirstValue(ClaimTypes.NameIdentifier));

            TempData["SuccessMessage"] = "Gate pass approved successfully.";
            return RedirectToAction(nameof(Admin));
        }

        // GET: GatePass/DownloadPdf/5
        [Authorize(Roles = "admin,homeowners")]
        public async Task<IActionResult> DownloadPdf(int id)
        {
            var gatePass = await _context.GatePasses.FindAsync(id);
            if (gatePass == null || string.IsNullOrEmpty(gatePass.PdfPath))
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("admin");

            // Only allow homeowners to download their own gate passes
            if (!isAdmin && gatePass.HomeownerId != userId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", gatePass.PdfPath);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/pdf", $"GatePass_{gatePass.PassNumber}.pdf");
        }

        // GET: GatePass/ViewPdf/5
        [Authorize(Roles = "admin,homeowners")]
        public async Task<IActionResult> ViewPdf(int id)
        {
            var gatePass = await _context.GatePasses.FindAsync(id);
            if (gatePass == null || string.IsNullOrEmpty(gatePass.PdfPath))
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("admin");

            // Only allow homeowners to view their own gate passes
            if (!isAdmin && gatePass.HomeownerId != userId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", gatePass.PdfPath);
            if (!System.IO.File.Exists(filePath))
            {
                // If the PDF file doesn't exist, let's regenerate it
                var pdfBytes = await GenerateGatePassPdf(gatePass);
                gatePass.PdfPath = await SavePdfToFile(pdfBytes, gatePass.PassNumber);
                await _context.SaveChangesAsync();
                
                filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", gatePass.PdfPath);
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound();
                }
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            // Return PDF to be displayed in the browser
            return File(memory, "application/pdf");
        }

        private async Task<byte[]> GenerateGatePassPdf(GatePass gatePass)
        {
            // Get the absolute path to the logo file
            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "TramLOGO.jpg");
            bool logoExists = System.IO.File.Exists(logoPath);

            // Define colors for a more elegant design
            var primaryColor = QuestPDF.Helpers.Colors.Blue.Medium;
            var secondaryColor = QuestPDF.Helpers.Colors.Grey.Lighten3;
            var accentColor = QuestPDF.Helpers.Colors.Green.Medium;
            var borderColor = QuestPDF.Helpers.Colors.Grey.Lighten2;
            
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Setup the page with a border
                    page.Size(QuestPDF.Helpers.PageSizes.A4);
                    page.Margin(20);
                    page.Background(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));
                    
                    // Create an elegant header
                    page.Header().Padding(10).Border(1).BorderColor(borderColor).Element(header =>
                    {
                        header.Row(row =>
                        {
                            // Logo section
                            row.RelativeItem(2).Column(logoColumn =>
                            {
                                if (logoExists)
                                {
                                    logoColumn.Item().Height(60).Padding(5).Image(logoPath, ImageScaling.FitArea);
                                }
                                else
                                {
                                    logoColumn.Item().Height(60).Padding(5).Text("Community Portal")
                                        .FontSize(20).FontColor(primaryColor).Bold();
                                }
                            });
                            
                            // Title section with a background color for emphasis
                            row.RelativeItem(3).Column(titleColumn =>
                            {
                                titleColumn.Item().Background(secondaryColor).Padding(10)
                                    .Text("GATE PASS").FontSize(24).Bold().FontColor(primaryColor).AlignCenter();
                                titleColumn.Item().Padding(5).Text($"Pass Number: {gatePass.PassNumber}")
                                    .FontSize(14).Bold().FontColor(accentColor).AlignCenter();
                                titleColumn.Item().Padding(5).Text($"Status: {gatePass.Status}")
                                    .FontSize(12).Bold().FontColor(gatePass.Status.ToString() == "Approved" ? accentColor : QuestPDF.Helpers.Colors.Orange.Medium).AlignCenter();
                            });
                        });
                    });

                    // Create elegant content
                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        // Visitor Information
                        column.Item().PaddingBottom(5).Text("Visitor Information")
                            .FontSize(14).Bold().FontColor(primaryColor).Underline();
                        
                        column.Item().PaddingBottom(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });
                            
                            // Add styles to table
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Visitor Name:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.VisitorName);

                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Purpose:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.Purpose);

                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Visit Date:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.VisitDate.ToString("dddd, MMMM dd, yyyy"));

                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Expected Arrival:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.ExpectedArrivalTime.ToString("hh:mm tt"));

                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Number of Visitors:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.NumberOfVisitors.ToString());

                            if (!string.IsNullOrEmpty(gatePass.VisitorVehicleDetails))
                            {
                                table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                    .Text("Vehicle Details:").Bold();
                                table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                    .Text(gatePass.VisitorVehicleDetails);
                            }

                            if (!string.IsNullOrEmpty(gatePass.ContactNumber))
                            {
                                table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                    .Text("Contact Number:").Bold();
                                table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                    .Text(gatePass.ContactNumber);
                            }
                        });

                        // Homeowner Information
                        column.Item().PaddingTop(15).PaddingBottom(5).Text("Homeowner Information")
                            .FontSize(14).Bold().FontColor(primaryColor).Underline();
                        
                        column.Item().PaddingBottom(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });
                            
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Name:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.Homeowner.User.FullName);

                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("Block Number:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.Homeowner.BlockNumber);

                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text("House Number:").Bold();
                            table.Cell().BorderBottom(1).BorderColor(borderColor).Padding(5)
                                .Text(gatePass.Homeowner.HouseNumber);
                        });

                        // Validity Information with highlight box
                        column.Item().PaddingTop(15).PaddingBottom(5).Text("Validity Information")
                            .FontSize(14).Bold().FontColor(primaryColor).Underline();
                        
                        column.Item().Background(secondaryColor).Border(1).BorderColor(borderColor).Padding(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(3);
                            });
                            
                            table.Cell().Padding(5).Text("Valid From:").Bold();
                            table.Cell().Padding(5).Text(gatePass.VisitDate.ToString("dddd, MMMM dd, yyyy"));

                            table.Cell().Padding(5).Text("Valid Until:").Bold();
                            table.Cell().Padding(5).Text(gatePass.ExpirationDate?.ToString("dddd, MMMM dd, yyyy") ?? "N/A");
                        });

                        // Admin Notes
                        if (!string.IsNullOrEmpty(gatePass.AdminNotes))
                        {
                            column.Item().PaddingTop(15).PaddingBottom(5).Text("Administrator Notes")
                                .FontSize(14).Bold().FontColor(primaryColor).Underline();
                            column.Item().Background(QuestPDF.Helpers.Colors.Yellow.Lighten5).Border(1).BorderColor(borderColor)
                                .Padding(10).Text(gatePass.AdminNotes);
                        }

                        // QR Code section for scanning at gate - Using simpler box instead of QR code
                        column.Item().PaddingTop(20).AlignCenter().Row(row => 
                        {
                            row.RelativeItem().Column(qrColumn => 
                            {
                                string qrText = $"GATE PASS #{gatePass.PassNumber}";
                                qrColumn.Item().AlignCenter().Border(2).BorderColor(primaryColor).Background(secondaryColor)
                                    .Padding(10).Width(150).Height(80).Text(qrText).FontSize(14).Bold().FontColor(primaryColor);
                                qrColumn.Item().AlignCenter().Text("Show at entrance").FontSize(10);
                            });
                        });
                    });

                    // Add a footer with signature section and generation info
                    page.Footer().Padding(10).Column(footer =>
                    {
                        // Signature section
                        footer.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Border(1).BorderColor(borderColor).Height(50).Width(200).Text("");
                                col.Item().Text("Security Personnel Signature").FontSize(8);
                            });
                            
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().AlignRight().Border(1).BorderColor(borderColor).Height(50).Width(200).Text("");
                                col.Item().AlignRight().Text("Homeowner Signature").FontSize(8);
                            });
                        });
                        
                        // Footer text
                        footer.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text($"Generated on: {DateTime.Now:dddd, MMMM dd, yyyy, hh:mm tt}").FontSize(8);
                                col.Item().Text($"Approved by: {gatePass.ApprovedBy}").FontSize(8);
                            });
                            
                            row.RelativeItem().AlignRight().Text(text =>
                            {
                                text.Span("Page ").FontSize(8);
                                text.CurrentPageNumber().FontSize(8);
                                text.Span(" of ").FontSize(8);
                                text.TotalPages().FontSize(8);
                            });
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private async Task<string> SavePdfToFile(byte[] pdfBytes, string passNumber)
        {
            var fileName = $"GatePass_{passNumber}.pdf";
            var filePath = Path.Combine("uploads", "gatepasses", fileName);
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath);

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Save file
            await System.IO.File.WriteAllBytesAsync(fullPath, pdfBytes);

            return filePath;
        }

        // POST: GatePass/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var gatePass = await _context.GatePasses.FindAsync(id);
            if (gatePass == null)
            {
                return NotFound();
            }

            // Soft delete
            gatePass.IsDeleted = true;
            gatePass.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Notify the homeowner that their gate pass was deleted
            if (gatePass.Status == GatePassStatus.Pending)
            {
                await _notificationService.CreateNotificationAsync(
                    gatePass.HomeownerId,
                    "Gate Pass Deleted",
                    $"Your gate pass request for visitor {gatePass.VisitorName} has been deleted by an administrator.",
                    "/GatePass/Index",
                    NotificationType.General,
                    User.FindFirstValue(ClaimTypes.NameIdentifier));
            }

            TempData["SuccessMessage"] = "Gate pass deleted successfully.";
            return RedirectToAction(nameof(Admin));
        }

        // Validation method for visit date
        [HttpGet]
        public IActionResult ValidateVisitDate(DateTime visitDate)
        {
            if (visitDate.Date < DateTime.Today)
            {
                return Json("Visit date must be today or in the future.");
            }

            return Json(true);
        }

        // Helper method to generate a unique pass number
        private string GeneratePassNumber()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var random = new Random();
            var randomPart = random.Next(1000, 9999).ToString();
            return $"GP-{timestamp}-{randomPart}";
        }
    }
} 