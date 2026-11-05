namespace SettleMate.Web.Models
{
    public class Notification
    {
        public long Id { get; set; }
        public long AccountId { get; set; }
        public string Text { get; set; } = "";
        public string Url { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
