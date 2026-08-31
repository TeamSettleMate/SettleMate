using System.ComponentModel.DataAnnotations;

namespace SettleMate.Models.Student
{
    // Stores Student Login form data
    public class LoginModel
    {
        // Email entered by the user
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }

        // Password entered by the user
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
}