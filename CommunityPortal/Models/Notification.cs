using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; }
        
        [Required]
        public string Message { get; set; }
        
        public string? Link { get; set; }
        
        [Required]
        public string RecipientId { get; set; }
        
        [ForeignKey("RecipientId")]
        public ApplicationUser Recipient { get; set; }
        
        public string? SenderId { get; set; }
        
        [ForeignKey("SenderId")]
        public ApplicationUser Sender { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ReadAt { get; set; }
        
        // Soft Delete Properties
        public bool IsDeleted { get; set; } = false;
        
        public DateTime? DeletedAt { get; set; }
        
        // Notification Type for categorization
        public NotificationType Type { get; set; } = NotificationType.General;
    }
    
    public enum NotificationType
    {
        General,
        Alert,
        Message,
        Event,
        Billing,
        ServiceRequest,
        Forum,
        Poll,
        Document,
        System
    }
} 