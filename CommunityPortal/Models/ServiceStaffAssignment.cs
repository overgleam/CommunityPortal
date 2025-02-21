using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models
{
    public class ServiceStaffAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ServiceRequestId { get; set; }
        
        [ForeignKey("ServiceRequestId")]
        public virtual ServiceRequest ServiceRequest { get; set; }

        [Required]
        public string StaffId { get; set; }
        
        [ForeignKey("StaffId")]
        public virtual ApplicationUser Staff { get; set; }

        public bool IsAccepted { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        public DateTime? AcceptedAt { get; set; }
    }
} 