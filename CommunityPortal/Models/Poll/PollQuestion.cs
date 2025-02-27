using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.Poll
{
    public class PollQuestion
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int PollId { get; set; }
        
        [ForeignKey("PollId")]
        public Poll Poll { get; set; }
        
        [Required]
        [StringLength(255)]
        public string QuestionText { get; set; }
        
        [Required]
        public QuestionType QuestionType { get; set; }
        
        public int DisplayOrder { get; set; }
        
        [Required]
        public bool IsRequired { get; set; } = true;
        
        // For rating questions
        public int? MinRating { get; set; }
        public int? MaxRating { get; set; }
        
        // Soft delete support
        public bool IsDeleted { get; set; } = false;
        
        // Navigation properties
        public virtual ICollection<PollQuestionOption> Options { get; set; } = new List<PollQuestionOption>();
        public virtual ICollection<PollQuestionAnswer> Answers { get; set; } = new List<PollQuestionAnswer>();
    }
} 