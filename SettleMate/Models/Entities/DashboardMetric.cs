namespace SettleMate.Web.Models
{
    public class DashboardMetric
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
        public string Url { get; set; } = "";
        public string Chart { get; set; } = "";
    }
}
