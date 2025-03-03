using System;
using System.ComponentModel.DataAnnotations;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.GatePass
{
    public class GatePassAdminViewModel
    {
        public int Id { get; set; }
        
        [Display(Name = "Homeowner")]
        public string HomeownerName { get; set; }
        
        [Display(Name = "Block Number")]
        public int BlockNumber { get; set; }
        
        [Display(Name = "House Number")]
        public int HouseNumber { get; set; }
        
        [Display(Name = "Visitor Name")]
        public string VisitorName { get; set; }
        
        [Display(Name = "Purpose of Visit")]
        public string Purpose { get; set; }
        
        [Display(Name = "Visit Date")]
        [DataType(DataType.Date)]
        public DateTime VisitDate { get; set; }
        
        [Display(Name = "Expected Arrival Time")]
        [DataType(DataType.DateTime)]
        public DateTime ExpectedArrivalTime { get; set; }
        
        [Display(Name = "Number of Visitors")]
        public int NumberOfVisitors { get; set; }
        
        [Display(Name = "Vehicle Details")]
        public string VisitorVehicleDetails { get; set; }
        
        [Display(Name = "Contact Number")]
        public string ContactNumber { get; set; }
        
        [Display(Name = "Pass Number")]
        [StringLength(50, ErrorMessage = "Pass number cannot exceed 50 characters")]
        public string PassNumber { get; set; }
        
        [Display(Name = "Expiration Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ExpirationDate { get; set; }
        
        [Display(Name = "Status")]
        public GatePassStatus Status { get; set; }
        
        [Display(Name = "Admin Notes")]
        [StringLength(500, ErrorMessage = "Admin notes cannot exceed 500 characters")]
        public string AdminNotes { get; set; }
        
        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; }
        
        public string HomeownerId { get; set; }
    }
} 