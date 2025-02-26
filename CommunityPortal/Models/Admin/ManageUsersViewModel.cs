namespace CommunityPortal.Models.Admin
{
    public class ManageUsersViewModel
    {
        public List<UserWithRoleViewModel> Users { get; set; }
    }

    public class UserWithRoleViewModel
    {
        public ApplicationUser User { get; set; }
        public string Role { get; set; }
    }
}
