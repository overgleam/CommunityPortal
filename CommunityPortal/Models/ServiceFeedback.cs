using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models
{
    public class ServiceFeedback
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Please provide a rating")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars")]
        [Display(Name = "Rating (1-5 stars)")]
        public int Rating { get; set; }

        [StringLength(1000, ErrorMessage = "Comment cannot be longer than 1000 characters")]
        [Display(Name = "Additional Comments")]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public int ServiceRequestId { get; set; }
        
        [ForeignKey("ServiceRequestId")]
        public virtual ServiceRequest? ServiceRequest { get; set; }

        public string HomeownerId { get; set; }
        
        [ForeignKey("HomeownerId")]
        public virtual ApplicationUser? Homeowner { get; set; }
    }
} 