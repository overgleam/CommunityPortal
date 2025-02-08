using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models.Admin
{
    public class AdminSettingsViewModel
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Address")]
        public string Address { get; set; }
    }
}
