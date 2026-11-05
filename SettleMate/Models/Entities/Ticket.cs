namespace SettleMate.Web.Models
{
    public class Ticket
    {
        public long Id { get; set; }
        public long? AccountId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
