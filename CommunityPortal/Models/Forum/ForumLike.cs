using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Forum
{
    public class ForumLike
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public int? PostId { get; set; }

        [ForeignKey("PostId")]
        public ForumPost? Post { get; set; }

        public int? CommentId { get; set; }

        [ForeignKey("CommentId")]
        public ForumComment? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 