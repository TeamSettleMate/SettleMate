namespace SettleMate.Web.Models
{
    public class EmailOutbox
    {
        public long Id { get; set; }
        public string Recipient { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public int Attempts { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime NextAttemptAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
