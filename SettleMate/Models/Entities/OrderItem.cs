namespace SettleMate.Web.Models
{
    public class OrderItem
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public long ListingId { get; set; }
        public long SellerId { get; set; }
        public string Title { get; set; } = "";
        public string ListingType { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public decimal Deposit { get; set; }
        public int Months { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public bool Collected { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public long BuyerId { get; set; }
        public DateTime OrderDate { get; set; }
        public string BuyerName { get; set; } = "";
        public string SellerName { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Pincode { get; set; } = "";
        public string Phone { get; set; } = "";
        public string BuyerEmail { get; set; } = "";
    }
}
