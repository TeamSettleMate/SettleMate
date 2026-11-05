using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class VisitStatus : RecordAction
    {
        [Required, RegularExpression("^(Approved|Rejected|Completed)$")]
        public string Value { get; set; } = "";
        public DateTime? At { get; set; }

        [StringLength(1000)]
        public string Message { get; set; } = "";

        [StringLength(500)]
        public string Reason { get; set; } = "";
    }
}
