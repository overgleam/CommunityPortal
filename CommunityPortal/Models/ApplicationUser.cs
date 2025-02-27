// Models/ApplicationUser.cs
using CommunityPortal.Models.Enums;
using Microsoft.AspNetCore.Identity;

namespace CommunityPortal.Models
{
    public class ApplicationUser : IdentityUser
    {
        public UserStatus Status { get; set; } = UserStatus.PendingApproval;
        public Administrator? Administrator { get; set; }
        public Staff? Staff { get; set; }
        public Homeowner? Homeowner { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ProfileImagePath { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // Improved FullName property to handle cases where related entities might not be loaded
        public string FullName 
        { 
            get 
            { 
                if (Homeowner != null)
                {
                    return $"{Homeowner.FirstName} {Homeowner.LastName}".Trim();
                }
                else if (Staff != null)
                {
                    return $"{Staff.FirstName} {Staff.LastName}".Trim();
                }
                else if (Administrator != null)
                {
                    return $"{Administrator.FirstName} {Administrator.LastName}".Trim();
                }
                
                // Fallback to the email username part if no name is available
                return UserName?.Split('@')[0] ?? "Unknown User";
            } 
        }
    }
}