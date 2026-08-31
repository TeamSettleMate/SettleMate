using System.ComponentModel.DataAnnotations;

namespace SettleMate.Models.Student
{
    // Stores Reset Password form data
    public class ResetPasswordModel
    {
        // Store new password
        [Required(ErrorMessage = "New password is required")]
        [StringLength(
            50,
            MinimumLength = 8,
            ErrorMessage = "Password must be at least 8 characters")]
        public string NewPassword { get; set; }

        // Store confirm password
        [Required(ErrorMessage = "Please confirm your password")]
        [Compare(
            "NewPassword",
            ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }
    }
}