using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.Poll
{
    public class Poll
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [StringLength(500)]
        public string Description { get; set; }
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        [Required]
        public PollStatus Status { get; set; } = PollStatus.Draft;
        
        [Required]
        public PollTargetAudience TargetAudience { get; set; } = PollTargetAudience.AllHomeowners;
        
        public bool IsDeleted { get; set; } = false;
        
        [Required]
        public string CreatedById { get; set; }
        
        [ForeignKey("CreatedById")]
        public ApplicationUser CreatedBy { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        public string? LastUpdatedById { get; set; }
        
        [ForeignKey("LastUpdatedById")]
        public ApplicationUser LastUpdatedBy { get; set; }
        
        // Navigation properties
        public virtual ICollection<PollQuestion> Questions { get; set; } = new List<PollQuestion>();
        public virtual ICollection<PollResponse> Responses { get; set; } = new List<PollResponse>();
    }
} 