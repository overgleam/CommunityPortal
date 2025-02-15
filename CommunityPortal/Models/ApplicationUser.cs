// Models/ApplicationUser.cs
using CommunityPortal.Models.Admin;
using CommunityPortal.Models.Enums;
using CommunityPortal.Models.Homeowners;
using CommunityPortal.Models.Staffs;
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
    }
}