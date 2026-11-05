namespace SettleMate.Web.Models
{
    public class TicketReply
    {
        public long Id { get; set; }
        public long TicketId { get; set; }
        public long AdminId { get; set; }
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string FullName { get; set; } = "";
    }
}
