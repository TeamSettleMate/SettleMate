namespace SettleMate.Web.Models
{
    public class StoredFile
    {
        public StoredFile()
        {
        }

        public StoredFile(string name, string originalName, string contentType, bool isPrivate, string purpose)
        {
            Name = name;
            OriginalName = originalName;
            ContentType = contentType;
            Private = isPrivate;
            Purpose = purpose;
        }

        public long Id { get; set; }
        public long OwnerId { get; set; }
        public long? ListingId { get; set; }
        public string Name { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public bool Private { get; set; }
        public string Purpose { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
