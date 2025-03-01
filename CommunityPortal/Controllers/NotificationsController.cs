using CommunityPortal.Data;
using CommunityPortal.Hubs;
using CommunityPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public NotificationsController(ApplicationDbContext context, IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _notificationHub = notificationHub;
        }

        // GET: Notifications
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _context.Notifications
                .Include(n => n.Sender)
                .Where(n => n.RecipientId == userId && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notifications);
        }

        // GET: Notifications/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notification = await _context.Notifications
                .Include(n => n.Sender)
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == userId && !n.IsDeleted);

            if (notification == null)
            {
                return NotFound();
            }

            // Mark as read if not already
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return View(notification);
        }

        // GET: Notifications/Create
        [Authorize(Roles = "admin,staff")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Notifications/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> Create([Bind("Title,Message,Link,RecipientId,Type")] Notification notification)
        {
            if (ModelState.IsValid)
            {
                var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                notification.SenderId = senderId;
                notification.CreatedAt = DateTime.UtcNow;
                
                _context.Add(notification);
                await _context.SaveChangesAsync();

                // Send real-time notification
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                await _notificationHub.Clients.User(notification.RecipientId)
                    .SendAsync("ReceiveNotification", notification.Id, notification.Title, notification.Message, 
                    sender?.FullName ?? "System", notification.Link, notification.Type.ToString(), notification.CreatedAt.ToString("g"));

                return RedirectToAction(nameof(Index));
            }
            return View(notification);
        }

        // POST: Notifications/CreateBroadcast
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,staff")]
        public async Task<IActionResult> CreateBroadcast([Bind("Title,Message,Link,Type")] Notification notification)
        {
            if (ModelState.IsValid)
            {
                var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                // Get all active users (not deleted)
                var activeUsers = await _context.Users
                    .Where(u => !u.IsDeleted)
                    .Select(u => u.Id)
                    .ToListAsync();

                var createdNotifications = new List<Notification>();
                
                // Create a notification for each user
                foreach (var userId in activeUsers)
                {
                    var newNotification = new Notification
                    {
                        Title = notification.Title,
                        Message = notification.Message,
                        Link = notification.Link,
                        SenderId = senderId,
                        RecipientId = userId,
                        Type = notification.Type,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Notifications.Add(newNotification);
                    createdNotifications.Add(newNotification);
                }

                await _context.SaveChangesAsync();

                // Broadcast to all connected clients
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                await _notificationHub.Clients.All
                    .SendAsync("ReceiveNotification", 0, notification.Title, notification.Message,
                    sender?.FullName ?? "System", notification.Link, notification.Type.ToString(), DateTime.UtcNow.ToString("g"));

                return RedirectToAction(nameof(Index));
            }
            return View("Create", notification);
        }

        // POST: Notifications/MarkAsRead/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == userId && !n.IsDeleted);

            if (notification == null)
            {
                return NotFound();
            }

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: Notifications/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var unreadNotifications = await _context.Notifications
                .Where(n => n.RecipientId == userId && !n.IsRead && !n.IsDeleted)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: Notifications/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == userId);

            if (notification == null)
            {
                return NotFound();
            }

            // Soft delete
            notification.IsDeleted = true;
            notification.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: Notifications/DeleteAll
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _context.Notifications
                .Where(n => n.RecipientId == userId && !n.IsDeleted)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsDeleted = true;
                notification.DeletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Notifications/GetUnreadCount
        [HttpGet]
        public async Task<JsonResult> GetUnreadCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var count = await _context.Notifications
                .CountAsync(n => n.RecipientId == userId && !n.IsRead && !n.IsDeleted);

            return Json(new { count });
        }

        // AJAX endpoint for getting notifications for dropdown
        [HttpGet]
        public async Task<IActionResult> GetNotificationsDropdown(int count = 5)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _context.Notifications
                .Include(n => n.Sender)
                .Where(n => n.RecipientId == userId && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();

            return PartialView("_NotificationsDropdown", notifications);
        }
    }
} 