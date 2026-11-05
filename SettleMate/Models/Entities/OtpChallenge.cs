namespace SettleMate.Web.Models
{
    public class OtpChallenge
    {
        public long Id { get; set; }
        public string Email { get; set; } = "";
        public string Purpose { get; set; } = "";
        public long? ActorId { get; set; }
        public string SessionHash { get; set; } = "";
        public string CodeHash { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public bool IsUsed { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string ProofHash { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
