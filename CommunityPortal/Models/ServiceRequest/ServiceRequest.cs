using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.ServiceRequest
{
    public class ServiceRequest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(100, ErrorMessage = "Title cannot be longer than 100 characters")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot be longer than 1000 characters")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [StringLength(100, ErrorMessage = "Location cannot be longer than 100 characters")]
        public string Location { get; set; }

        [Required(ErrorMessage = "Preferred schedule is required")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Preferred Schedule")]
        public DateTime PreferredSchedule { get; set; }

        [Required]
        public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;

        public string? RejectionReason { get; set; }

        public string? CancellationReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public DateTime? RejectedAt { get; set; }

        public string HomeownerId { get; set; }
        
        [ForeignKey("HomeownerId")]
        public virtual ApplicationUser? Homeowner { get; set; }

        [Required(ErrorMessage = "Service category is required")]
        [Display(Name = "Service Category")]
        public int ServiceCategoryId { get; set; }
        
        [ForeignKey("ServiceCategoryId")]
        public virtual ServiceCategory? ServiceCategory { get; set; }

        public virtual ServiceFeedback? Feedback { get; set; }

        public virtual ICollection<ServiceStaffAssignment> StaffAssignments { get; set; } = new List<ServiceStaffAssignment>();
    }

    public enum ServiceRequestStatus
    {
        [Display(Name = "Pending")]
        Pending,
        [Display(Name = "Assigned")]
        Assigned,
        [Display(Name = "In Progress")]
        InProgress,
        [Display(Name = "Completed")]
        Completed,
        [Display(Name = "Rejected")]
        Rejected,
        [Display(Name = "Cancelled")]
        Cancelled
    }
} 