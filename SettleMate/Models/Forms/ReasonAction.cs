using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class ReasonAction : RecordAction
    {
        [Required, StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = "";
    }
}
