namespace SettleMate.Web.Models
{
    public class Order
    {
        public long Id { get; set; }
        public long BuyerId { get; set; }
        public Guid CheckoutToken { get; set; }
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string Pincode { get; set; } = "";
        public string Phone { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
