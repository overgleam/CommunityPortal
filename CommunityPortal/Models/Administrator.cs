using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models
{
    public class Administrator
    {
        [Key]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string? ProfilePicturePath { get; set; }
    }
}
