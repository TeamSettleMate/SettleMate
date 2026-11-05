namespace SettleMate.Web.Models
{
    /// <summary>One listing in a user's cart; each existing listing represents one inventory unit.</summary>
    public class CartItem
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string SellerName { get; set; } = "";
        public string Image { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Deposit { get; set; }
        public int Months { get; set; }

        public int ChargedMonths
        {
            get
            {
                if (Type == "Rent")
                {
                    return Months;
                }
                else
                {
                    return 1;
                }
            }
        }

        public decimal ChargedDeposit
        {
            get
            {
                if (Type == "Rent")
                {
                    return Deposit;
                }
                else
                {
                    return 0;
                }
            }
        }

        public decimal Amount
        {
            get
            {
                return Price * ChargedMonths + ChargedDeposit;
            }
        }
    }
}
