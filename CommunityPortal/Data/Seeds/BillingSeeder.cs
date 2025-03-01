using CommunityPortal.Models.Billing;
using Microsoft.EntityFrameworkCore;

namespace CommunityPortal.Data.Seeds
{
    public static class BillingSeeder
    {
        public static void SeedFeeTypes(ModelBuilder builder)
        {
            builder.Entity<FeeType>().HasData(
                new FeeType
                {
                    Id = 1,
                    Name = "Association Dues",
                    Description = "Monthly homeowner association dues",
                    DefaultAmount = 2000.00M,
                    Category = "Association Dues",
                    IsRecurring = true,
                    IsRequired = true
                },
                new FeeType
                {
                    Id = 2,
                    Name = "Security and Maintenance",
                    Description = "Fees for security personnel and maintenance of common areas",
                    DefaultAmount = 1000.00M,
                    Category = "Security and Maintenance",
                    IsRecurring = true,
                    IsRequired = true
                },
                new FeeType
                {
                    Id = 3,
                    Name = "Emergency Fund",
                    Description = "Contribution to emergency fund for unforeseen community needs",
                    DefaultAmount = 200.00M,
                    Category = "Emergency Fund",
                    IsRecurring = true,
                    IsRequired = true
                },
                new FeeType
                {
                    Id = 4,
                    Name = "Facility Upkeep",
                    Description = "Maintenance and upkeep of community facilities",
                    DefaultAmount = 500.00M,
                    Category = "Facility Upkeep",
                    IsRecurring = true,
                    IsRequired = true
                },
                new FeeType
                {
                    Id = 5,
                    Name = "Administrative Expenses",
                    Description = "Expenses related to administrative functions",
                    DefaultAmount = 300.00M,
                    Category = "Administrative",
                    IsRecurring = true,
                    IsRequired = true
                }
            );
        }

        public static void SeedBillingSettings(ModelBuilder builder)
        {
            builder.Entity<BillingSettings>().HasData(
                new BillingSettings
                {
                    Id = 1,
                    Name = "Default Billing Settings",
                    Description = "Default configuration for billing operations",
                    LateFeePercentage = 5.00M,
                    LateFeeDays = 30,
                    BillingCycleDay = 1,
                    PaymentDueDays = 15,
                    CreatedBy = "system"
                }
            );
        }
    }
}