using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;

namespace SettleMate.Web.Configuration
{
    public class CookieValidator : CookieAuthenticationEvents
    {
        private readonly IConfiguration configuration;
        public CookieValidator(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            long id;
            bool valid = false;
            if (long.TryParse(context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id))
            {
                using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("SELECT Status, SecurityStamp FROM dbo.Accounts WHERE Id=@Id", connection))
                    {
                        command.Parameters.AddWithValue("@Id", id);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string status = reader.GetString(0);
                                valid = (status == "Active" || status == "Approved") && reader.GetGuid(1).ToString() == context.Principal?.FindFirst("Stamp")?.Value;
                            }
                        }
                    }
                }
            }

            if (!valid)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        }
    }
}
