using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using SettleMate.Web.Models;

namespace SettleMate.Web.Models
{
    public class AccountProfile
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = "";

        [Required, RegularExpression(@"^\d{10}$")]
        public string Phone { get; set; } = "";

        [StringLength(300)]
        public string Address { get; set; } = "";

        [Required, StringLength(80)]
        public string City { get; set; } = "";

        [Required, StringLength(80)]
        public string State { get; set; } = "";

        [RegularExpression(@"^\d{6}$")]
        public string? Pincode { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Gender { get; set; } = "Other";

        [Range(1, 8)]
        public int Avatar { get; set; } = 1;
    }
}
