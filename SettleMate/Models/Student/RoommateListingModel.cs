using System.ComponentModel.DataAnnotations;

namespace SettleMate.Models.Student
{
    public class RoommateListingModel
    {
        // =========================================================
        // LISTING INFORMATION
        // =========================================================

        public int ListingID { get; set; }

        public int StudentID { get; set; }


        // =========================================================
        // STUDENT INFORMATION
        // Loaded from Users table
        // =========================================================

        public string FullName { get; set; } = "";

        public string Email { get; set; } = "";

        public string MobileNumber { get; set; } = "";

        public string ProfileAvatar { get; set; } = "";


        // =========================================================
        // ROOMMATE LISTING
        // =========================================================

        [Required(ErrorMessage = "Gender is required")]
        [StringLength(40)]
        public string Gender { get; set; } = "";


        [Required(ErrorMessage = "Location is required")]
        [StringLength(400)]
        public string Location { get; set; } = "";


        [Required(ErrorMessage = "Budget is required")]
        [Range(1, 1000000, ErrorMessage = "Enter a valid budget")]
        public decimal? Budget { get; set; }


        [StringLength(
            4000,
            ErrorMessage = "Lifestyle preferences cannot exceed 4000 characters")]
        public string LifestylePreferences { get; set; } = "";


        [StringLength(
            4000,
            ErrorMessage = "Other listing information cannot exceed 4000 characters")]
        public string OtherListingInformation { get; set; } = "";


        [StringLength(1000)]
        public string ProfilePhoto { get; set; } = "";


        // =========================================================
        // FILTERS
        // =========================================================

        public string FilterLocation { get; set; } = "";

        public string FilterGender { get; set; } = "";

        public decimal? MinBudget { get; set; }

        public decimal? MaxBudget { get; set; }
    }
}