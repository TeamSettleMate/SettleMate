namespace SettleMate.Web.Common
{
    // Thrown when a business rule fails. The message is safe to show to the user.
    public class RuleException : Exception
    {
        public RuleException(string message) : base(message)
        {
        }
    }
}
