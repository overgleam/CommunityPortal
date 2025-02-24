using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityPortal.Models;

namespace CommunityPortal.Models.Event
{
    public class Event
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000, ErrorMessage = "Description cannot be longer than 2000 characters")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Start date and time is required")]
        public DateTime StartDateTime { get; set; }

        [Required(ErrorMessage = "End date and time is required")]
        public DateTime EndDateTime { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Maximum attendees must be a positive number")]
        public int? MaxAttendees { get; set; }

        public bool RequiresRegistration { get; set; }

        [StringLength(500)]
        public string? RegistrationInstructions { get; set; }

        public EventStatus Status { get; set; } = EventStatus.Scheduled;

        [StringLength(500)]
        public string? CancellationReason { get; set; }

        public bool IsHighPriority { get; set; }

        public string? ImageUrl { get; set; }

        [Required]
        public string CreatorId { get; set; }

        public virtual ApplicationUser Creator { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public bool IsDeleted { get; set; } = false;
        
        public DateTime? DeletedAt { get; set; }

        [NotMapped]
        public string StatusBadgeClass => Status switch
        {
            EventStatus.Scheduled => "badge-primary",
            EventStatus.InProgress => "badge-success",
            EventStatus.Completed => "badge-info",
            EventStatus.Cancelled => "badge-danger",
            EventStatus.Postponed => "badge-warning",
            _ => "badge-secondary"
        };

        [NotMapped]
        public string CalendarEventColor => Status switch
        {
            EventStatus.Scheduled => "#007bff",  // Blue
            EventStatus.InProgress => "#28a745", // Green
            EventStatus.Completed => "#17a2b8",  // Cyan
            EventStatus.Cancelled => "#dc3545",  // Red
            EventStatus.Postponed => "#ffc107",  // Yellow
            _ => "#6c757d"  // Gray
        };

        [NotMapped]
        public string StatusText => Status switch
        {
            EventStatus.Scheduled => "This event is scheduled to take place as planned.",
            EventStatus.InProgress => "This event is currently in progress.",
            EventStatus.Completed => "This event has been completed.",
            EventStatus.Cancelled => $"This event has been cancelled. Reason: {CancellationReason ?? "No reason provided"}",
            EventStatus.Postponed => "This event has been postponed.",
            _ => "Status unknown"
        };
    }

    public enum EventStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Postponed
    }
} 