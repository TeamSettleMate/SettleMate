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
    public class MessageController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public MessageController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(int page = 1, string search = "", string status = "")
        {
            return View(WorkspaceQueryLogic_Records(User, "Messages", page, search, status));
        }

        [HttpGet]
        public IActionResult Thread(long id)
        {
            var model = WorkspaceQueryLogic_Thread(User, id);
            if (model == null)
            {
                return NotFound();
            }
            else
            {
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult Send(MessageInput model)
        {
            ValidateForm();
            WorkflowLogic_SendMessage(User, model.Id, message: model.Message);
            return Success(model);
        }

        [HttpPost]
        public IActionResult Start([FromForm] long listing)
        {
            if (!ModelState.IsValid || listing <= 0)
            {
                return BadRequest();
            }

            long id = WorkflowLogic_Conversation(User, listing);
            return RedirectToAction(nameof(Thread), new { id });
        }

        private List<Conversation> WorkspaceQueryLogic_Records(ClaimsPrincipal user, string section, int page, string search, string status, bool received = false)
        {
            List<Conversation> rows = WorkflowSql_Messages(CurrentAccount.Id(user));
            List<Conversation> matches = new List<Conversation>();
            foreach (Conversation row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.ListingId, row.UserId, row.OwnerId, row.CreatedAt, row.Title, row.UserName, row.OwnerName });
                if (!string.IsNullOrWhiteSpace(search) && !text.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(status) && true)
                {
                    continue;
                }

                matches.Add(row);
            }

            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(matches.Count / 10.0)));
            ViewData["Title"] = "Messages";
            ViewData["Section"] = section;
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
        }

        private Conversation? WorkspaceQueryLogic_Thread(ClaimsPrincipal user, long id)
        {
            foreach (Conversation conversation in WorkflowSql_Messages(CurrentAccount.Id(user)))
            {
                if (conversation.Id == id)
                {
                    ViewData["Messages"] = WorkflowSql_Thread(CurrentAccount.Id(user), id);
                    return conversation;
                }
            }

            return null;
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

        private long WorkflowLogic_Conversation(ClaimsPrincipal user, long listing)
        {
            WorkflowLogic_Require(user, "User");
            return WorkflowSql_StartConversation(CurrentAccount.Id(user), listing);
        }

        private List<Conversation> WorkflowSql_Messages(long actor)
        {
            List<Conversation> sqlResult1 = new List<Conversation>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT c.[Id], c.[ListingId], c.[UserId], c.[OwnerId], c.[CreatedAt],l.Title,a.FullName AS UserName,b.FullName AS OwnerName
                FROM dbo.Conversations c
                JOIN dbo.Listings l ON l.Id=c.ListingId
                JOIN dbo.Accounts a ON a.Id=c.UserId
                JOIN dbo.Accounts b ON b.Id=c.OwnerId
                WHERE c.UserId=@actor
                OR c.OwnerId=@actor
                ORDER BY c.Id DESC", sqlConnection1))
                {
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

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["UserName"] != DBNull.Value)
                            {
                                databaseRow.UserName = Convert.ToString(sqlReader1["UserName"])!;
                            }

                            if (sqlReader1["OwnerName"] != DBNull.Value)
                            {
                                databaseRow.OwnerName = Convert.ToString(sqlReader1["OwnerName"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
        }

        private List<Message> WorkflowSql_Thread(long actor, long id)
        {
            List<Message> sqlResult1 = new List<Message>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT m.[Id], m.[ConversationId], m.[SenderId], m.[Text], m.[CreatedAt],a.FullName
                FROM dbo.Messages m
                JOIN dbo.Conversations c ON c.Id=m.ConversationId
                JOIN dbo.Accounts a ON a.Id=m.SenderId
                WHERE c.Id=@id
                AND (c.UserId=@actor
                OR c.OwnerId=@actor)
                ORDER BY m.Id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Message databaseRow = new Message();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["ConversationId"] != DBNull.Value)
                            {
                                databaseRow.ConversationId = Convert.ToInt64(sqlReader1["ConversationId"]);
                            }

                            if (sqlReader1["SenderId"] != DBNull.Value)
                            {
                                databaseRow.SenderId = Convert.ToInt64(sqlReader1["SenderId"]);
                            }

                            if (sqlReader1["Text"] != DBNull.Value)
                            {
                                databaseRow.Text = Convert.ToString(sqlReader1["Text"])!;
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["FullName"] != DBNull.Value)
                            {
                                databaseRow.FullName = Convert.ToString(sqlReader1["FullName"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
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
