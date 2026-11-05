namespace SettleMate.Web.Models
{
    public class OrderHistory
    {
        public long Id { get; set; }
        public long OrderItemId { get; set; }
        public string Status { get; set; } = "";
        public long ActorId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
