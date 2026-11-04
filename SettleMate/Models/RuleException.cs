namespace SettleMate.Web.Models
{
    // Expected input/business-rule errors are shown safely to the user.
    public class RuleException : Exception
    {
        public RuleException(string message) : base(message)
        {
        }
    }
}
