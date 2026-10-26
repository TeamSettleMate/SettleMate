using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    /// <summary>Persisted Listings.Details JSON contract, compatible with existing rows.</summary>
    public class ListingData
    {
        public long Id { get; set; }
        public string Kind { get; set; } = "Property";

        [Required, StringLength(100)]
        public string Title { get; set; } = "";

        [Required, StringLength(1200, MinimumLength = 20)]
        public string Description { get; set; } = "";

        [Required, StringLength(40)]
        public string Type { get; set; } = "Room";

        [Required, StringLength(80)]
        public string City { get; set; } = "";

        [Required, StringLength(80)]
        public string State { get; set; } = "";

        [StringLength(300)]
        public string Address { get; set; } = "";

        [RegularExpression(@"^\d{6}$")]
        public string? Pincode { get; set; }

        [Range(1, 10000000)]
        public decimal Price { get; set; }

        [Range(0, 10000000)]
        public decimal Deposit { get; set; }

        [Range(0, 1000000)]
        public decimal Maintenance { get; set; }
        public string Basis { get; set; } = "Per Room";
        public string Gender { get; set; } = "Any";
        public string Furnishing { get; set; } = "Unfurnished";
        public string RoomType { get; set; } = "Single";

        [Range(0, 20)]
        public int Bhk { get; set; }

        [Range(0, 20)]
        public int Bedrooms { get; set; }

        [Range(-5, 200)]
        public int Floor { get; set; }

        [Range(0, 1000)]
        public int Beds { get; set; }
        public bool AttachedBathroom { get; set; }
        public bool Food { get; set; }

        [StringLength(100)]
        public string Meals { get; set; } = "";

        [StringLength(40)]
        public string Curfew { get; set; } = "";
        public DateTime? AvailableFrom { get; set; }

        [Range(0, 120)]
        public int MinimumStay { get; set; }

        [Range(0, 365)]
        public int NoticePeriod { get; set; }

        [StringLength(100)]
        public string Electricity { get; set; } = "";

        [StringLength(100)]
        public string Water { get; set; } = "";

        [StringLength(100)]
        public string Landmark { get; set; } = "";

        [Range(-90, 90)]
        public decimal? Latitude { get; set; }

        [Range(-180, 180)]
        public decimal? Longitude { get; set; }

        [StringLength(1000)]
        public string Rules { get; set; } = "";
        public bool Visitors { get; set; }
        public bool Smoking { get; set; }
        public bool Pets { get; set; }

        [StringLength(100)]
        public string AgreementType { get; set; } = "";
        public string Condition { get; set; } = "Good";
        public string Category { get; set; } = "Other";
        public string OwnerRelation { get; set; } = "Owner";
        public string DocumentType { get; set; } = "Government ID";

        [StringLength(100)]
        public string DocumentReference { get; set; } = "";

        [StringLength(500)]
        public string AdminNote { get; set; } = "";
        public bool Declaration1 { get; set; }
        public bool Declaration2 { get; set; }
        public bool Declaration3 { get; set; }
        public bool Declaration4 { get; set; }
        public string[] Amenities { get; set; } = new string[0];
    }
}
