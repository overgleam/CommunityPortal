﻿using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CommunityPortal.Models;
using CommunityPortal.Models.Forum;
using CommunityPortal.Models.Facility;
using CommunityPortal.Models.Event;
using CommunityPortal.Models.ServiceRequest;
using CommunityPortal.Models.Documents;
using CommunityPortal.Models.Poll;
using CommunityPortal.Models.Billing;
using CommunityPortal.Models.GatePass;
using CommunityPortal.Data.Seeds;

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
        public DbSet<Facility> Facilities { get; set; }
        public DbSet<FacilityReservation> FacilityReservations { get; set; }
        public DbSet<BlackoutDate> BlackoutDates { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<PaymentMethod> PaymentMethods { get; set; }
        public DbSet<Poll> Polls { get; set; }
        public DbSet<PollQuestion> PollQuestions { get; set; }
        public DbSet<PollQuestionOption> PollQuestionOptions { get; set; }
        public DbSet<PollResponse> PollResponses { get; set; }
        public DbSet<PollQuestionAnswer> PollQuestionAnswers { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<BillItem> BillItems { get; set; }
        public DbSet<FeeType> FeeTypes { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<BillingSettings> BillingSettings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<GatePass> GatePasses { get; set; }

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


            // Facility configurations
            builder.Entity<Facility>()
                .HasQueryFilter(f => !f.IsDeleted);

            builder.Entity<FacilityReservation>()
                .HasQueryFilter(fr => !fr.IsDeleted && !fr.Facility.IsDeleted);

            builder.Entity<BlackoutDate>()
                .HasQueryFilter(bd => !bd.IsDeleted && !bd.Facility.IsDeleted);

            builder.Entity<FacilityReservation>()
                .HasOne(fr => fr.Facility)
                .WithMany(f => f.Reservations)
                .HasForeignKey(fr => fr.FacilityId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.Entity<FacilityReservation>()
                .HasOne(fr => fr.User)
                .WithMany()
                .HasForeignKey(fr => fr.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.Entity<BlackoutDate>()
                .HasOne(bd => bd.Facility)
                .WithMany(f => f.BlackoutDates)
                .HasForeignKey(bd => bd.FacilityId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Event configurations
            builder.Entity<Event>()
                .HasQueryFilter(e => !e.IsDeleted);

            builder.Entity<Event>()
                .HasOne(e => e.Creator)
                .WithMany()
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Document configurations
            builder.Entity<Document>()
                .HasOne(d => d.UploadedBy)
                .WithMany()
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Document>()
                .HasOne(d => d.DeletedBy)
                .WithMany()
                .HasForeignKey(d => d.DeletedById)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Poll configurations
            builder.Entity<Poll>()
                .HasQueryFilter(p => !p.IsDeleted);

            builder.Entity<PollQuestion>()
                .HasQueryFilter(q => !q.IsDeleted);

            builder.Entity<PollResponse>()
                .HasQueryFilter(r => !r.IsDeleted);

            builder.Entity<PollQuestionAnswer>()
                .HasQueryFilter(a => !a.IsDeleted);

            builder.Entity<PollQuestionOption>()
                .HasQueryFilter(o => !o.IsDeleted);

            builder.Entity<Poll>()
                .HasOne(p => p.CreatedBy)
                .WithMany()
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Poll>()
                .HasOne(p => p.LastUpdatedBy)
                .WithMany()
                .HasForeignKey(p => p.LastUpdatedById)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.Entity<PollQuestion>()
                .HasOne(q => q.Poll)
                .WithMany(p => p.Questions)
                .HasForeignKey(q => q.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PollQuestionOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            builder.Entity<PollResponse>()
                .HasOne(r => r.Poll)
                .WithMany(p => p.Responses)
                .HasForeignKey(r => r.PollId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            builder.Entity<PollResponse>()
                .HasOne(r => r.Respondent)
                .WithMany()
                .HasForeignKey(r => r.RespondentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PollQuestionAnswer>()
                .HasOne(a => a.Response)
                .WithMany(r => r.Answers)
                .HasForeignKey(a => a.ResponseId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            builder.Entity<PollQuestionAnswer>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.Entity<PollQuestionAnswer>()
                .HasOne(a => a.SelectedOption)
                .WithMany()
                .HasForeignKey(a => a.SelectedOptionId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Billing-related configurations
            builder.Entity<Bill>()
                .HasOne(b => b.Homeowner)
                .WithMany()
                .HasForeignKey(b => b.HomeownerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<BillItem>()
                .HasOne(bi => bi.Bill)
                .WithMany(b => b.BillItems)
                .HasForeignKey(bi => bi.BillId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BillItem>()
                .HasOne(bi => bi.FeeType)
                .WithMany(ft => ft.BillItems)
                .HasForeignKey(bi => bi.FeeTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Bill)
                .WithMany(b => b.Payments)
                .HasForeignKey(p => p.BillId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Homeowner)
                .WithMany()
                .HasForeignKey(p => p.HomeownerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.PaymentMethod)
                .WithMany()
                .HasForeignKey(p => p.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            
            // 2. Seed basic data
            ServiceCategorySeeder.SeedServiceCategories(builder);
            BillingSeeder.SeedFeeTypes(builder);
            BillingSeeder.SeedBillingSettings(builder);
            
            // Configure Notification entity
            builder.Entity<Notification>()
                .HasOne(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notification>()
                .HasOne(n => n.Sender)
                .WithMany()
                .HasForeignKey(n => n.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
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
                .Where(e => (e.Entity is ApplicationUser || e.Entity is Notification) && 
                            (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                if (entry.Entity is ApplicationUser user)
                {
                    if (entry.State == EntityState.Added)
                    {
                        user.CreatedAt = DateTime.UtcNow;
                    }
                    user.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Notification notification && entry.State == EntityState.Modified)
                {
                    // For soft delete
                    if (notification.IsDeleted && notification.DeletedAt == null)
                    {
                        notification.DeletedAt = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}

