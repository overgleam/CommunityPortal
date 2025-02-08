﻿using Microsoft.AspNetCore.Identity;

namespace CommunityPortal.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool Enable { get; set; } = false;
        public Administrator? Administrator { get; set; }
        public Staff? Staff { get; set; }
        public Homeowner? Homeowner { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
