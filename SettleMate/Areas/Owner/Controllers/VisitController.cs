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

namespace SettleMate.Web.Areas.Owner.Controllers
{
    [Area("Owner")]
    [Authorize(Roles = "Owner")]
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

        private static string WorkflowLogic_Text(string value, int min, int max)
        {
            value = (value ?? "").Trim();
            if (value.Length < min || value.Length > max)
            {
                throw new RuleException($"Enter between {min} and {max} characters.");
            }

            return value;
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
    }
}
