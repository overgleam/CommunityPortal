using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.Documents
{
    public class DocumentUploadViewModel
    {
        [Required(ErrorMessage = "Please enter a title for the document")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        [Display(Name = "Document Title")]
        public string Title { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        [Display(Name = "Description")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Please select a document category")]
        [Display(Name = "Document Category")]
        public DocumentCategory Category { get; set; }

        [Required(ErrorMessage = "Please select a file to upload")]
        [Display(Name = "File")]
        public IFormFile File { get; set; }
    }
} 