using CommunityPortal.Models.ServiceRequest;
using Microsoft.EntityFrameworkCore;

namespace CommunityPortal.Data.Seeds
{
    public static class ServiceCategorySeeder
    {
        public static void SeedServiceCategories(ModelBuilder builder)
        {
            builder.Entity<ServiceCategory>().HasData(
                new ServiceCategory
                {
                    Id = 1,
                    Name = "Electrical Issues",
                    Description = "Power outages, malfunctioning streetlights, faulty wiring, outlets, circuit breakers, and installation of additional outdoor lighting"
                },
                new ServiceCategory
                {
                    Id = 2,
                    Name = "Plumbing & Water Supply Issues",
                    Description = "Low or no water pressure, leaking pipes, faucets, toilets, clogged drainage, sewage backups, and water supply interruptions"
                },
                new ServiceCategory
                {
                    Id = 3,
                    Name = "Structural & Property Repairs",
                    Description = "Cracks in walls, sidewalks, or roads, broken gates, fences, perimeter walls, roof leaks, damaged ceilings, and pest infestation"
                },
                new ServiceCategory
                {
                    Id = 4,
                    Name = "Waste Management & Cleaning",
                    Description = "Missed garbage collection, request for additional trash bins, flooding or stagnant water after heavy rains, and cleaning of community spaces"
                }
            );
        }
    }
}