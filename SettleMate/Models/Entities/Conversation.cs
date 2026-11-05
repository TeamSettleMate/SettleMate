namespace SettleMate.Web.Models
{
    public class Conversation
    {
        public long Id { get; set; }
        public long ListingId { get; set; }
        public long UserId { get; set; }
        public long OwnerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Title { get; set; } = "";
        public string UserName { get; set; } = "";
        public string OwnerName { get; set; } = "";
    }
}
