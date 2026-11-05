using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class OrderStatus : RecordAction
    {
        [Required, RegularExpression("^(Processing|Completed)$")]
        public string Value { get; set; } = "";
    }
}
