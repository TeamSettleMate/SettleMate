using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace SettleMate.Web.Configuration
{
    public static class CurrentAccount
    {
        public static long Id(ClaimsPrincipal user)
        {
            if (long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            {
                return id;
            }
            else
            {
                return 0;
            }
        }

        public static string Role(ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Role) ?? "Public";
        }
    }
}
