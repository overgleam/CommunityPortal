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
                .Where(u => u.Id != currentUser.Id)
                .ToListAsync();

            return View(users);
        }

        // Display chat interface with a specific user
        public async Task<IActionResult> Chat(string recipientId)
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
                .FirstOrDefaultAsync(u => u.Id == recipientId);

            if (recipient == null)
            {
                return NotFound("Recipient not found.");
            }

            // Retrieve chat history
            var messages = await _context.ChatMessages
                .Where(m => (m.SenderId == currentUser.Id && m.RecipientId == recipientId) ||
                            (m.SenderId == recipientId && m.RecipientId == currentUser.Id))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            var model = new ChatViewModel
            {
                RecipientId = recipientId,
                RecipientUsername = GetFullName(recipient),
                CurrentUserFullName = GetFullName(currentUser),
                Messages = messages.Select(m => new ChatMessageViewModel
                {
                    SenderFullName = m.SenderId == currentUser.Id ? GetFullName(currentUser) : GetFullName(recipient),
                    Message = m.Message,
                    Timestamp = m.Timestamp.ToLocalTime().ToString("g")
                }).ToList()
            };

            return View(model);
        }


        private string GetFullName(ApplicationUser user)
        {
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