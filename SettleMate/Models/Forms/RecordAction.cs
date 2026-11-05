using System.ComponentModel.DataAnnotations;

namespace SettleMate.Web.Models
{
    public class RecordAction
    {
        [Range(1, long.MaxValue)]
        public long Id { get; set; }
        public string ReturnUrl { get; set; } = "/Workspace";
    }
}
