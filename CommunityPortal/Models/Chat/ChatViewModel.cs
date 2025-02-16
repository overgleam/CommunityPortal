using System.Collections.Generic;

namespace CommunityPortal.Models.Chat
{
    public class ChatViewModel
    {
        public string RecipientId { get; set; }
        public string RecipientUsername { get; set; }
        public string CurrentUserFullName { get; set; }
        public List<ChatMessageViewModel> Messages { get; set; } = new List<ChatMessageViewModel>();
    }
}