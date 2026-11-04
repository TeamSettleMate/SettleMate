using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SettleMate.Web.Configuration
{
    public class NotificationCountFilter : IActionFilter
    {
        private readonly IConfiguration configuration;
        public NotificationCountFilter(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            Controller? controller = context.Controller as Controller;
            if (controller == null)
            {
                return;
            }

            controller.ViewData["UnreadCount"] = 0;
            controller.ViewData["Avatar"] = 1;
            long id;
            if (!long.TryParse(context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id))
            {
                return;
            }

            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM dbo.Notifications WHERE AccountId=@Id AND IsRead=0", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    controller.ViewData["UnreadCount"] = Convert.ToInt32(command.ExecuteScalar());
                }
                // Load the avatar picture number (1-8) shown in the header.
                using (SqlCommand command = new SqlCommand("SELECT Avatar FROM dbo.Accounts WHERE Id=@Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    object? avatar = command.ExecuteScalar();
                    if (avatar != null)
                    {
                        controller.ViewData["Avatar"] = Convert.ToInt32(avatar);
                    }
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
