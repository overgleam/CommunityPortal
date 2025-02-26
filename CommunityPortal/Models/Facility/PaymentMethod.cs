using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models.Facility
{
    public class PaymentMethod
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(100)]
        public string Type { get; set; } // "Bank", "GCash", etc.

        [Required]
        [StringLength(255)]
        public string Details { get; set; } // Account number, QR code file, etc.

        public string? QRCodeFileName { get; set; }

        [StringLength(1000)]
        public string? Instructions { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
} 