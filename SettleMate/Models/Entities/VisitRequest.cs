namespace SettleMate.Web.Models
{
    public class VisitRequest
    {
        public long Id { get; set; }
        public long UserId { get; set; }
        public long ListingId { get; set; }
        public DateTime VisitAt { get; set; }
        public string Message { get; set; } = "";
        public string Response { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public long OwnerId { get; set; }
        public string Title { get; set; } = "";
        public string Kind { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
    }
}
