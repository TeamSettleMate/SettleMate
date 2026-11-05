using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class SupportReply : RecordAction
    {
        [Required, StringLength(2000, MinimumLength = 5)]
        public string Message { get; set; } = "";
        public bool Email { get; set; }
    }
}
