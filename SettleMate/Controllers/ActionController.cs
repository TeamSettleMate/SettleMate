using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SettleMate.Web.Common;
using SettleMate.Web.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace SettleMate.Web.Controllers
{
    [Authorize]
    public class ActionController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public ActionController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpPost]
        public IActionResult Run(string operation, long id, string value = "", string message = "", DateTime? at = null, int number = 1, bool email = false, string reason = "", string returnUrl = "/Workspace")
        {
            WorkflowLogic_Execute(operation, User, id, value, string.IsNullOrWhiteSpace(reason) ? message : reason, at, number, email);
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Updated successfully." });
            }

            TempData["Success"] = "Updated successfully.";
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/Workspace");
        }

        [HttpPost]
        public IActionResult Conversation(long listing)
        {
            long id = WorkflowLogic_Conversation(User, listing);
            return Redirect("/Workspace/Thread/" + id);
        }

        private void WorkflowLogic_Execute(string action, ClaimsPrincipal user, long id, string value, string message, DateTime? at, int number, bool email)
        {
            if (action == "visit")
            {
                {
                    WorkflowLogic_RequestVisit(user, id, message, at);
                    return;
                }
            }

            if (action == "visit-status")
            {
                {
                    WorkflowLogic_UpdateVisit(user, id, value, message, at);
                    return;
                }
            }

            if (action == "review")
            {
                {
                    WorkflowLogic_CreateReview(user, id, message, number);
                    return;
                }
            }

            if (action == "report-review")
            {
                {
                    WorkflowLogic_ReportReview(user, id, message);
                    return;
                }
            }

            if (action == "wishlist")
            {
                {
                    WorkflowLogic_ToggleWishlist(user, id);
                    return;
                }
            }

            if (action == "availability")
            {
                {
                    WorkflowLogic_ToggleAvailability(user, id);
                    return;
                }
            }

            if (action == "read")
            {
                {
                    WorkflowLogic_MarkNotificationRead(user, id);
                    return;
                }
            }

            if (action == "message")
            {
                {
                    WorkflowLogic_SendMessage(user, id, message);
                    return;
                }
            }

            if (action == "reply")
            {
                {
                    WorkflowLogic_ReplyToTicket(user, id, message, email);
                    return;
                }
            }

            if (action == "cart-add")
            {
                {
                    WorkflowLogic_AddToCart(user, id, number);
                    return;
                }
            }

            if (action == "cart-remove")
            {
                {
                    WorkflowLogic_RemoveFromCart(user, id);
                    return;
                }
            }

            if (action == "order-status")
            {
                {
                    WorkflowLogic_UpdateOrder(user, id, value);
                    return;
                }
            }

            if (action == "moderate")
            {
                {
                    WorkflowLogic_Moderate(user, id, value, message);
                    return;
                }
            }

            if (action == "delete-listing")
            {
                {
                    WorkflowLogic_DeleteListing(user, id, message);
                    return;
                }
            }

            throw new RuleException("Unknown action.");
        }

        private long WorkflowLogic_Conversation(ClaimsPrincipal user, long listing)
        {
            WorkflowLogic_Require(user, "User");
            return WorkflowSql_StartConversation(CurrentAccount.Id(user), listing);
        }

        private void WorkflowLogic_RequestVisit(ClaimsPrincipal user, long id, string message, DateTime? at)
        {
            if (at.HasValue)
            {
                at = DateTime.SpecifyKind(at.Value, DateTimeKind.Unspecified).AddHours(-5.5);
            }

            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            if (at == null || at < DateTime.UtcNow || at > DateTime.UtcNow.AddYears(1))
            {
                throw new RuleException("Choose a future visit within one year.");
            }

            WorkflowSql_RequestVisit(actor, id, at.Value, WorkflowLogic_Text(message, 0, 1000));
        }

        private void WorkflowLogic_UpdateVisit(ClaimsPrincipal user, long id, string value, string message, DateTime? at)
        {
            if (at.HasValue)
            {
                at = DateTime.SpecifyKind(at.Value, DateTimeKind.Unspecified).AddHours(-5.5);
            }

            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            if (value == "Rejected")
            {
                message = WorkflowLogic_Text(message, 5, 500);
            }
            else
            {
                message = WorkflowLogic_Text(message, 0, 1000);
            }

            WorkflowSql_VisitStatus(id, actor, value, at, message);
        }

        private void WorkflowLogic_CreateReview(ClaimsPrincipal user, long id, string message, int number)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            if (number < 1 || number > 5)
            {
                throw new RuleException("Choose 1–5 stars.");
            }

            WorkflowSql_Review(actor, id, number, WorkflowLogic_Text(message, 10, 1500));
        }

        private void WorkflowLogic_ReportReview(ClaimsPrincipal user, long id, string message)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            WorkflowSql_Report(actor, id, WorkflowLogic_Text(message, 5, 500));
        }

        private void WorkflowLogic_ToggleWishlist(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            ListingLogic_Wishlist(id, user);
        }

        private void WorkflowLogic_ToggleAvailability(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            ListingLogic_Availability(id, user);
        }

        private void WorkflowLogic_MarkNotificationRead(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowSql_MarkRead(actor, id);
        }

        private void WorkflowLogic_SendMessage(ClaimsPrincipal user, long id, string message)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowSql_SendMessage(actor, id, WorkflowLogic_Text(message, 1, 2000));
        }

        private void WorkflowLogic_ReplyToTicket(ClaimsPrincipal user, long id, string message, bool email)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "Admin");
            WorkflowSql_Reply(actor, id, WorkflowLogic_Text(message, 5, 2000), email);
        }

        private void WorkflowLogic_AddToCart(ClaimsPrincipal user, long id, int number)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            if (number < 1 || number > 24)
            {
                throw new RuleException("Rental duration must be 1–24 months.");
            }

            WorkflowSql_AddCart(actor, id, number);
        }

        private void WorkflowLogic_RemoveFromCart(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            WorkflowSql_RemoveCart(actor, id);
        }

        private void WorkflowLogic_UpdateOrder(ClaimsPrincipal user, long id, string value)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User", "Admin");
            WorkflowSql_OrderStatus(actor, CurrentAccount.Role(user), id, value);
        }

        private void WorkflowLogic_Moderate(ClaimsPrincipal user, long id, string value, string message)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "Admin");
            string[]? parts = value.Split(':');
            if (parts.Length != 2)
            {
                throw new RuleException("Invalid action.");
            }

            message = WorkflowLogic_Text(message, new[] { "Rejected", "Removed", "Blocked", "Unpublished" }.Contains(parts[1]) ? 5 : 0, 500);
            WorkflowSql_Moderation(actor, parts[0], id, parts[1], message);
        }

        private void WorkflowLogic_DeleteListing(ClaimsPrincipal user, long id, string message)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "Owner", "User");
            WorkflowSql_DeleteListing(actor, CurrentAccount.Role(user), id, WorkflowLogic_Text(message, 5, 500));
        }

        private static void WorkflowLogic_Require(ClaimsPrincipal user, params string[] roles)
        {
            if (!roles.Contains(CurrentAccount.Role(user)))
            {
                throw new RuleException("You do not have access to this action.");
            }
        }

        private long WorkflowSql_StartConversation(long actor, long listing)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt]
                FROM dbo.Listings l
                JOIN dbo.Accounts a ON a.Id=l.OwnerId
                WHERE l.Id=@listing
                AND l.Status='Approved'
                AND l.OwnerId<>@actor
                AND a.Status IN('Active','Approved')", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Listing databaseRow = new Listing();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    if (sqlReader1["Kind"] != DBNull.Value)
                                    {
                                        databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["Description"] != DBNull.Value)
                                    {
                                        databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                                    }

                                    if (sqlReader1["Type"] != DBNull.Value)
                                    {
                                        databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                                    }

                                    if (sqlReader1["City"] != DBNull.Value)
                                    {
                                        databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                                    }

                                    if (sqlReader1["State"] != DBNull.Value)
                                    {
                                        databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                                    }

                                    if (sqlReader1["Address"] != DBNull.Value)
                                    {
                                        databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                                    }

                                    if (sqlReader1["Price"] != DBNull.Value)
                                    {
                                        databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Available"] != DBNull.Value)
                                    {
                                        databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                                    }

                                    if (sqlReader1["Reason"] != DBNull.Value)
                                    {
                                        databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                                    }

                                    if (sqlReader1["Details"] != DBNull.Value)
                                    {
                                        databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["UpdatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                                    }

                                    databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var l = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Listing unavailable.");
                        List<Conversation> sqlResult2 = new List<Conversation>();
                        using (SqlCommand sqlCommand2 = new SqlCommand("SELECT Id FROM dbo.Conversations WHERE UserId=@actor AND ListingId=@listing", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                            {
                                while (sqlReader2.Read())
                                {
                                    Conversation databaseRow = new Conversation();
                                    if (sqlReader2["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                                    }

                                    sqlResult2.Add(databaseRow);
                                }
                            }
                        }

                        var old = sqlResult2;
                        if (old.Count > 0)
                        {
                            long transactionResult = old[0].Id;
                            transaction.Commit();
                            return transactionResult;
                        }

                        {
                            List<Conversation> sqlResult3 = new List<Conversation>();
                            using (SqlCommand sqlCommand3 = new SqlCommand(@"INSERT dbo.Conversations(UserId,OwnerId,ListingId)
                OUTPUT INSERTED.Id
                VALUES(@actor,@owner,@listing)", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@owner", ((object? )l.OwnerId ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                                using (SqlDataReader sqlReader3 = sqlCommand3.ExecuteReader())
                                {
                                    while (sqlReader3.Read())
                                    {
                                        Conversation databaseRow = new Conversation();
                                        if (sqlReader3["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader3["Id"]);
                                        }

                                        sqlResult3.Add(databaseRow);
                                    }
                                }
                            }

                            long transactionResult = (sqlResult3)[0].Id;
                            transaction.Commit();
                            return transactionResult;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static string WorkflowLogic_Text(string value, int min, int max)
        {
            value = (value ?? "").Trim();
            if (value.Length < min || value.Length > max)
            {
                throw new RuleException($"Enter between {min} and {max} characters.");
            }

            return value;
        }

        private long WorkflowSql_RequestVisit(long user, long listing, DateTime at, string message)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt]
                FROM dbo.Listings l
                JOIN dbo.Accounts a ON a.Id=l.OwnerId
                WHERE l.Id=@listing
                AND l.Status='Approved'
                AND l.Available=1
                AND l.Kind IN('Property','Room')
                AND l.OwnerId<>@user
                AND a.Status IN('Active','Approved')", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Listing databaseRow = new Listing();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    if (sqlReader1["Kind"] != DBNull.Value)
                                    {
                                        databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["Description"] != DBNull.Value)
                                    {
                                        databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                                    }

                                    if (sqlReader1["Type"] != DBNull.Value)
                                    {
                                        databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                                    }

                                    if (sqlReader1["City"] != DBNull.Value)
                                    {
                                        databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                                    }

                                    if (sqlReader1["State"] != DBNull.Value)
                                    {
                                        databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                                    }

                                    if (sqlReader1["Address"] != DBNull.Value)
                                    {
                                        databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                                    }

                                    if (sqlReader1["Price"] != DBNull.Value)
                                    {
                                        databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Available"] != DBNull.Value)
                                    {
                                        databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                                    }

                                    if (sqlReader1["Reason"] != DBNull.Value)
                                    {
                                        databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                                    }

                                    if (sqlReader1["Details"] != DBNull.Value)
                                    {
                                        databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["UpdatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                                    }

                                    databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var l = (sqlResult1).FirstOrDefault();
                        if (l == null)
                        {
                            throw new RuleException("This listing is not available for a visit.");
                        }

                        List<VisitRequest> sqlResult2 = new List<VisitRequest>();
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"INSERT dbo.VisitRequests(UserId,ListingId,VisitAt,Message)
                OUTPUT INSERTED.Id
                VALUES(@user,@listing,@at,@message)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@at", ((object? )at ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@message", ((object? )message ?? DBNull.Value));
                            using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                            {
                                while (sqlReader2.Read())
                                {
                                    VisitRequest databaseRow = new VisitRequest();
                                    if (sqlReader2["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                                    }

                                    sqlResult2.Add(databaseRow);
                                }
                            }
                        }

                        var row = sqlResult2;
                        WorkflowSql_Notify(connection, transaction, l.OwnerId, "New visit request for " + l.Title, "/Workspace/Visits");
                        {
                            long transactionResult = row[0].Id;
                            transaction.Commit();
                            return transactionResult;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_VisitStatus(long id, long actor, string status, DateTime? at, string message)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<VisitRequest> sqlResult1 = new List<VisitRequest>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT v.[Id], v.[UserId], v.[ListingId], v.[VisitAt], v.[Message], v.[Response], v.[Status], v.[CreatedAt],l.OwnerId
                FROM dbo.VisitRequests v WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.Listings l ON l.Id=v.ListingId
                WHERE v.Id=@id
                AND l.OwnerId=@actor", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    VisitRequest databaseRow = new VisitRequest();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["UserId"] != DBNull.Value)
                                    {
                                        databaseRow.UserId = Convert.ToInt64(sqlReader1["UserId"]);
                                    }

                                    if (sqlReader1["ListingId"] != DBNull.Value)
                                    {
                                        databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                                    }

                                    if (sqlReader1["VisitAt"] != DBNull.Value)
                                    {
                                        databaseRow.VisitAt = Convert.ToDateTime(sqlReader1["VisitAt"]);
                                    }

                                    if (sqlReader1["Message"] != DBNull.Value)
                                    {
                                        databaseRow.Message = Convert.ToString(sqlReader1["Message"])!;
                                    }

                                    if (sqlReader1["Response"] != DBNull.Value)
                                    {
                                        databaseRow.Response = Convert.ToString(sqlReader1["Response"])!;
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var row = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Visit not found.");
                        if (!(row.Status == "Pending" && (status == "Approved" || status == "Rejected") || row.Status == "Approved" && status == "Completed" && row.VisitAt <= DateTime.UtcNow))
                        {
                            throw new RuleException("This visit cannot make that transition.");
                        }

                        if (status == "Approved" && (at == null || at < DateTime.UtcNow))
                        {
                            throw new RuleException("Choose a future visit time.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"UPDATE dbo.VisitRequests
                SET Status=@status,VisitAt=COALESCE(@at,VisitAt),Response=@message
                WHERE Id=@id", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@at", ((object? )(status == "Approved" ? at : null) ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@message", ((object? )message ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        WorkflowSql_Notify(connection, transaction, row.UserId, "Your visit request is " + (status ?? "").ToLowerInvariant() + ". " + message, "/Workspace/Visits");
                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_Review(long user, long listing, int rating, string text)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<VisitRequest> sqlResult1 = new List<VisitRequest>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT v.Id,l.OwnerId
                FROM dbo.VisitRequests v
                JOIN dbo.Listings l ON l.Id=v.ListingId
                WHERE v.UserId=@user
                AND v.ListingId=@listing
                AND v.Status='Completed'
                AND l.Kind='Property'", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    VisitRequest databaseRow = new VisitRequest();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var visit = sqlResult1;
                        if (visit.Count == 0)
                        {
                            throw new RuleException("Only users with a completed property visit can review.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand("INSERT dbo.Reviews(UserId,ListingId,Rating,Text) VALUES(@user,@listing,@rating,@text)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@rating", ((object? )rating ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@text", ((object? )text ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        WorkflowSql_Notify(connection, transaction, visit[0].OwnerId, "A new property review was posted.", "/Workspace/Reviews");
                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_Report(long user, long review, string reason)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"INSERT dbo.ReviewReports(ReviewId,UserId,Reason) SELECT Id,@user,@reason
                FROM dbo.Reviews
                WHERE Id=@review
                AND Removed=0", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@review", ((object? )review ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }
        }

        private void ListingLogic_Wishlist(long id, ClaimsPrincipal user)
        {
            if (CurrentAccount.Role(user) != "User")
            {
                throw new RuleException("Sign in as a User.");
            }

            var row = ListingLogic_Accessible(id, user);
            if (row.Status != "Approved")
            {
                throw new RuleException("This listing is unavailable.");
            }

            ListingSql_ToggleWishlist(CurrentAccount.Id(user), id);
        }

        private void ListingLogic_Availability(long id, ClaimsPrincipal user)
        {
            {
                ListingSql_Availability(CurrentAccount.Id(user), id);
                return;
            }
        }

        private void WorkflowSql_MarkRead(long actor, long id)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("UPDATE dbo.Notifications SET IsRead=1 WHERE AccountId=@actor AND Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }
        }

        private void WorkflowSql_SendMessage(long actor, long conversation, string text)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Conversation> sqlResult1 = new List<Conversation>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [ListingId], [UserId], [OwnerId], [CreatedAt] FROM dbo.Conversations WHERE Id=@conversation AND (UserId=@actor OR OwnerId=@actor)", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@conversation", ((object? )conversation ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Conversation databaseRow = new Conversation();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["ListingId"] != DBNull.Value)
                                    {
                                        databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                                    }

                                    if (sqlReader1["UserId"] != DBNull.Value)
                                    {
                                        databaseRow.UserId = Convert.ToInt64(sqlReader1["UserId"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var row = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Conversation not found.");
                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand("INSERT dbo.Messages(ConversationId,SenderId,Text) VALUES(@conversation,@actor,@text)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@conversation", ((object? )conversation ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@text", ((object? )text ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        WorkflowSql_Notify(connection, transaction, row.UserId == actor ? row.OwnerId : row.UserId, "You have a new message.", "/Workspace/Thread/" + conversation, false);
                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_Reply(long admin, long ticket, string text, bool email)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Ticket> sqlResult1 = new List<Ticket>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [AccountId], [FullName], [Email], [Subject], [Message], [Status], [CreatedAt] FROM dbo.Tickets WHERE Id=@ticket", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@ticket", ((object? )ticket ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Ticket databaseRow = new Ticket();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["AccountId"] != DBNull.Value)
                                    {
                                        databaseRow.AccountId = Convert.ToInt64(sqlReader1["AccountId"]);
                                    }

                                    if (sqlReader1["FullName"] != DBNull.Value)
                                    {
                                        databaseRow.FullName = Convert.ToString(sqlReader1["FullName"])!;
                                    }

                                    if (sqlReader1["Email"] != DBNull.Value)
                                    {
                                        databaseRow.Email = Convert.ToString(sqlReader1["Email"])!;
                                    }

                                    if (sqlReader1["Subject"] != DBNull.Value)
                                    {
                                        databaseRow.Subject = Convert.ToString(sqlReader1["Subject"])!;
                                    }

                                    if (sqlReader1["Message"] != DBNull.Value)
                                    {
                                        databaseRow.Message = Convert.ToString(sqlReader1["Message"])!;
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var t = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Ticket not found.");
                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"INSERT dbo.TicketReplies(TicketId,AdminId,Message)
                VALUES(@ticket,@admin,@text);UPDATE dbo.Tickets
                SET Status='Resolved'
                WHERE Id=@ticket", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@ticket", ((object? )ticket ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@admin", ((object? )admin ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@text", ((object? )text ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        if (t.AccountId > 0)
                        {
                            WorkflowSql_Notify(connection, transaction, t.AccountId.GetValueOrDefault(), "Support replied to ticket #" + ticket, "/Workspace/Ticket/" + ticket, false);
                        }

                        if (email)
                        {
                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("INSERT dbo.EmailOutbox(Recipient,Subject,Body) VALUES(@recipient,@subject,@text)", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@recipient", ((object? )t.Email ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@subject", ((object? )("SettleMate support #" + ticket) ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@text", ((object? )text ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }
                        }

                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_AddCart(long actor, long listing, int months)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt]
                FROM dbo.Listings l
                JOIN dbo.Accounts a ON a.Id=l.OwnerId
                WHERE l.Id=@listing
                AND l.Kind='Item'
                AND l.Status='Approved'
                AND l.Available=1
                AND l.OwnerId<>@actor
                AND a.Status='Active'", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Listing databaseRow = new Listing();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    if (sqlReader1["Kind"] != DBNull.Value)
                                    {
                                        databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["Description"] != DBNull.Value)
                                    {
                                        databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                                    }

                                    if (sqlReader1["Type"] != DBNull.Value)
                                    {
                                        databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                                    }

                                    if (sqlReader1["City"] != DBNull.Value)
                                    {
                                        databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                                    }

                                    if (sqlReader1["State"] != DBNull.Value)
                                    {
                                        databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                                    }

                                    if (sqlReader1["Address"] != DBNull.Value)
                                    {
                                        databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                                    }

                                    if (sqlReader1["Price"] != DBNull.Value)
                                    {
                                        databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Available"] != DBNull.Value)
                                    {
                                        databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                                    }

                                    if (sqlReader1["Reason"] != DBNull.Value)
                                    {
                                        databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                                    }

                                    if (sqlReader1["Details"] != DBNull.Value)
                                    {
                                        databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["UpdatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                                    }

                                    databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var l = (sqlResult1).FirstOrDefault() ?? throw new RuleException("This item is unavailable or belongs to you.");
                        if (l.Type != "Rent")
                        {
                            months = 1;
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"DELETE dbo.CartItems
                WHERE UserId=@actor
                AND ListingId=@listing;INSERT dbo.CartItems(UserId,ListingId,Months)
                VALUES(@actor,@listing,@months)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@months", ((object? )months ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_RemoveCart(long actor, long listing)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("DELETE dbo.CartItems WHERE UserId=@actor AND ListingId=@listing", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }
        }

        private void WorkflowSql_OrderStatus(long actor, string role, long line, string status)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<OrderItem> sqlResult1 = new List<OrderItem>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT i.[Id], i.[OrderId], i.[ListingId], i.[SellerId], i.[Title], i.[ListingType], i.[UnitPrice], i.[Deposit], i.[Months], i.[Amount], i.[Status], i.[Collected], i.[CreatedAt], i.[ProcessedAt], i.[CompletedAt],o.BuyerId
                FROM dbo.OrderItems i WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.Orders o ON o.Id=i.OrderId
                WHERE i.Id=@line
                AND (@role='Admin'
                OR i.SellerId=@actor)", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@line", ((object? )line ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    OrderItem databaseRow = new OrderItem();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OrderId"] != DBNull.Value)
                                    {
                                        databaseRow.OrderId = Convert.ToInt64(sqlReader1["OrderId"]);
                                    }

                                    if (sqlReader1["ListingId"] != DBNull.Value)
                                    {
                                        databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                                    }

                                    if (sqlReader1["SellerId"] != DBNull.Value)
                                    {
                                        databaseRow.SellerId = Convert.ToInt64(sqlReader1["SellerId"]);
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["ListingType"] != DBNull.Value)
                                    {
                                        databaseRow.ListingType = Convert.ToString(sqlReader1["ListingType"])!;
                                    }

                                    if (sqlReader1["UnitPrice"] != DBNull.Value)
                                    {
                                        databaseRow.UnitPrice = Convert.ToDecimal(sqlReader1["UnitPrice"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Months"] != DBNull.Value)
                                    {
                                        databaseRow.Months = Convert.ToInt32(sqlReader1["Months"]);
                                    }

                                    if (sqlReader1["Amount"] != DBNull.Value)
                                    {
                                        databaseRow.Amount = Convert.ToDecimal(sqlReader1["Amount"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Collected"] != DBNull.Value)
                                    {
                                        databaseRow.Collected = Convert.ToBoolean(sqlReader1["Collected"]);
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["ProcessedAt"] != DBNull.Value)
                                    {
                                        databaseRow.ProcessedAt = Convert.ToDateTime(sqlReader1["ProcessedAt"]);
                                    }

                                    if (sqlReader1["CompletedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CompletedAt = Convert.ToDateTime(sqlReader1["CompletedAt"]);
                                    }

                                    if (sqlReader1["BuyerId"] != DBNull.Value)
                                    {
                                        databaseRow.BuyerId = Convert.ToInt64(sqlReader1["BuyerId"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var row = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Order not found.");
                        if (!(row.Status == "Pending" && status == "Processing" || row.Status == "Processing" && status == "Completed"))
                        {
                            throw new RuleException("Invalid order transition.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"UPDATE dbo.OrderItems
                SET Status=@status,ProcessedAt=CASE WHEN @status='Processing' THEN SYSUTCDATETIME() ELSE ProcessedAt END,CompletedAt=CASE WHEN @status='Completed' THEN SYSUTCDATETIME() ELSE CompletedAt END,Collected=CASE WHEN @status='Completed' THEN 1 ELSE 0 END
                WHERE Id=@line;INSERT dbo.OrderHistory(OrderItemId,Status,ActorId)
                VALUES(@line,@status,@actor)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@line", ((object? )line ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        if (status == "Completed")
                        {
                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("UPDATE dbo.Listings SET Status='Sold',Available=0 WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@id", ((object? )row.ListingId ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }
                        }

                        WorkflowSql_Notify(connection, transaction, row.BuyerId, "Order #" + row.OrderId + " is " + (status ?? "").ToLowerInvariant(), "/Workspace/Order/" + row.OrderId);
                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_Moderation(long actor, string entity, long id, string status, string reason)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        long recipient = 0;
                        string label = "";
                        if (entity == "Account")
                        {
                            List<Account> sqlResult1 = new List<Account>();
                            using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [FullName], [Email], [Phone], [PasswordHash], [Role], [Status], [Reason], [EmailVerified], [SecurityStamp], [DateOfBirth], [Gender], [Address], [City], [State], [Pincode], [Avatar], [DocumentType], [CreatedAt], [NormalizedEmail] FROM dbo.Accounts WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND Role<>'Admin'", connection, transaction))
                            {
                                sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                                {
                                    while (sqlReader1.Read())
                                    {
                                        Account databaseRow = new Account();
                                        if (sqlReader1["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                        }

                                        if (sqlReader1["FullName"] != DBNull.Value)
                                        {
                                            databaseRow.FullName = Convert.ToString(sqlReader1["FullName"])!;
                                        }

                                        if (sqlReader1["Email"] != DBNull.Value)
                                        {
                                            databaseRow.Email = Convert.ToString(sqlReader1["Email"])!;
                                        }

                                        if (sqlReader1["Phone"] != DBNull.Value)
                                        {
                                            databaseRow.Phone = Convert.ToString(sqlReader1["Phone"])!;
                                        }

                                        if (sqlReader1["PasswordHash"] != DBNull.Value)
                                        {
                                            databaseRow.PasswordHash = Convert.ToString(sqlReader1["PasswordHash"])!;
                                        }

                                        if (sqlReader1["Role"] != DBNull.Value)
                                        {
                                            databaseRow.Role = Convert.ToString(sqlReader1["Role"])!;
                                        }

                                        if (sqlReader1["Status"] != DBNull.Value)
                                        {
                                            databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                        }

                                        if (sqlReader1["Reason"] != DBNull.Value)
                                        {
                                            databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                                        }

                                        if (sqlReader1["EmailVerified"] != DBNull.Value)
                                        {
                                            databaseRow.EmailVerified = Convert.ToBoolean(sqlReader1["EmailVerified"]);
                                        }

                                        if (sqlReader1["SecurityStamp"] != DBNull.Value)
                                        {
                                            databaseRow.SecurityStamp = (Guid)sqlReader1["SecurityStamp"];
                                        }

                                        if (sqlReader1["DateOfBirth"] != DBNull.Value)
                                        {
                                            databaseRow.DateOfBirth = Convert.ToDateTime(sqlReader1["DateOfBirth"]);
                                        }

                                        if (sqlReader1["Gender"] != DBNull.Value)
                                        {
                                            databaseRow.Gender = Convert.ToString(sqlReader1["Gender"])!;
                                        }

                                        if (sqlReader1["Address"] != DBNull.Value)
                                        {
                                            databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                                        }

                                        if (sqlReader1["City"] != DBNull.Value)
                                        {
                                            databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                                        }

                                        if (sqlReader1["State"] != DBNull.Value)
                                        {
                                            databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                                        }

                                        if (sqlReader1["Pincode"] != DBNull.Value)
                                        {
                                            databaseRow.Pincode = Convert.ToString(sqlReader1["Pincode"])!;
                                        }

                                        if (sqlReader1["Avatar"] != DBNull.Value)
                                        {
                                            databaseRow.Avatar = Convert.ToInt32(sqlReader1["Avatar"]);
                                        }

                                        if (sqlReader1["DocumentType"] != DBNull.Value)
                                        {
                                            databaseRow.DocumentType = Convert.ToString(sqlReader1["DocumentType"])!;
                                        }

                                        if (sqlReader1["CreatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                        }

                                        if (sqlReader1["NormalizedEmail"] != DBNull.Value)
                                        {
                                            databaseRow.NormalizedEmail = Convert.ToString(sqlReader1["NormalizedEmail"])!;
                                        }

                                        sqlResult1.Add(databaseRow);
                                    }
                                }
                            }

                            var a = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Account not found.");
                            string? from = a.Status;
                            string? role = a.Role;
                            bool allowed;
                            if (role == "User")
                            {
                                allowed = (from == "Active" && status == "Blocked" || from == "Blocked" && status == "Active");
                            }
                            else
                            {
                                allowed = (from == "PendingApproval" && (status == "Approved" || status == "Rejected") || from == "Approved" && status == "Blocked" || from == "Blocked" && status == "Approved" || from == "Rejected" && status == "PendingApproval");
                            }

                            if (!allowed)
                            {
                                throw new RuleException("Invalid account transition.");
                            }

                            List<StoredFile> sqlResult2 = new List<StoredFile>();
                            using (SqlCommand sqlCommand2 = new SqlCommand("SELECT Id FROM dbo.Files WHERE OwnerId=@id AND ListingId IS NULL AND Private=1", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                                {
                                    while (sqlReader2.Read())
                                    {
                                        StoredFile databaseRow = new StoredFile();
                                        if (sqlReader2["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                                        }

                                        sqlResult2.Add(databaseRow);
                                    }
                                }
                            }

                            if (role == "Owner" && status == "Approved" && (sqlResult2).Count == 0)
                            {
                                throw new RuleException("Review the owner identity document before approval.");
                            }

                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("UPDATE dbo.Accounts SET Status=@status,Reason=@reason,SecurityStamp=NEWID() WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }

                            recipient = id;
                            label = "Your account";
                        }
                        else if (entity == "Listing")
                        {
                            List<Listing> sqlResult4 = new List<Listing>();
                            using (SqlCommand sqlCommand4 = new SqlCommand(@"SELECT [Id], [OwnerId], [Kind], [Title], [Description], [Type], [City], [State], [Address], [Price], [Deposit], [Status], [Available], [Reason], [Details], [CreatedAt], [UpdatedAt] FROM dbo.Listings WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand4.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader4 = sqlCommand4.ExecuteReader())
                                {
                                    while (sqlReader4.Read())
                                    {
                                        Listing databaseRow = new Listing();
                                        if (sqlReader4["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader4["Id"]);
                                        }

                                        if (sqlReader4["OwnerId"] != DBNull.Value)
                                        {
                                            databaseRow.OwnerId = Convert.ToInt64(sqlReader4["OwnerId"]);
                                        }

                                        if (sqlReader4["Kind"] != DBNull.Value)
                                        {
                                            databaseRow.Kind = Convert.ToString(sqlReader4["Kind"])!;
                                        }

                                        if (sqlReader4["Title"] != DBNull.Value)
                                        {
                                            databaseRow.Title = Convert.ToString(sqlReader4["Title"])!;
                                        }

                                        if (sqlReader4["Description"] != DBNull.Value)
                                        {
                                            databaseRow.Description = Convert.ToString(sqlReader4["Description"])!;
                                        }

                                        if (sqlReader4["Type"] != DBNull.Value)
                                        {
                                            databaseRow.Type = Convert.ToString(sqlReader4["Type"])!;
                                        }

                                        if (sqlReader4["City"] != DBNull.Value)
                                        {
                                            databaseRow.City = Convert.ToString(sqlReader4["City"])!;
                                        }

                                        if (sqlReader4["State"] != DBNull.Value)
                                        {
                                            databaseRow.State = Convert.ToString(sqlReader4["State"])!;
                                        }

                                        if (sqlReader4["Address"] != DBNull.Value)
                                        {
                                            databaseRow.Address = Convert.ToString(sqlReader4["Address"])!;
                                        }

                                        if (sqlReader4["Price"] != DBNull.Value)
                                        {
                                            databaseRow.Price = Convert.ToDecimal(sqlReader4["Price"]);
                                        }

                                        if (sqlReader4["Deposit"] != DBNull.Value)
                                        {
                                            databaseRow.Deposit = Convert.ToDecimal(sqlReader4["Deposit"]);
                                        }

                                        if (sqlReader4["Status"] != DBNull.Value)
                                        {
                                            databaseRow.Status = Convert.ToString(sqlReader4["Status"])!;
                                        }

                                        if (sqlReader4["Available"] != DBNull.Value)
                                        {
                                            databaseRow.Available = Convert.ToBoolean(sqlReader4["Available"]);
                                        }

                                        if (sqlReader4["Reason"] != DBNull.Value)
                                        {
                                            databaseRow.Reason = Convert.ToString(sqlReader4["Reason"])!;
                                        }

                                        if (sqlReader4["Details"] != DBNull.Value)
                                        {
                                            databaseRow.Details = Convert.ToString(sqlReader4["Details"])!;
                                        }

                                        if (sqlReader4["CreatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.CreatedAt = Convert.ToDateTime(sqlReader4["CreatedAt"]);
                                        }

                                        if (sqlReader4["UpdatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader4["UpdatedAt"]);
                                        }

                                        databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                        sqlResult4.Add(databaseRow);
                                    }
                                }
                            }

                            var l = (sqlResult4).FirstOrDefault() ?? throw new RuleException("Listing not found.");
                            string? from = l.Status;
                            bool allowed = from == "PendingVerification" && (status == "Approved" || status == "Rejected") || from == "Approved" && (status == "Unpublished" || status == "Rejected" || status == "Removed") || from == "Rejected" && status == "Approved" || from == "Unpublished" && status == "Approved";
                            if (!allowed)
                            {
                                throw new RuleException("Invalid listing transition.");
                            }

                            int sqlResult5;
                            using (SqlCommand sqlCommand5 = new SqlCommand("UPDATE dbo.Listings SET Status=@status,Reason=@reason WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand5.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                                sqlCommand5.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                                sqlCommand5.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult5 = sqlCommand5.ExecuteNonQuery();
                            }

                            recipient = l.OwnerId;
                            label = l.Title;
                        }
                        else if (entity == "Review" && status == "Removed")
                        {
                            List<Review> sqlResult6 = new List<Review>();
                            using (SqlCommand sqlCommand6 = new SqlCommand(@"SELECT [Id], [UserId], [ListingId], [Rating], [Text], [Removed], [Reason], [CreatedAt] FROM dbo.Reviews WHERE Id=@id AND Removed=0", connection, transaction))
                            {
                                sqlCommand6.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader6 = sqlCommand6.ExecuteReader())
                                {
                                    while (sqlReader6.Read())
                                    {
                                        Review databaseRow = new Review();
                                        if (sqlReader6["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader6["Id"]);
                                        }

                                        if (sqlReader6["UserId"] != DBNull.Value)
                                        {
                                            databaseRow.UserId = Convert.ToInt64(sqlReader6["UserId"]);
                                        }

                                        if (sqlReader6["ListingId"] != DBNull.Value)
                                        {
                                            databaseRow.ListingId = Convert.ToInt64(sqlReader6["ListingId"]);
                                        }

                                        if (sqlReader6["Rating"] != DBNull.Value)
                                        {
                                            databaseRow.Rating = Convert.ToInt32(sqlReader6["Rating"]);
                                        }

                                        if (sqlReader6["Text"] != DBNull.Value)
                                        {
                                            databaseRow.Text = Convert.ToString(sqlReader6["Text"])!;
                                        }

                                        if (sqlReader6["Removed"] != DBNull.Value)
                                        {
                                            databaseRow.Removed = Convert.ToBoolean(sqlReader6["Removed"]);
                                        }

                                        if (sqlReader6["Reason"] != DBNull.Value)
                                        {
                                            databaseRow.Reason = Convert.ToString(sqlReader6["Reason"])!;
                                        }

                                        if (sqlReader6["CreatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.CreatedAt = Convert.ToDateTime(sqlReader6["CreatedAt"]);
                                        }

                                        sqlResult6.Add(databaseRow);
                                    }
                                }
                            }

                            var review = (sqlResult6).FirstOrDefault() ?? throw new RuleException("Review not found.");
                            int sqlResult7;
                            using (SqlCommand sqlCommand7 = new SqlCommand("UPDATE dbo.Reviews SET Removed=1,Reason=@reason WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand7.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                                sqlCommand7.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult7 = sqlCommand7.ExecuteNonQuery();
                            }

                            recipient = review.UserId;
                            label = "Your review";
                        }
                        else
                        {
                            throw new RuleException("Unknown moderation action.");
                        }

                        int sqlResult8;
                        using (SqlCommand sqlCommand8 = new SqlCommand(@"INSERT dbo.ModerationHistory(ActorId,Entity,EntityId,Action,Reason)
                VALUES(@actor,@entity,@id,@status,@reason)", connection, transaction))
                        {
                            sqlCommand8.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@entity", ((object? )entity ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                            sqlResult8 = sqlCommand8.ExecuteNonQuery();
                        }

                        WorkflowSql_Notify(connection, transaction, recipient, label + " is " + (status ?? "").ToLowerInvariant() + ". " + reason, "/Workspace/Notifications");
                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void WorkflowSql_DeleteListing(long actor, string role, long id, string reason)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [OwnerId], [Kind], [Title], [Description], [Type], [City], [State], [Address], [Price], [Deposit], [Status], [Available], [Reason], [Details], [CreatedAt], [UpdatedAt] FROM dbo.Listings WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND OwnerId=@actor", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Listing databaseRow = new Listing();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    if (sqlReader1["Kind"] != DBNull.Value)
                                    {
                                        databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["Description"] != DBNull.Value)
                                    {
                                        databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                                    }

                                    if (sqlReader1["Type"] != DBNull.Value)
                                    {
                                        databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                                    }

                                    if (sqlReader1["City"] != DBNull.Value)
                                    {
                                        databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                                    }

                                    if (sqlReader1["State"] != DBNull.Value)
                                    {
                                        databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                                    }

                                    if (sqlReader1["Address"] != DBNull.Value)
                                    {
                                        databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                                    }

                                    if (sqlReader1["Price"] != DBNull.Value)
                                    {
                                        databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Available"] != DBNull.Value)
                                    {
                                        databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                                    }

                                    if (sqlReader1["Reason"] != DBNull.Value)
                                    {
                                        databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                                    }

                                    if (sqlReader1["Details"] != DBNull.Value)
                                    {
                                        databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["UpdatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                                    }

                                    databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var row = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Listing not found.");
                        if (row.Status == "Reserved" || row.Status == "Sold")
                        {
                            throw new RuleException("Ordered listings cannot be deleted.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"UPDATE dbo.Listings
                SET Status='Removed',Available=0,Reason=@reason
                WHERE Id=@id;INSERT dbo.ModerationHistory(ActorId,Entity,EntityId,Action,Reason)
                VALUES(@actor,'Listing',@id,'Removed',@reason)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void WorkflowSql_Notify(SqlConnection c, SqlTransaction tx, long account, string message, string url, bool email = true)
        {
            int sqlResult1;
            using (SqlCommand sqlCommand1 = new SqlCommand("INSERT dbo.Notifications(AccountId,Text,Url) VALUES(@account,@message,@url)", c, tx))
            {
                sqlCommand1.Parameters.AddWithValue("@account", ((object? )account ?? DBNull.Value));
                sqlCommand1.Parameters.AddWithValue("@message", ((object? )message[..Math.Min(500, message.Length)] ?? DBNull.Value));
                sqlCommand1.Parameters.AddWithValue("@url", ((object? )url ?? DBNull.Value));
                sqlResult1 = sqlCommand1.ExecuteNonQuery();
            }

            if (email)
            {
                int sqlResult2;
                using (SqlCommand sqlCommand2 = new SqlCommand(@"INSERT dbo.EmailOutbox(Recipient,Subject,Body) SELECT Email,'SettleMate update',@message
                FROM dbo.Accounts
                WHERE Id=@account", c, tx))
                {
                    sqlCommand2.Parameters.AddWithValue("@message", ((object? )message ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@account", ((object? )account ?? DBNull.Value));
                    sqlResult2 = sqlCommand2.ExecuteNonQuery();
                }
            }
        }

        private Listing ListingLogic_Accessible(long id, ClaimsPrincipal user)
        {
            var row = ListingSql_Get(id);
            if (row == null)
            {
                throw new RuleException("Listing not found.");
            }

            if (CurrentAccount.Role(user) == "Admin" || row.OwnerId == CurrentAccount.Id(user))
            {
                return row;
            }

            if (row.Status != "Approved" || !(row.OwnerStatus == "Active" || row.OwnerStatus == "Approved"))
            {
                throw new RuleException("Listing not found.");
            }

            return row;
        }

        private void ListingSql_ToggleWishlist(long user, long id)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        int sqlResult1;
                        using (SqlCommand sqlCommand1 = new SqlCommand("DELETE dbo.Wishlists WHERE AccountId=@user AND ListingId=@id", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlResult1 = sqlCommand1.ExecuteNonQuery();
                        }

                        int n = sqlResult1;
                        if (n == 0)
                        {
                            int sqlResult2;
                            using (SqlCommand sqlCommand2 = new SqlCommand("INSERT dbo.Wishlists VALUES(@user,@id)", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult2 = sqlCommand2.ExecuteNonQuery();
                            }
                        }

                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private void ListingSql_Availability(long owner, long id)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("UPDATE dbo.Listings SET Available=1-Available WHERE Id=@id AND OwnerId=@owner AND Status='Approved'", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@owner", ((object? )owner ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }

            if (sqlResult1 != 1)
            {
                throw new RuleException("Only approved listings can change availability.");
            }
        }

        private Listing? ListingSql_Get(long id)
        {
            List<Listing> sqlResult1 = new List<Listing>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(ListingSql_select + " WHERE l.Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Listing databaseRow = new Listing();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["OwnerId"] != DBNull.Value)
                            {
                                databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                            }

                            if (sqlReader1["Kind"] != DBNull.Value)
                            {
                                databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                            }

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["Description"] != DBNull.Value)
                            {
                                databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                            }

                            if (sqlReader1["Type"] != DBNull.Value)
                            {
                                databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                            }

                            if (sqlReader1["City"] != DBNull.Value)
                            {
                                databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                            }

                            if (sqlReader1["State"] != DBNull.Value)
                            {
                                databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                            }

                            if (sqlReader1["Address"] != DBNull.Value)
                            {
                                databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                            }

                            if (sqlReader1["Price"] != DBNull.Value)
                            {
                                databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                            }

                            if (sqlReader1["Deposit"] != DBNull.Value)
                            {
                                databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Available"] != DBNull.Value)
                            {
                                databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                            }

                            if (sqlReader1["Reason"] != DBNull.Value)
                            {
                                databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                            }

                            if (sqlReader1["Details"] != DBNull.Value)
                            {
                                databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["UpdatedAt"] != DBNull.Value)
                            {
                                databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                            }

                            if (sqlReader1["OwnerName"] != DBNull.Value)
                            {
                                databaseRow.OwnerName = Convert.ToString(sqlReader1["OwnerName"])!;
                            }

                            if (sqlReader1["OwnerEmail"] != DBNull.Value)
                            {
                                databaseRow.OwnerEmail = Convert.ToString(sqlReader1["OwnerEmail"])!;
                            }

                            if (sqlReader1["OwnerPhone"] != DBNull.Value)
                            {
                                databaseRow.OwnerPhone = Convert.ToString(sqlReader1["OwnerPhone"])!;
                            }

                            if (sqlReader1["OwnerStatus"] != DBNull.Value)
                            {
                                databaseRow.OwnerStatus = Convert.ToString(sqlReader1["OwnerStatus"])!;
                            }

                            if (sqlReader1["Image"] != DBNull.Value)
                            {
                                databaseRow.Image = Convert.ToString(sqlReader1["Image"])!;
                            }

                            if (sqlReader1["Rating"] != DBNull.Value)
                            {
                                databaseRow.Rating = Convert.ToDecimal(sqlReader1["Rating"]);
                            }

                            databaseRow.Data = ReadListingDetails(databaseRow.Details);
                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return (sqlResult1).FirstOrDefault();
        }

        const string ListingSql_select = @"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt],a.FullName AS OwnerName,a.Email AS OwnerEmail,a.Phone AS OwnerPhone,a.Status AS OwnerStatus,(SELECT TOP 1 Name
                FROM dbo.Files
                WHERE ListingId=l.Id
                AND Private=0
                ORDER BY Id) AS Image,(SELECT AVG(CAST(Rating AS decimal(4,2)))
                FROM dbo.Reviews
                WHERE ListingId=l.Id
                AND Removed=0) AS Rating
                FROM dbo.Listings l
                JOIN dbo.Accounts a ON a.Id=l.OwnerId ";
        private Listing ReadListingDetails(string json)
        {
            Listing listing = new Listing();
            if (string.IsNullOrWhiteSpace(json))
            {
                return listing;
            }

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement data = document.RootElement;
                JsonElement value;
                if (data.TryGetProperty("Id", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Id = value.GetInt64();
                }

                if (data.TryGetProperty("Kind", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Kind = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Title", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Title = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Description", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Description = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Type", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Type = value.GetString() ?? "";
                }

                if (data.TryGetProperty("City", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.City = value.GetString() ?? "";
                }

                if (data.TryGetProperty("State", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.State = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Address", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Address = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Pincode", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Pincode = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Price", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Price = value.GetDecimal();
                }

                if (data.TryGetProperty("Deposit", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Deposit = value.GetDecimal();
                }

                if (data.TryGetProperty("Maintenance", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Maintenance = value.GetDecimal();
                }

                if (data.TryGetProperty("Basis", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Basis = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Gender", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Gender = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Furnishing", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Furnishing = value.GetString() ?? "";
                }

                if (data.TryGetProperty("RoomType", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.RoomType = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Bhk", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Bhk = value.GetInt32();
                }

                if (data.TryGetProperty("Bedrooms", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Bedrooms = value.GetInt32();
                }

                if (data.TryGetProperty("Floor", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Floor = value.GetInt32();
                }

                if (data.TryGetProperty("Beds", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Beds = value.GetInt32();
                }

                if (data.TryGetProperty("AttachedBathroom", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AttachedBathroom = value.GetBoolean();
                }

                if (data.TryGetProperty("Food", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Food = value.GetBoolean();
                }

                if (data.TryGetProperty("Meals", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Meals = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Curfew", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Curfew = value.GetString() ?? "";
                }

                if (data.TryGetProperty("AvailableFrom", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AvailableFrom = value.GetDateTime();
                }

                if (data.TryGetProperty("MinimumStay", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.MinimumStay = value.GetInt32();
                }

                if (data.TryGetProperty("NoticePeriod", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.NoticePeriod = value.GetInt32();
                }

                if (data.TryGetProperty("Electricity", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Electricity = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Water", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Water = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Landmark", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Landmark = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Latitude", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Latitude = value.GetDecimal();
                }

                if (data.TryGetProperty("Longitude", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Longitude = value.GetDecimal();
                }

                if (data.TryGetProperty("Rules", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Rules = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Visitors", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Visitors = value.GetBoolean();
                }

                if (data.TryGetProperty("Smoking", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Smoking = value.GetBoolean();
                }

                if (data.TryGetProperty("Pets", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Pets = value.GetBoolean();
                }

                if (data.TryGetProperty("AgreementType", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AgreementType = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Condition", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Condition = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Category", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Category = value.GetString() ?? "";
                }

                if (data.TryGetProperty("OwnerRelation", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.OwnerRelation = value.GetString() ?? "";
                }

                if (data.TryGetProperty("DocumentType", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.DocumentType = value.GetString() ?? "";
                }

                if (data.TryGetProperty("DocumentReference", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.DocumentReference = value.GetString() ?? "";
                }

                if (data.TryGetProperty("AdminNote", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AdminNote = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Declaration1", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration1 = value.GetBoolean();
                }

                if (data.TryGetProperty("Declaration2", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration2 = value.GetBoolean();
                }

                if (data.TryGetProperty("Declaration3", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration3 = value.GetBoolean();
                }

                if (data.TryGetProperty("Declaration4", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration4 = value.GetBoolean();
                }

                if (data.TryGetProperty("Amenities", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    List<string> values = new List<string>();
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        values.Add(item.GetString() ?? "");
                    }

                    listing.Amenities = values.ToArray();
                }
            }

            return listing;
        }
    }
}
