using Microsoft.AspNetCore.SignalR;
using CommunityPortal.Data;
using CommunityPortal.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CommunityPortal.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public NotificationHub(ApplicationDbContext context)
        {
            _context = context;
        }

        // Register the connection with the user's ID
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }

        // When disconnecting
        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
            await base.OnDisconnectedAsync(exception);
        }

        // Method to send a notification to a specific user
        public async Task SendNotification(string recipientId, string title, string message, string link = null, NotificationType type = NotificationType.General)
        {
            var senderId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Create and save the notification to the database
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Link = link,
                SenderId = senderId,
                RecipientId = recipientId,
                Type = type,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Load sender information
            var sender = await _context.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .Include(u => u.Homeowner)
                .FirstOrDefaultAsync(u => u.Id == senderId);

            var senderName = sender?.FullName ?? "System";

            // Send the notification to the recipient in real-time
            await Clients.Group(recipientId).SendAsync("ReceiveNotification", notification.Id, title, message, senderName, link, type.ToString(), notification.CreatedAt.ToString("g"));
        }

        // Method to broadcast a notification to all connected users
        public async Task BroadcastNotification(string title, string message, string link = null, NotificationType type = NotificationType.System)
        {
            var senderId = Context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Check if the user has admin or staff role to broadcast
            var user = await _context.Users
                .Include(u => u.Administrator)
                .Include(u => u.Staff)
                .FirstOrDefaultAsync(u => u.Id == senderId);
                
            if (user?.Administrator == null && user?.Staff == null)
            {
                throw new HubException("You don't have permission to broadcast notifications.");
            }

            var senderName = user?.FullName ?? "System";

            // Get all active users (not deleted)
            var activeUsers = await _context.Users
                .Where(u => !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            // Create a notification for each user
            foreach (var userId in activeUsers)
            {
                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    Link = link,
                    SenderId = senderId,
                    RecipientId = userId,
                    Type = type,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            // Broadcast to all connected clients
            await Clients.All.SendAsync("ReceiveNotification", 0, title, message, senderName, link, type.ToString(), DateTime.UtcNow.ToString("g"));
        }
    }
} 