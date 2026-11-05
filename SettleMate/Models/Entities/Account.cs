namespace SettleMate.Web.Models
{
    public class Account
    {
        public long Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public string Reason { get; set; } = "";
        public bool EmailVerified { get; set; }
        public Guid SecurityStamp { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Pincode { get; set; } = "";
        public int Avatar { get; set; }
        public string DocumentType { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string NormalizedEmail { get; set; } = "";
        public long Listings { get; set; }
    }
}
