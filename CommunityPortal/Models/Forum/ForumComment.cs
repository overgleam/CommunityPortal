using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Forum
{
    public class ForumComment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public ApplicationUser Author { get; set; }

        [Required]
        public int PostId { get; set; }

        [ForeignKey("PostId")]
        public ForumPost Post { get; set; }

        public int? ParentCommentId { get; set; }

        [ForeignKey("ParentCommentId")]
        public ForumComment? ParentComment { get; set; }

        public ICollection<ForumComment> Replies { get; set; } = new List<ForumComment>();
        public ICollection<ForumLike> Likes { get; set; } = new List<ForumLike>();
    }
} 