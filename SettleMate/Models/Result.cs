namespace SettleMate.Web.Models
{
    public class Result
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long Id { get; set; }
        public string? Proof { get; set; }

        public Result(bool success, string message, long id = 0, string? proof = null)
        {
            Success = success;
            Message = message;
            Id = id;
            Proof = proof;
        }
    }
}
