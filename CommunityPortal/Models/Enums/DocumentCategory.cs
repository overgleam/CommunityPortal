using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models.Enums
{
    public enum DocumentCategory
    {
        [Display(Name = "Community Guidelines")]
        CommunityGuidelines = 1,
        
        [Display(Name = "Emergency Contacts")]
        EmergencyContacts = 2,
        
        [Display(Name = "Forms")]
        Forms = 3,
        
        [Display(Name = "Newsletters")]
        Newsletters = 4,
        
        [Display(Name = "Meeting Minutes")]
        MeetingMinutes = 5,
        
        [Display(Name = "Financial Reports")]
        FinancialReports = 6,
        
        [Display(Name = "Policies")]
        Policies = 7,
        
        [Display(Name = "Other")]
        Other = 8
    }
} 