using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class ModerationAction : RecordAction
    {
        [Required, StringLength(80)]
        public string Value { get; set; } = "";

        [StringLength(500)]
        public string Reason { get; set; } = "";
    }
}
