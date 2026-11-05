using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using SettleMate.Web.Models;

namespace SettleMate.Web.Models
{
    public class SupportRequest
    {
        [Required, StringLength(100)]
        public string FullName { get; set; } = "";

        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        [Required, StringLength(150)]
        public string Subject { get; set; } = "";

        [Required, StringLength(2000, MinimumLength = 10)]
        public string Message { get; set; } = "";
    }
}
