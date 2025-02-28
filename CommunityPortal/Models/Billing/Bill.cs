using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Billing
{
    public class Bill
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string HomeownerId { get; set; }

        [ForeignKey("HomeownerId")]
        public Homeowner Homeowner { get; set; }

        [Required]
        public DateTime BillingDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAmount { get; set; }

        public string BillingPeriod { get; set; } // e.g., "January 2023"

        public DateTime? PaidDate { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Unpaid"; // Unpaid, Paid, Partially Paid, Overdue

        public bool IsPenaltyApplied { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PenaltyAmount { get; set; } = 0;

        public string? PaymentReference { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
} 