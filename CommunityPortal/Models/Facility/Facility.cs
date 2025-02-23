using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Facility
{
    public class Facility
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public FacilityType Type { get; set; }

        [Required]
        public TimeSpan OpeningTime { get; set; }

        [Required]
        public TimeSpan ClosingTime { get; set; }

        [Required]
        public int MaximumOccupancy { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PricePerHour { get; set; }

        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public virtual ICollection<FacilityReservation> Reservations { get; set; }
        public virtual ICollection<BlackoutDate> BlackoutDates { get; set; }

        public Facility()
        {
            Reservations = new HashSet<FacilityReservation>();
            BlackoutDates = new HashSet<BlackoutDate>();
        }
    }
} 