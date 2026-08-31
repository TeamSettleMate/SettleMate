using System.ComponentModel.DataAnnotations;

namespace SettleMate.Models.Student
{
    // Stores Forgot Password form data
    public class ForgotPasswordModel
    {
        // Email entered by the user
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }
    }
}