using CommunityPortal.Models;
using CommunityPortal.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;

namespace CommunityPortal.Data.Seeds
{
    public static class StaffSeeder
    {
        public static void SeedStaff(ModelBuilder builder)
        {
            // Create staff users
            var staffUsers = new[]
            {
                CreateStaffUser("aiah@gmail.com", "aiahstaff", "09772719114", "images/default/Aiah.png"),
                CreateStaffUser("david@gmail.com", "davidstaff", "09772719114", "images/default/David.png"),
                CreateStaffUser("marinella@gmail.com", "marinellastaff", "09772719114", "images/default/Caber.png"),
                CreateStaffUser("kevin@gmail.com", "kevinstaff", "09772719114", "images/default/Tamayo.png"),
                CreateStaffUser("slater@gmail.com", "slaterstaff", "09772719114", "images/default/Slater.png"),
                CreateStaffUser("crist@gmail.com", "criststaff", "09772719114", "images/default/Brader.png"),
                CreateStaffUser("al@gmail.com", "alstaff", "09772719114", "images/default/Roblox.png"),
                CreateStaffUser("rowell@gmail.com", "rowellstaff", "09772719114", "images/default/Rowell.png"),
                CreateStaffUser("hev@gmail.com", "hevstaff", "09772719114", "images/default/Hev.png"),
                CreateStaffUser("denise@gmail.com", "denisestaff", "09772719114", "images/default/Denise.png"),
            };

            // Create hashed password for all staff (password: 123123)
            var hasher = new PasswordHasher<ApplicationUser>();
            var defaultPassword = "123123";

            for (int i = 0; i < staffUsers.Length; i++)
            {
                staffUsers[i].NormalizedEmail = staffUsers[i].Email.ToUpper();
                staffUsers[i].NormalizedUserName = staffUsers[i].UserName.ToUpper();
                staffUsers[i].PasswordHash = hasher.HashPassword(staffUsers[i], defaultPassword);
            }

            builder.Entity<ApplicationUser>().HasData(staffUsers);

            // Create staff profile records
            builder.Entity<Staff>().HasData(
                new Staff { UserId = staffUsers[0].Id, FirstName = "Aiah", LastName = "Arceta", Department = "Maintenance Department", Position = "General Maintenance Worker", Address = "123 Main St, Cityville" },
                new Staff { UserId = staffUsers[1].Id, FirstName = "David", LastName = "Guison", Department = "Maintenance Department", Position = "Plumber", Address = "456 Elm St, Townsville" },
                new Staff { UserId = staffUsers[2].Id, FirstName = "Marinella", LastName = "Caber", Department = "Maintenance Department", Position = "Electrician", Address = "789 Oak St, Villageton" },
                new Staff { UserId = staffUsers[3].Id, FirstName = "Kevin", LastName = "Tamayo", Department = "Maintenance Department", Position = "HVAC Technician", Address = "101 Pine St, Metrocity" },
                new Staff { UserId = staffUsers[4].Id, FirstName = "Slater", LastName = "Young", Department = "Maintenance Department", Position = "Carpenter", Address = "202 Birch St, Suburbia" },
                new Staff { UserId = staffUsers[5].Id, FirstName = "Crist", LastName = "Briand", Department = "Security Department", Position = "Security Guard", Address = "303 Cedar St, Uptown" },
                new Staff { UserId = staffUsers[6].Id, FirstName = "Al", LastName = "Moralde", Department = "Security Department", Position = "Access Control Officer", Address = "404 Redwood St, Downtown" },
                new Staff { UserId = staffUsers[7].Id, FirstName = "Rowell", LastName = "Divina", Department = "Housekeeping & Sanitation Department", Position = "Janitor/Cleaner", Address = "505 Maple St, Citytown" },
                new Staff { UserId = staffUsers[8].Id, FirstName = "Hev", LastName = "Abi", Department = "Housekeeping & Sanitation Department", Position = "Waste Management Staff", Address = "606 Spruce St, Countryville" },
                new Staff { UserId = staffUsers[9].Id, FirstName = "Denise", LastName = "Julia", Department = "Landscaping & Gardening Department", Position = "Gardener", Address = "707 Willow St, Riverside" }
            );
        }

        private static ApplicationUser CreateStaffUser(string email, string username, string phone, string imagePath)
        {
            return new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                UserName = username,
                PhoneNumber = phone,
                ProfileImagePath = imagePath,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                Status = UserStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}