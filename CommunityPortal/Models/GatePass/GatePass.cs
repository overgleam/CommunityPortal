using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.GatePass
{
    public class GatePass
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string HomeownerId { get; set; }

        [ForeignKey("HomeownerId")]
        public Homeowner Homeowner { get; set; }

        [Required]
        [StringLength(100)]
        public string VisitorName { get; set; }

        [Required]
        public string Purpose { get; set; }

        [Required]
        public DateTime VisitDate { get; set; }

        [Required]
        public DateTime ExpectedArrivalTime { get; set; }

        [Required]
        public int NumberOfVisitors { get; set; }

        [StringLength(100)]
        public string? VisitorVehicleDetails { get; set; }

        [StringLength(50)]
        public string? ContactNumber { get; set; }

        [StringLength(50)]
        public string? PassNumber { get; set; }

        public DateTime? ExpirationDate { get; set; }

        [Required]
        public GatePassStatus Status { get; set; } = GatePassStatus.Pending;

        public string? AdminNotes { get; set; }

        [StringLength(50)]
        public string? ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        [StringLength(255)]
        public string? PdfPath { get; set; }
    }
} 