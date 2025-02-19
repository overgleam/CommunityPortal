using System;
using System.ComponentModel.DataAnnotations;
using CommunityPortal.Models.Enums; // Import the namespace for FeedbackStatus

namespace CommunityPortal.Models
{
    public class Feedback
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } // Foreign key to ApplicationUser

        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Use the enum for the status with a default of New
        public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

        public ApplicationUser User { get; set; } // Navigation property
    }
}