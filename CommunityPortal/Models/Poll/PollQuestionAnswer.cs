using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Poll
{
    public class PollQuestionAnswer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        [Required]
        public int ResponseId { get; set; }
        
        [ForeignKey("ResponseId")]
        public PollResponse Response { get; set; }
        
        [Required]
        public int QuestionId { get; set; }
        
        [ForeignKey("QuestionId")]
        public PollQuestion Question { get; set; }
        
        public int? SelectedOptionId { get; set; }
        
        [ForeignKey("SelectedOptionId")]
        public PollQuestionOption SelectedOption { get; set; }
        
        [StringLength(1000)]
        public string TextAnswer { get; set; }
        
        public int? RatingAnswer { get; set; }
        
        public bool? BoolAnswer { get; set; }
        
        // Soft delete support
        public bool IsDeleted { get; set; } = false;
    }
} 