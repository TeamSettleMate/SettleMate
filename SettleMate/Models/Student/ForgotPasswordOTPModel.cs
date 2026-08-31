using System.ComponentModel.DataAnnotations;

namespace SettleMate.Models.Student
{
    // Stores OTP entered by the user
    public class ForgotPasswordOTPModel
    {
        // OTP is required and must contain exactly 6 digits
        [Required(ErrorMessage = "OTP is required")]
        [RegularExpression(
            @"^\d{6}$",
            ErrorMessage = "OTP must contain 6 digits")]
        public string OTP { get; set; }
    }
}