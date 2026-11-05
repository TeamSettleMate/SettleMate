namespace SettleMate.Web.Models
{
    public class ListingSearch
    {
        public string Kind { get; set; } = "Property";
        public string Search { get; set; } = "";
        public string City { get; set; } = "";
        public string Type { get; set; } = "";
        public string Gender { get; set; } = "";
        public string Furnishing { get; set; } = "";
        public bool Food { get; set; }
        public int Bhk { get; set; }
        public string Amenity { get; set; } = "";
        public DateTime? MoveIn { get; set; }
        public decimal? Min { get; set; }
        public decimal? Max { get; set; }
        public string Sort { get; set; } = "";
        public int Page { get; set; } = 1;
        public string Category { get; set; } = "";
        public string Condition { get; set; } = "";
        public string RoomType { get; set; } = "";
    }
}
