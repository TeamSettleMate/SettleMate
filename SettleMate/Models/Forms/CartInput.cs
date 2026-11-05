using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class CartInput : RecordAction
    {
        [Range(1, 24)]
        public int Number { get; set; } = 1;
    }
}
