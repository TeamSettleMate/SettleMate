namespace SettleMate.Web.Models
{
    public class CartEntry
    {
        public long UserId { get; set; }
        public long ListingId { get; set; }
        public int Months { get; set; }
        public long Id { get; set; }
        public long OwnerId { get; set; }
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Type { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Address { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Deposit { get; set; }
        public string Status { get; set; } = "";
        public bool Available { get; set; }
        public string Reason { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string SellerName { get; set; } = "";
        public string Image { get; set; } = "";
        public string SellerStatus { get; set; } = "";
    }
}
