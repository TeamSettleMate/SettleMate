using Microsoft.AspNetCore.WebUtilities;

namespace SettleMate.Web.Common
{
    /// <summary>Maps bookmarks and stored notification URLs to role-specific pages.</summary>
    public static class WorkspaceRoutes
    {
        public static string Resolve(string role, string url)
        {
            if (!(role == "User" || role == "Owner" || role == "Admin"))
            {
                return url;
            }

            string[]? split = url.Split('?', 2);
            string? path = split[0].TrimEnd('/');
            string? query;
            if (split.Length == 2)
            {
                query = "?" + split[1];
            }
            else
            {
                query = "";
            }

            Dictionary<string, StringValues>? values = QueryHelpers.ParseQuery(query);
            string? kind;
            if (values.TryGetValue("kind", out var k))
            {
                kind = k.ToString();
            }
            else
            {
                kind = "Property";
            }

            string? feature;
            if (kind is "Item")
            {
                feature = "Marketplace";
            }
            else if (kind is "Room" or "Finder")
            {
                feature = "Roommate";
            }
            else
            {
                feature = "Property";
            }

            string[]? parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts[0].Equals("Workspace", StringComparison.OrdinalIgnoreCase))
            {
                string? action;
                if (parts.Length > 1)
                {
                    action = parts[1].ToLowerInvariant();
                }
                else
                {
                    action = "index";
                }

                string? id;
                if (parts.Length > 2)
                {
                    id = "/" + parts[2];
                }
                else
                {
                    id = "";
                }

                string? destination;
                if (action is "index")
                {
                    destination = "Dashboard/Index";
                }
                else if (action is "listings")
                {
                    destination = feature + "/Manage";
                }
                else if (action is "edit")
                {
                    destination = feature + "/Edit";
                }
                else if (action is "visits")
                {
                    destination = "Visit/Index";
                }
                else if (action is "reviews")
                {
                    destination = "Review/Index";
                }
                else if (action is "notifications")
                {
                    destination = "Notification/Index";
                }
                else if (action is "messages")
                {
                    destination = "Message/Index";
                }
                else if (action is "thread")
                {
                    destination = "Message/Thread";
                }
                else if (action is "support")
                {
                    destination = "Support/Index";
                }
                else if (action is "ticket")
                {
                    destination = "Support/Ticket";
                }
                else if (action is "orders")
                {
                    destination = "Order/Index";
                }
                else if (action is "order")
                {
                    destination = "Order/Details";
                }
                else if (action is "payments")
                {
                    destination = "Order/Payments";
                }
                else if (action is "cart")
                {
                    destination = "Cart/Index";
                }
                else if (action is "checkout")
                {
                    destination = "Cart/Checkout";
                }
                else
                {
                    destination = null;
                }

                if (role == "User" && kind == "Property" && action == "listings")
                {
                    destination = "Property/Browse";
                }

                if (destination == null)
                {
                    return url;
                }
                else
                {
                    return $"/{role}/{destination}{id}{query}";
                }
            }

            if (path.Equals("/Account/Profile", StringComparison.OrdinalIgnoreCase))
            {
                return $"/{role}/Profile/Index{query}";
            }

            if (path.Equals("/Account/Settings", StringComparison.OrdinalIgnoreCase))
            {
                return $"/{role}/Settings/Index{query}";
            }

            if (path.Equals("/Admin/Accounts", StringComparison.OrdinalIgnoreCase) && role == "Admin")
            {
                return $"/Admin/{(values.TryGetValue("role", out var r) && r == "Owner" ? "Owner" : "User")}/Index{query}";
            }

            if (role == "User")
            {
                if (path.Equals("/Catalog/Browse", StringComparison.OrdinalIgnoreCase))
                {
                    return $"/User/{feature}/Browse{query}";
                }

                if (path.Equals("/Catalog/Wishlist", StringComparison.OrdinalIgnoreCase))
                {
                    return $"/User/Wishlist/Index{query}";
                }

                if (path.Equals("/Catalog/Marketplace", StringComparison.OrdinalIgnoreCase))
                {
                    return $"/User/Marketplace/Index{query}";
                }
            }

            return url;
        }
    }
}
