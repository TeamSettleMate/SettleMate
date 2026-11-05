namespace SettleMate.Web.Models
{
    public class Message
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public long SenderId { get; set; }
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string FullName { get; set; } = "";
    }
}
