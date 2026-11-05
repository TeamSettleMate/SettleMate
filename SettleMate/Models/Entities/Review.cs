namespace SettleMate.Web.Models
{
    public class Review
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long ListingId { get; set; }
        public int Rating { get; set; }
        public string Text { get; set; } = "";
        public bool Removed { get; set; }
        public string Reason { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string FullName { get; set; } = "";
        public string Title { get; set; } = "";
        public long OwnerId { get; set; }
        public long Reports { get; set; }
    }
}
