using SettleMate.Web.Models;

namespace SettleMate.Web.Models
{
    /// <summary>Editor input adds uploads and transient UI state to persisted listing fields.</summary>
    public class Listing : ListingData
    {
        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public ListingData Data { get; set; } = new ListingData();
        public string SellerStatus { get; set; } = "";
        public int Months { get; set; }
        public decimal Rating { get; set; }
        public string Image { get; set; } = "";
        public string OwnerStatus { get; set; } = "";
        public string OwnerPhone { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public string OwnerName { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Details { get; set; } = "";
        public string Reason { get; set; } = "";
        public bool Available { get; set; }
        public string Status { get; set; } = "";
        public long OwnerId { get; set; }
        public List<IFormFile> Images { get; set; } = new List<IFormFile>();
        public long[] RemoveImageIds { get; set; } = new long[0];
        public IFormFile? IdentityDocument { get; set; }
        public IFormFile? PropertyProof { get; set; }
        public IFormFile? AuthorizationDocument { get; set; }
        public string SaveMode { get; set; } = "Submit";
        public List<StoredFile> ExistingImages { get; set; } = new List<StoredFile>();
        public List<StoredFile> ExistingDocuments { get; set; } = new List<StoredFile>();
        public string RejectionReason { get; set; } = "";
    }
}
