using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Data;
using CommunityPortal.Models;
using CommunityPortal.Models.Documents;
using CommunityPortal.Models.Enums;
using CommunityPortal.Services;
using System.Security.Claims;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly NotificationService _notificationService;
        private readonly long _fileSizeLimit = 10 * 1024 * 1024; // 10MB
        private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png" };

        public DocumentsController(
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

        // GET: Documents
        public async Task<IActionResult> Index(DocumentCategory? category, string searchTerm, int page = 1, int pageSize = 9)
        {
            var query = _context.Documents
                .Where(d => !d.IsDeleted)
                .AsQueryable();

            if (category.HasValue)
            {
                query = query.Where(d => d.Category == category.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(d => d.Title.ToLower().Contains(searchTerm) || 
                                        (d.Description != null && d.Description.ToLower().Contains(searchTerm)));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var documents = await query
                .OrderByDescending(d => d.UploadDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(d => d.UploadedBy)
                .ToListAsync();

            var viewModel = new DocumentListViewModel
            {
                Documents = documents,
                CategoryFilter = category,
                SearchTerm = searchTerm,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        // GET: Documents/CommunityGuidelines
        public async Task<IActionResult> CommunityGuidelines()
        {
            var documents = await _context.Documents
                .Where(d => !d.IsDeleted && d.Category == DocumentCategory.CommunityGuidelines)
                .OrderByDescending(d => d.UploadDate)
                .Include(d => d.UploadedBy)
                .ToListAsync();

            return View(documents);
        }

        // GET: Documents/EmergencyContacts
        public async Task<IActionResult> EmergencyContacts()
        {
            var documents = await _context.Documents
                .Where(d => !d.IsDeleted && d.Category == DocumentCategory.EmergencyContacts)
                .OrderByDescending(d => d.UploadDate)
                .Include(d => d.UploadedBy)
                .ToListAsync();

            return View(documents);
        }

        // GET: Documents/Upload
        [Authorize(Roles = "admin")]
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Documents/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Upload(DocumentUploadViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate file size
                if (model.File.Length > _fileSizeLimit)
                {
                    ModelState.AddModelError("File", $"File size exceeds the limit of {_fileSizeLimit / 1024 / 1024}MB");
                    return View(model);
                }

                // Validate file extension
                var extension = Path.GetExtension(model.File.FileName).ToLowerInvariant();
                if (!_allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("File", $"File type not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");
                    return View(model);
                }

                var user = await _userManager.GetUserAsync(User);
                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "documents");
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(fileStream);
                }

                var document = new Document
                {
                    Title = model.Title,
                    Description = model.Description,
                    Category = model.Category,
                    FilePath = $"/uploads/documents/{uniqueFileName}",
                    FileName = model.File.FileName,
                    FileType = extension,
                    FileSizeInKB = model.File.Length / 1024,
                    UploadDate = DateTime.Now,
                    UploadedById = user.Id
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                // Broadcast a notification to all users about the new document
                await _notificationService.CreateDocumentNotificationAsync(
                    recipientId: document.UploadedById, // This is a placeholder as we'll broadcast to all
                    documentId: document.Id,
                    documentName: document.Title,
                    action: "Uploaded",
                    senderId: user.Id
                );
                
                // Broadcast to all users
                await _notificationService.BroadcastNotificationAsync(
                    title: "New Document Available",
                    message: $"A new document has been uploaded: {document.Title}",
                    link: $"/Documents/Download/{document.Id}",
                    type: NotificationType.Document,
                    senderId: user.Id
                );

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: Documents/Download/5
        public async Task<IActionResult> Download(int id)
        {
            var document = await _context.Documents
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (document == null)
            {
                return NotFound();
            }

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, document.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            
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

            return File(memory, GetContentType(document.FileType), document.FileName);
        }

        // GET: Documents/Delete/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var document = await _context.Documents
                .Include(d => d.UploadedBy)
                .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);

            if (document == null)
            {
                return NotFound();
            }

            return View(document);
        }

        // POST: Documents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            document.IsDeleted = true;
            document.DeletedDate = DateTime.Now;
            document.DeletedById = user.Id;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Manage));
        }

        // GET: Documents/Restore/5
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Restore(int id)
        {
            var document = await _context.Documents
                .Include(d => d.DeletedBy)
                .FirstOrDefaultAsync(d => d.Id == id && d.IsDeleted);

            if (document == null)
            {
                return NotFound();
            }

            return View(document);
        }

        // POST: Documents/Restore/5
        [HttpPost, ActionName("Restore")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> RestoreConfirmed(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            document.IsDeleted = false;
            document.DeletedDate = null;
            document.DeletedById = null;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Manage));
        }

        // GET: Documents/Manage
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> Manage(DocumentCategory? category, string searchTerm, bool showDeleted = false, int page = 1, int pageSize = 10)
        {
            var query = _context.Documents.AsQueryable();

            if (!showDeleted)
            {
                query = query.Where(d => !d.IsDeleted);
            }

            if (category.HasValue)
            {
                query = query.Where(d => d.Category == category.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(d => d.Title.ToLower().Contains(searchTerm) || 
                                        (d.Description != null && d.Description.ToLower().Contains(searchTerm)));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var documents = await query
                .OrderByDescending(d => d.UploadDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(d => d.UploadedBy)
                .Include(d => d.DeletedBy)
                .ToListAsync();

            var viewModel = new DocumentListViewModel
            {
                Documents = documents,
                CategoryFilter = category,
                SearchTerm = searchTerm,
                ShowDeleted = showDeleted,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        private string GetContentType(string fileExtension)
        {
            switch (fileExtension.ToLower())
            {
                case ".pdf":
                    return "application/pdf";
                case ".doc":
                case ".docx":
                    return "application/msword";
                case ".xls":
                case ".xlsx":
                    return "application/vnd.ms-excel";
                case ".ppt":
                case ".pptx":
                    return "application/vnd.ms-powerpoint";
                case ".txt":
                    return "text/plain";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".png":
                    return "image/png";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
