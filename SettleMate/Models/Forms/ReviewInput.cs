using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class ReviewInput : RecordAction
    {
        [Range(1, 5)]
        public int Number { get; set; }

        [Required, StringLength(1500, MinimumLength = 10)]
        public string Message { get; set; } = "";
    }
}
