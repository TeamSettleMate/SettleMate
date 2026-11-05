namespace SettleMate.Web.Models
{
    public class ModerationEntry
    {
        public long Id { get; set; }
        public long ActorId { get; set; }
        public string Entity { get; set; } = "";
        public long EntityId { get; set; }
        public string Action { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string FullName { get; set; } = "";
    }
}
