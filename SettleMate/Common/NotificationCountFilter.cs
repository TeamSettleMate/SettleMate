using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SettleMate.Web.Common
{
    // Puts the signed-in account's unread notification count in ViewData for the layout.
    public class NotificationCountFilter : IResultFilter
    {
        private readonly IConfiguration configuration;
        public NotificationCountFilter(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is not ViewResult view)
            {
                return;
            }

            long id = CurrentAccount.Id(context.HttpContext.User);
            if (id == 0)
            {
                return;
            }

            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.Notifications WHERE AccountId=@id AND IsRead=0", connection))
                {
                    command.Parameters.AddWithValue("@id", id);
                    view.ViewData["NotificationCount"] = Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
        }
    }
}
