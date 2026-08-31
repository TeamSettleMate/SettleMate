using System.ComponentModel.DataAnnotations;

namespace SettleMate.Models.Student
{
    // Stores Student Registration form data
    public class RegisterModel
    {
        // Store User ID from database
        public int UserID { get; set; }

        // Store full name
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(
            100,
            MinimumLength = 3,
            ErrorMessage = "Name must be between 3 and 100 characters")]
        public string Name { get; set; }

        // Store email
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; }

        // Store 10-digit mobile number
        [Required(ErrorMessage = "Mobile number is required")]
        [RegularExpression(
            @"^[0-9]{10}$",
            ErrorMessage = "Mobile number must contain 10 digits")]
        public string Phone { get; set; }

        // Store selected gender
        [Required(ErrorMessage = "Please select gender")]
        public string Gender { get; set; }

        // Store selected city
        [Required(ErrorMessage = "City is required")]
        public string City { get; set; }

        // Store address
        [Required(ErrorMessage = "Address is required")]
        public string Address { get; set; }

        // Store profile photo path
        public string? ProfilePhoto { get; set; }

        // Store password
        [Required(ErrorMessage = "Password is required")]
        [StringLength(
            50,
            MinimumLength = 8,
            ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; }

        // Store confirm password
        [Required(ErrorMessage = "Please confirm password")]
        [Compare(
            "Password",
            ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        // Default role is Student
        public string Role { get; set; } = "Student";
    }
}