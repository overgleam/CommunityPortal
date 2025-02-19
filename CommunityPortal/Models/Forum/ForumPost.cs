using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityPortal.Models.Forum
{
    public class ForumPost
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        [StringLength(5000)]
        public string Content { get; set; }

        [StringLength(255)]
        [RegularExpression(@"^.*\.(jpg|jpeg|png|gif)$", ErrorMessage = "Only image files (jpg, jpeg, png, gif) are allowed.")]
        public string? ImagePath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Required]
        public string AuthorId { get; set; }

        [ForeignKey("AuthorId")]
        public ApplicationUser Author { get; set; }

        public ICollection<ForumComment> Comments { get; set; } = new List<ForumComment>();
        public ICollection<ForumLike> Likes { get; set; } = new List<ForumLike>();
    }
} 