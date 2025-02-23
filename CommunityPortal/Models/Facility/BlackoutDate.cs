using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Facility
{
    public class BlackoutDate
    {
        [Key]
        public int Id { get; set; }

        public int? FacilityId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public string Reason { get; set; }

        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation property
        [ForeignKey("FacilityId")]
        public virtual Facility? Facility { get; set; }
    }
} 