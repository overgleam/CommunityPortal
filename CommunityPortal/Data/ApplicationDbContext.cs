using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models;
using CommunityPortal.Models.Forum;

namespace CommunityPortal.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<Administrator> Admins { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Homeowner> Homeowners { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ForumPost> ForumPosts { get; set; }
        public DbSet<ForumComment> ForumComments { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<ForumLike> ForumLikes { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<ServiceCategory> ServiceCategories { get; set; }
        public DbSet<ServiceFeedback> ServiceFeedbacks { get; set; }
        public DbSet<ServiceStaffAssignment> ServiceStaffAssignments { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Administrator>().HasKey(a => a.UserId);

            builder.Entity<Administrator>()
                .HasOne(a => a.User)
                .WithOne(u => u.Administrator)
                .HasForeignKey<Administrator>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Staff>().HasKey(s => s.UserId);

            builder.Entity<Staff>()
                .HasOne(s => s.User)
                .WithOne(u => u.Staff)
                .HasForeignKey<Staff>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Homeowner>().HasKey(h => h.UserId);

            builder.Entity<Homeowner>()
                .HasOne(h => h.User)
                .WithOne(u => u.Homeowner)
                .HasForeignKey<Homeowner>(h => h.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationUser>()
                .Property(u => u.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
            builder.Entity<ApplicationUser>()
                .Property(u => u.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");


            builder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChatMessage>()
                .HasOne(cm => cm.Recipient)
                .WithMany()
                .HasForeignKey(cm => cm.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ForumPost>()
                .HasOne(p => p.Author)
                .WithMany()
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ForumComment>()
                .HasOne(c => c.Author)
                .WithMany()
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ForumComment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ForumComment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ForumLike>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ForumLike>()
                .HasOne(l => l.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ForumLike>()
                .HasOne(l => l.Comment)
                .WithMany(c => c.Likes)
                .HasForeignKey(l => l.CommentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Homeowner)
                .WithMany()
                .HasForeignKey(sr => sr.HomeownerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ServiceStaffAssignment>()
                .HasOne(ssa => ssa.ServiceRequest)
                .WithMany(sr => sr.StaffAssignments)
                .HasForeignKey(ssa => ssa.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServiceStaffAssignment>()
                .HasOne(ssa => ssa.Staff)
                .WithMany()
                .HasForeignKey(ssa => ssa.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ServiceRequest>()
                .HasOne(sr => sr.ServiceCategory)
                .WithMany(sc => sc.ServiceRequests)
                .HasForeignKey(sr => sr.ServiceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ServiceFeedback>()
                .HasOne(sf => sf.ServiceRequest)
                .WithOne(sr => sr.Feedback)
                .HasForeignKey<ServiceFeedback>(sf => sf.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServiceFeedback>()
                .HasOne(sf => sf.Homeowner)
                .WithMany()
                .HasForeignKey(sf => sf.HomeownerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed Service Categories
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

        public override int SaveChanges()
        {
            UpdateAuditFields();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateAuditFields();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateAuditFields()
        {
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is ApplicationUser && 
                            (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var user = (ApplicationUser)entry.Entity;
                if (entry.State == EntityState.Added)
                {
                    user.CreatedAt = DateTime.UtcNow;
                }
                user.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}

