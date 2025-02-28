using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityPortal.Models.Facility;
using Microsoft.AspNetCore.Http;

namespace CommunityPortal.Models.Billing
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BillId { get; set; }

        [ForeignKey("BillId")]
        public Bill Bill { get; set; }

        [Required]
        public string HomeownerId { get; set; }

        [ForeignKey("HomeownerId")]
        public Homeowner Homeowner { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        [Required]
        public decimal Amount { get; set; }

        [Required]
        public int PaymentMethodId { get; set; }

        [ForeignKey("PaymentMethodId")]
        public PaymentMethod PaymentMethod { get; set; }

        [StringLength(100)]
        [Required]
        public string TransactionReference { get; set; }

        public string? Notes { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Verified, Rejected

        // For tracking payment proof uploads
        public string? PaymentProofFile { get; set; }

        public string? VerifiedBy { get; set; } // Admin/Staff userId who verified the payment
        public DateTime? VerifiedAt { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        [NotMapped]
        public IFormFile? PaymentProofImage { get; set; }
    }
} 