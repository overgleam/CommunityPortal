using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Poll
{
    public class PollResponse
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int PollId { get; set; }
        
        [ForeignKey("PollId")]
        public Poll Poll { get; set; }
        
        [Required]
        public string RespondentId { get; set; }
        
        [ForeignKey("RespondentId")]
        public ApplicationUser Respondent { get; set; }
        
        [Required]
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        
        // Soft delete support
        public bool IsDeleted { get; set; } = false;
        
        // Navigation property
        public virtual ICollection<PollQuestionAnswer> Answers { get; set; } = new List<PollQuestionAnswer>();
    }
} 