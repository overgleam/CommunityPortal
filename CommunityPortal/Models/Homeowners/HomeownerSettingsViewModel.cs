using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models.Homeowners
{
    public class HomeownerSettingsViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Block Number")]
        public int BlockNumber { get; set; }

        [Required]
        [Display(Name = "House Number")]
        public int HouseNumber { get; set; }

        [Required]
        [Display(Name = "Address")]
        public string Address { get; set; }
    }
}
