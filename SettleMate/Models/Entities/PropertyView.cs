namespace SettleMate.Web.Models
{
    public class PropertyView
    {
        public long Id { get; set; }
        public long ListingId { get; set; }
        public long ViewerId { get; set; }
        public DateTime ViewDate { get; set; }
    }
}
