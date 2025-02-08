using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models
{
    public class Homeowner
    {
        [Key]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string ContactNumber { get; set; }
        public int BlockNumber { get; set; }
        public int HouseNumber { get; set; }
        public string Address { get; set; }
    }
}
