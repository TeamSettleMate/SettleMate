namespace SettleMate.Web.Models
{
    public class ReviewReport
    {
        public long ReviewId { get; set; }
        public long UserId { get; set; }
        public string Reason { get; set; } = "";
    }
}
