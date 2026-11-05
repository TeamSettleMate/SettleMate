using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using SettleMate.Web.Models;

namespace SettleMate.Web.Models
{
    public class Registration
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "Enter a 10-digit phone number.")]
        public string Phone { get; set; } = "";

        [Required, MinLength(8), RegularExpression(@"^(?=.*\d)(?=.*[^\w\s]).{8,128}$", ErrorMessage = "Use 8–128 characters, a number and a symbol.")]
        public string Password { get; set; } = "";

        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = "";
        public string Role { get; set; } = "User";
        public DateTime? DateOfBirth { get; set; }

        [StringLength(20)]
        public string Gender { get; set; } = "Other";

        [StringLength(300)]
        public string Address { get; set; } = "";

        [Required, StringLength(80)]
        public string City { get; set; } = "";

        [Required, StringLength(80)]
        public string State { get; set; } = "";

        [RegularExpression(@"^\d{6}$")]
        public string? Pincode { get; set; }

        [Range(1, 8)]
        public int Avatar { get; set; } = 1;
        public string DocumentType { get; set; } = "Government ID";
        public IFormFile? IdentityDocument { get; set; }

        [Required]
        public string OtpProof { get; set; } = "";
    }
}
