using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Billing
{
    public class FeeType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DefaultAmount { get; set; } = 0;

        public string Category { get; set; } // Association Dues, Service Request, Facility Upkeep, Security and Maintenance, Emergency Fund, Administrative

        public bool IsRecurring { get; set; } = false;

        public bool IsRequired { get; set; } = false;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
    }
} 