using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Billing
{
    public class BillingSettings
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        [Required]
        public string Name { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LateFeePercentage { get; set; } = 5; // Default to 5%

        public int LateFeeDays { get; set; } = 30; // Default to 30 days
        
        public int BillingCycleDay { get; set; } = 1; // Default to the 1st day of the month
        
        public int PaymentDueDays { get; set; } = 15; // Default to 15 days after billing date

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public string CreatedBy { get; set; } // Admin userId who created the settings
        public string? UpdatedBy { get; set; } // Admin userId who last updated the settings
    }
} 