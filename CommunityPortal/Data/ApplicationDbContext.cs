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
                    Name = "Plumbing",
                    Description = "Water systems, pipes, drains, and related fixtures"
                },
                new ServiceCategory 
                { 
                    Id = 2, 
                    Name = "Electrical",
                    Description = "Electrical systems, wiring, outlets, and lighting"
                },
                new ServiceCategory 
                { 
                    Id = 3, 
                    Name = "HVAC",
                    Description = "Heating, ventilation, and air conditioning systems"
                },
                new ServiceCategory 
                { 
                    Id = 4, 
                    Name = "Garbage Collection",
                    Description = "Waste management and disposal services"
                },
                new ServiceCategory 
                { 
                    Id = 5, 
                    Name = "Pest Control",
                    Description = "Pest inspection and elimination services"
                },
                new ServiceCategory 
                { 
                    Id = 6, 
                    Name = "General Maintenance",
                    Description = "General repairs and maintenance work"
                },
                new ServiceCategory 
                { 
                    Id = 7, 
                    Name = "Landscaping",
                    Description = "Garden maintenance, tree trimming, and lawn care"
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

