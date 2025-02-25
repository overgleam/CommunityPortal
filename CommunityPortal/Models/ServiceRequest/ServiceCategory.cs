using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models.ServiceRequest
{
    public class ServiceCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsDeleted { get; set; } = false;
        
        public DateTime? DeletedAt { get; set; }

        public virtual ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    }
} 