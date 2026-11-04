namespace SettleMate.Web.Models
{
    public class NavigationLink
    {
        public string Url { get; set; }
        public string Text { get; set; }
        public string Icon { get; set; }

        public NavigationLink(string url, string text, string icon)
        {
            Url = url;
            Text = text;
            Icon = icon;
        }
    }
}
