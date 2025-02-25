using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.Documents
{
    public class Document
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Document Title")]
        public string Title { get; set; }

        [StringLength(500)]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required]
        [StringLength(255)]
        [Display(Name = "File Path")]
        public string FilePath { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "File Name")]
        public string FileName { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "File Type")]
        public string FileType { get; set; }

        [Display(Name = "File Size (KB)")]
        public long FileSizeInKB { get; set; }

        [Required]
        [Display(Name = "Document Category")]
        public DocumentCategory Category { get; set; }

        [Display(Name = "Upload Date")]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        [Display(Name = "Last Updated")]
        public DateTime? LastUpdated { get; set; }

        [Display(Name = "Uploaded By")]
        public string UploadedById { get; set; }

        [ForeignKey("UploadedById")]
        public ApplicationUser UploadedBy { get; set; }

        [Display(Name = "Is Deleted")]
        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Deleted Date")]
        public DateTime? DeletedDate { get; set; }

        [Display(Name = "Deleted By")]
        public string? DeletedById { get; set; }

        [ForeignKey("DeletedById")]
        public ApplicationUser? DeletedBy { get; set; }
    }
} 