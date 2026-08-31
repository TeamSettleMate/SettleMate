namespace SettleMate.Models
{
    // Stores error information
    public class ErrorViewModel
    {
        // Store error request ID
        public string? RequestId { get; set; }

        // Check if RequestId has a value
        public bool ShowRequestId =>
            !string.IsNullOrEmpty(RequestId);
    }
}