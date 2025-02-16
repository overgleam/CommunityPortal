using System;
using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; }

        [Required]
        public string RecipientId { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public ApplicationUser Sender { get; set; }
        public ApplicationUser Recipient { get; set; }
    }
}