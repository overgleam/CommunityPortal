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
        [Display(Name = "Block No.")]
        public int BlockNumber { get; set; }

        [Required]
        [Display(Name = "House No.")]
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

    }

    public class HomeownerProfileEditViewModel : HomeownerProfileViewModel
    {
        [Phone]
        [Required(ErrorMessage = "You must provide a phone number")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression(@"^(09|\+639)\d{9}$", ErrorMessage = "Not a valid phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [DataType(DataType.Upload)]
        [Display(Name = "Profile Image")]
        public IFormFile? ProfileImage { get; set; }

        [Required(ErrorMessage = "Password is required to save changes")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm Password is required to save changes")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }
    }
}