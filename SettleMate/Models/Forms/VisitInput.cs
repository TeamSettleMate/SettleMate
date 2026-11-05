using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class VisitInput : RecordAction
    {
        [Required]
        public DateTime? At { get; set; }

        [StringLength(1000)]
        public string Message { get; set; } = "";
    }
}
