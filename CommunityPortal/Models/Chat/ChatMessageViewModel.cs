namespace CommunityPortal.Models.Chat
{
    public class ChatMessageViewModel
    {
        public int Id { get; set; }
        public string SenderUsername { get; set; }
        public string SenderFullName { get; set; }
        public string Message { get; set; }
        public string Timestamp { get; set; }
    }
}
