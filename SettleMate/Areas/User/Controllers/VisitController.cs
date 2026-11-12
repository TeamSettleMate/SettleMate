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

namespace SettleMate.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize(Roles = "User")]
    public class VisitController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public VisitController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(int page = 1, string search = "", string status = "")
        {
            return View(WorkspaceQueryLogic_Records(User, "Visits", page, search, status));
        }

        [HttpPost]
        public IActionResult RequestVisit(VisitInput model)
        {
            ValidateForm();
            WorkflowLogic_RequestVisit(User, model.Id, message: model.Message, at: model.At);
            return Success(model);
        }

        [HttpPost]
        public IActionResult UpdateStatus(VisitStatus model)
        {
            ValidateForm();
            WorkflowLogic_UpdateVisit(User, model.Id, value: model.Value, message: string.IsNullOrWhiteSpace(model.Reason) ? model.Message : model.Reason, at: model.At);
            return Success(model);
        }

        private List<VisitRequest> WorkspaceQueryLogic_Records(ClaimsPrincipal user, string section, int page, string search, string status, bool received = false)
        {
            List<VisitRequest> rows = WorkflowSql_Visits(CurrentAccount.Id(user), CurrentAccount.Role(user));
            List<VisitRequest> matches = new List<VisitRequest>();
            foreach (VisitRequest row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.UserId, row.ListingId, row.VisitAt, row.Message, row.Response, row.Status, row.CreatedAt, row.OwnerId, row.Title, row.Kind, row.FullName, row.Email, row.Phone });
                if (!string.IsNullOrWhiteSpace(search) && !text.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(status) && row.Status != status)
                {
                    continue;
                }

                matches.Add(row);
            }

            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(matches.Count / 10.0)));
            ViewData["Title"] = "Visit requests";
            ViewData["Section"] = section;
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
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

        private List<VisitRequest> WorkflowSql_Visits(long user, string role)
        {
            List<VisitRequest> sqlResult1 = new List<VisitRequest>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT v.[Id], v.[UserId], v.[ListingId], v.[VisitAt], v.[Message], v.[Response], v.[Status], v.[CreatedAt],l.Title,l.OwnerId,l.Kind,a.FullName,a.Email,a.Phone
                FROM dbo.VisitRequests v
                JOIN dbo.Listings l ON l.Id=v.ListingId
                JOIN dbo.Accounts a ON a.Id=v.UserId
                WHERE @role='Admin'
                OR v.UserId=@user
                OR l.OwnerId=@user
                ORDER BY v.Id DESC", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
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

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["OwnerId"] != DBNull.Value)
                            {
                                databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                            }

                            if (sqlReader1["Kind"] != DBNull.Value)
                            {
                                databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
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

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
        }

        private static void WorkflowLogic_Require(ClaimsPrincipal user, params string[] roles)
        {
            if (!roles.Contains(CurrentAccount.Role(user)))
            {
                throw new RuleException("You do not have access to this action.");
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

        private void ValidateForm()
        {
            if (ModelState.IsValid)
            {
                return;
            }

            List<string> messages = new List<string>();
            foreach (ModelStateEntry? field in ModelState.Values)
            {
                foreach (ModelError? error in field.Errors)
                {
                    if (string.IsNullOrWhiteSpace(error.ErrorMessage))
                    {
                        messages.Add("Check the submitted values.");
                    }
                    else
                    {
                        messages.Add(error.ErrorMessage);
                    }
                }
            }

            throw new RuleException(string.Join(" ", messages));
        }

        private IActionResult Success(RecordAction model)
        {
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Updated successfully." });
            }

            TempData["Success"] = "Updated successfully.";
            string destination = "/Workspace";
            if (Url.IsLocalUrl(model.ReturnUrl))
            {
                destination = model.ReturnUrl;
            }

            destination = WorkspaceRoutes.Resolve(CurrentAccount.Role(User), destination);
            return LocalRedirect(destination);
        }

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
