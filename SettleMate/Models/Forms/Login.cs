using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using SettleMate.Web.Models;

namespace SettleMate.Web.Models
{
    public class Login
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";
        public string Role { get; set; } = "User";
        public string? ReturnUrl { get; set; }
    }
}
