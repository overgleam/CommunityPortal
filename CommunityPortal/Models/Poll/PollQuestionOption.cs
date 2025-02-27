using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Poll
{
    public class PollQuestionOption
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int QuestionId { get; set; }
        
        [ForeignKey("QuestionId")]
        public PollQuestion Question { get; set; }
        
        [Required]
        [StringLength(255)]
        public string OptionText { get; set; }
        
        public int DisplayOrder { get; set; }
        
        // Soft delete support
        public bool IsDeleted { get; set; } = false;
    }
} 