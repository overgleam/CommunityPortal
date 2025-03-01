using CommunityPortal.Data;
using CommunityPortal.Hubs;
using CommunityPortal.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CommunityPortal.Services
{
    public class NotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        public NotificationService(ApplicationDbContext context, IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _notificationHub = notificationHub;
        }

        /// <summary>
        /// Creates a notification and sends it in real-time to the recipient
        /// </summary>
        public async Task CreateNotificationAsync(string recipientId, string title, string message, string? link = null, NotificationType type = NotificationType.General, string? senderId = null)
        {
            // Create the notification entity
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Link = link,
                RecipientId = recipientId,
                SenderId = senderId,
                Type = type,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Get sender name if senderId is provided
            string senderName = "System";
            if (!string.IsNullOrEmpty(senderId))
            {
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                senderName = sender?.FullName ?? "System";
            }

            // Send the notification in real-time
            await _notificationHub.Clients.User(recipientId)
                .SendAsync("ReceiveNotification", notification.Id, title, message, senderName, link, type.ToString(), notification.CreatedAt.ToString("g"));
        }

        /// <summary>
        /// Creates a service request notification
        /// </summary>
        public async Task CreateServiceRequestNotificationAsync(string recipientId, int serviceRequestId, string action, string? senderId = null)
        {
            string title = $"Service Request {action}";
            string message = $"Your service request #{serviceRequestId} has been {action.ToLower()}.";
            string link = $"/ServiceRequest/Details/{serviceRequestId}";
            
            await CreateNotificationAsync(recipientId, title, message, link, NotificationType.ServiceRequest, senderId);
        }

        /// <summary>
        /// Creates a facility reservation notification
        /// </summary>
        public async Task CreateFacilityReservationNotificationAsync(string recipientId, int reservationId, string facilityName, string status, string? senderId = null)
        {
            string title = $"Facility Reservation {status}";
            string message = $"Your reservation for {facilityName} has been {status.ToLower()}.";
            string link = $"/Facility/ReservationDetails/{reservationId}";
            
            await CreateNotificationAsync(recipientId, title, message, link, NotificationType.Event, senderId);
        }

        /// <summary>
        /// Creates a billing notification
        /// </summary>
        public async Task CreateBillingNotificationAsync(string recipientId, int billId, decimal amount, string action, string? senderId = null)
        {
            string title = $"Billing {action}";
            string message = $"A bill for ${amount:N2} has been {action.ToLower()}.";
            string link = $"/Billing/Details/{billId}";
            
            await CreateNotificationAsync(recipientId, title, message, link, NotificationType.Billing, senderId);
        }

        /// <summary>
        /// Creates a forum notification (new post, comment, etc.)
        /// </summary>
        public async Task CreateForumNotificationAsync(string recipientId, int postId, string action, string title, string? senderId = null)
        {
            string notificationTitle = $"Forum {action}";
            string message = $"New {action.ToLower()} on forum post: {title}";
            string link = $"/Forum/Post/{postId}";
            
            await CreateNotificationAsync(recipientId, notificationTitle, message, link, NotificationType.Forum, senderId);
        }

        /// <summary>
        /// Creates a message notification
        /// </summary>
        public async Task CreateMessageNotificationAsync(string recipientId, string senderName, string? senderId = null)
        {
            string title = "New Message";
            string message = $"You have received a new message from {senderName}.";
            string link = "/Chat";
            
            await CreateNotificationAsync(recipientId, title, message, link, NotificationType.Message, senderId);
        }

        /// <summary>
        /// Creates a document notification
        /// </summary>
        public async Task CreateDocumentNotificationAsync(string recipientId, int documentId, string documentName, string action, string? senderId = null)
        {
            string title = $"Document {action}";
            string message = $"Document '{documentName}' has been {action.ToLower()}.";
            string link = $"/Documents/Details/{documentId}";
            
            await CreateNotificationAsync(recipientId, title, message, link, NotificationType.Document, senderId);
        }

        /// <summary>
        /// Broadcasts a notification to all users
        /// </summary>
        public async Task BroadcastNotificationAsync(string title, string message, string? link = null, NotificationType type = NotificationType.System, string? senderId = null)
        {
            // Get all active users
            var activeUsers = await _context.Users
                .Where(u => !u.IsDeleted)
                .Select(u => u.Id)
                .ToListAsync();

            // Get sender name if senderId is provided
            string senderName = "System";
            if (!string.IsNullOrEmpty(senderId))
            {
                var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                senderName = sender?.FullName ?? "System";
            }

            // Create a notification for each user
            foreach (var userId in activeUsers)
            {
                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    Link = link,
                    RecipientId = userId,
                    SenderId = senderId,
                    Type = type,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();

            // Broadcast to all connected clients
            await _notificationHub.Clients.All
                .SendAsync("ReceiveNotification", 0, title, message, senderName, link, type.ToString(), DateTime.UtcNow.ToString("g"));
        }
    }
} 