using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.GatePass
{
    public class GatePassViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Visitor name is required")]
        [StringLength(100, ErrorMessage = "Visitor name cannot exceed 100 characters")]
        [Display(Name = "Visitor Name")]
        public string VisitorName { get; set; }

        [Required(ErrorMessage = "Purpose of visit is required")]
        [Display(Name = "Purpose of Visit")]
        public string Purpose { get; set; }

        [Required(ErrorMessage = "Visit date is required")]
        [Display(Name = "Visit Date")]
        [DataType(DataType.Date)]
        [Remote(action: "ValidateVisitDate", controller: "GatePass", ErrorMessage = "Visit date must be in the future")]
        public DateTime VisitDate { get; set; }

        [Required(ErrorMessage = "Expected arrival time is required")]
        [Display(Name = "Expected Arrival Time")]
        [DataType(DataType.DateTime)]
        public DateTime ExpectedArrivalTime { get; set; }

        [Required(ErrorMessage = "Number of visitors is required")]
        [Range(1, 20, ErrorMessage = "Number of visitors must be between 1 and 20")]
        [Display(Name = "Number of Visitors")]
        public int NumberOfVisitors { get; set; }

        [Display(Name = "Vehicle Details (Optional)")]
        [StringLength(100, ErrorMessage = "Vehicle details cannot exceed 100 characters")]
        public string? VisitorVehicleDetails { get; set; }

        [Display(Name = "Contact Number (Optional)")]
        [StringLength(50, ErrorMessage = "Contact number cannot exceed 50 characters")]
        [RegularExpression(@"^[0-9\-\+\s\(\)]*$", ErrorMessage = "Please enter a valid phone number")]
        public string? ContactNumber { get; set; }

        [Display(Name = "Pass Number")]
        public string? PassNumber { get; set; }

        [Display(Name = "Expiration Date")]
        [DataType(DataType.DateTime)]
        public DateTime? ExpirationDate { get; set; }

        [Display(Name = "Status")]
        public GatePassStatus Status { get; set; }

        [Display(Name = "Admin Notes")]
        public string? AdminNotes { get; set; }

        public string? HomeownerId { get; set; }
        
        [Display(Name = "Homeowner")]
        public string? HomeownerName { get; set; }

        [Display(Name = "Created Date")]
        public DateTime CreatedAt { get; set; }

        public bool CanBeCancelled => Status == GatePassStatus.Pending;
    }
} 