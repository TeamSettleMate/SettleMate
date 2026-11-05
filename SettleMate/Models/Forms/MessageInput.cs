using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class MessageInput : RecordAction
    {
        [Required, StringLength(2000, MinimumLength = 1)]
        public string Message { get; set; } = "";
    }
}
