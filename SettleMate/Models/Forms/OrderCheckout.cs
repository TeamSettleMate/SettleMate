using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using SettleMate.Web.Models;

namespace SettleMate.Web.Models
{
    public class OrderCheckout
    {
        [Required, StringLength(300)]
        public string Address { get; set; } = "";

        [Required, StringLength(80)]
        public string City { get; set; } = "";

        [Required, StringLength(80)]
        public string State { get; set; } = "";

        [Required, RegularExpression(@"^\d{6}$")]
        public string Pincode { get; set; } = "";

        [Required, RegularExpression(@"^\d{10}$")]
        public string Phone { get; set; } = "";
        public Guid CheckoutToken { get; set; } = Guid.NewGuid();

        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public List<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
