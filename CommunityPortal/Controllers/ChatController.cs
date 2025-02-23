using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Data;
using CommunityPortal.Models.Chat;
using CommunityPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace CommunityPortal.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ChatController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // Display list of users to chat with
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var users = await _userManager.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .Where(u => u.Id != currentUser.Id && !u.IsDeleted)
                .ToListAsync();

            return View(users);
        }

        // Display chat interface with a specific user
        public async Task<IActionResult> Message(string recipientId)
        {
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                return BadRequest("Recipient ID is required.");
            }

            var currentUserId = _userManager.GetUserId(User);
            var currentUser = await _context.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == currentUserId);

            var recipient = await _context.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == recipientId && !u.IsDeleted);

            if (recipient == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            // Retrieve the latest 20 messages
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => 
                    ((m.SenderId == currentUser.Id && m.RecipientId == recipientId) ||
                    (m.SenderId == recipientId && m.RecipientId == currentUser.Id)) &&
                    !m.Sender.IsDeleted && !m.Recipient.IsDeleted)
                .OrderByDescending(m => m.Timestamp)
                .Take(20)
                .OrderBy(m => m.Timestamp) 
                .ToListAsync();

            var model = new ChatViewModel
            {
                RecipientId = recipientId,
                RecipientUsername = GetFullName(recipient),
                CurrentUserFullName = GetFullName(currentUser),
                Messages = messages.Select(m => new ChatMessageViewModel
                {
                    Id = m.Id,
                    SenderUsername = m.Sender.UserName,
                    SenderFullName = GetFullName(m.Sender),
                    Message = m.Message,
                    Timestamp = m.Timestamp.ToLocalTime().ToString("g")
                }).ToList()
            };

            return View(model);
        }

        // New Action to Load More Messages
        [HttpGet]
        public async Task<IActionResult> LoadMoreMessages(string recipientId, int skip, int take = 20)
        {
            if (string.IsNullOrWhiteSpace(recipientId))
            {
                return BadRequest("Recipient ID is required.");
            }

            var currentUserId = _userManager.GetUserId(User);

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Recipient)
                .Where(m => 
                    ((m.SenderId == currentUserId && m.RecipientId == recipientId) ||
                    (m.SenderId == recipientId && m.RecipientId == currentUserId)) &&
                    !m.Sender.IsDeleted && !m.Recipient.IsDeleted)
                .OrderByDescending(m => m.Timestamp)
                .Skip(skip)
                .Take(take)
                .OrderBy(m => m.Timestamp) 
                .ToListAsync();

            var messageViewModels = messages.Select(m => new ChatMessageViewModel
            {
                Id = m.Id,
                SenderUsername = m.Sender.UserName,
                SenderFullName = GetFullName(m.Sender),
                Message = m.Message,
                Timestamp = m.Timestamp.ToLocalTime().ToString("g")
            }).ToList();

            return PartialView("_ChatMessagesPartial", messageViewModels);
        }

        private string GetFullName(ApplicationUser user)
        {
            if (user == null)
                return "Unknown";

            if (user.Administrator != null)
            {
                return $"{user.Administrator.FirstName} {user.Administrator.LastName}";
            }
            else if (user.Staff != null)
            {
                return $"{user.Staff.FirstName} {user.Staff.LastName}";
            }
            else if (user.Homeowner != null)
            {
                return $"{user.Homeowner.FirstName} {user.Homeowner.LastName}";
            }
            else
            {
                return user.UserName; // Fallback to username (email)
            }
        }
    }
}