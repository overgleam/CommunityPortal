using System.Collections.Generic;
using CommunityPortal.Models.Enums;

namespace CommunityPortal.Models.Documents
{
    public class DocumentListViewModel
    {
        public IEnumerable<Document> Documents { get; set; }
        public DocumentCategory? CategoryFilter { get; set; }
        public string SearchTerm { get; set; }
        public bool ShowDeleted { get; set; } = false;
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
        public int PageSize { get; set; } = 10;
    }
} 