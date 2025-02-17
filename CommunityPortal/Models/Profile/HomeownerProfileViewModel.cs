using System.ComponentModel.DataAnnotations;

namespace CommunityPortal.Models.Profile
{
    public class HomeownerProfileViewModel
    {
        public ApplicationUser User { get; set; }

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

        [Required]
        [Display(Name = "Move In Date")]
        public DateTime MoveInDate { get; set; }

        [Required]
        [Display(Name = "Type of Residency")]
        public string TypeOfResidency { get; set; }

        [Phone]
        [Required(ErrorMessage = "You must provide a phone number")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Not a valid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

    }

    public class HomeownerProfileEditViewModel : HomeownerProfileViewModel
    {
        // Password change fields
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 6)]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}