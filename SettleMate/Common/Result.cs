namespace SettleMate.Web.Common
{
    // Outcome returned to the browser by JSON endpoints.
    public class Result
    {
        public Result(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; set; }
        public string Message { get; set; }
        public string? Proof { get; set; }
    }
}
