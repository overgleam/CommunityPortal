using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Facility
{
    public class FacilityReservation
    {
        [Key]
        public int Id { get; set; }

        public int? FacilityId { get; set; }

        public string? UserId { get; set; }

        [Required]
        public DateTime ReservationDate { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        [Required]
        public int GuestCount { get; set; }

        public string? SpecialRequests { get; set; }

        [Required]
        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public string? RejectionReason { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        public bool IsPaid { get; set; } = false;
        public string? PaymentTransactionId { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        [ForeignKey("FacilityId")]
        public virtual Facility? Facility { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }

    public enum ReservationStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        Completed
    }
} 