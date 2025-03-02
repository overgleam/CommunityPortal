using Microsoft.AspNetCore.SignalR;
using CommunityPortal.Data;
using CommunityPortal.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Services;

namespace CommunityPortal.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public ChatHub(ApplicationDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // Register the connection with the user's ID
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }

        // Explicitly handle disconnection
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Send a message to a specific user
        public async Task SendMessage(string recipientId, string message)
        {
            var senderId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Load sender with related profile
            var sender = await _context.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == senderId);

            var senderFullName = GetFullName(sender);

            // Save message to database
            var chatMessage = new ChatMessage
            {
                SenderId = senderId,
                RecipientId = recipientId,
                Message = message
            };
            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // Send message to recipient via SignalR
            await Clients.Group(recipientId).SendAsync("ReceiveMessage", senderFullName, message, chatMessage.Timestamp.ToLocalTime().ToString("g"));
            
            // Send notification to recipient
            await _notificationService.CreateMessageNotificationAsync(
                recipientId: recipientId,
                senderName: senderFullName,
                senderId: senderId
            );
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