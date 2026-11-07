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
    public class ReviewController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public ReviewController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(int page = 1, string search = "", string status = "")
        {
            return View(WorkspaceQueryLogic_Records(User, "Reviews", page, search, status));
        }

        [HttpPost]
        public IActionResult Create(ReviewInput model)
        {
            ValidateForm();
            WorkflowLogic_CreateReview(User, model.Id, message: model.Message, number: model.Number);
            return Success(model);
        }

        [HttpPost]
        public IActionResult Report(ReasonAction model)
        {
            ValidateForm();
            WorkflowLogic_ReportReview(User, model.Id, message: model.Reason);
            return Success(model);
        }

        private List<Review> WorkspaceQueryLogic_Records(ClaimsPrincipal user, string section, int page, string search, string status, bool received = false)
        {
            List<Review> rows = WorkflowSql_Reviews(CurrentAccount.Id(user), CurrentAccount.Role(user));
            List<Review> matches = new List<Review>();
            foreach (Review row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.UserId, row.ListingId, row.Rating, row.Text, row.Removed, row.Reason, row.CreatedAt, row.FullName, row.Title, row.OwnerId, row.Reports });
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
            ViewData["Title"] = "Reviews & ratings";
            ViewData["Section"] = section;
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
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

        private List<Review> WorkflowSql_Reviews(long actor, string role)
        {
            List<Review> sqlResult1 = new List<Review>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT r.[Id], r.[UserId], r.[ListingId], r.[Rating], r.[Text], r.[Removed], r.[Reason], r.[CreatedAt],l.Title,l.OwnerId,a.FullName,(SELECT COUNT(*)
                FROM dbo.ReviewReports
                WHERE ReviewId=r.Id) AS Reports
                FROM dbo.Reviews r
                JOIN dbo.Listings l ON l.Id=r.ListingId
                JOIN dbo.Accounts a ON a.Id=r.UserId
                WHERE @role='Admin'
                OR l.OwnerId=@actor
                OR r.UserId=@actor
                ORDER BY r.Id DESC", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Review databaseRow = new Review();
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

                            if (sqlReader1["Rating"] != DBNull.Value)
                            {
                                databaseRow.Rating = Convert.ToInt32(sqlReader1["Rating"]);
                            }

                            if (sqlReader1["Text"] != DBNull.Value)
                            {
                                databaseRow.Text = Convert.ToString(sqlReader1["Text"])!;
                            }

                            if (sqlReader1["Removed"] != DBNull.Value)
                            {
                                databaseRow.Removed = Convert.ToBoolean(sqlReader1["Removed"]);
                            }

                            if (sqlReader1["Reason"] != DBNull.Value)
                            {
                                databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
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

                            if (sqlReader1["FullName"] != DBNull.Value)
                            {
                                databaseRow.FullName = Convert.ToString(sqlReader1["FullName"])!;
                            }

                            if (sqlReader1["Reports"] != DBNull.Value)
                            {
                                databaseRow.Reports = Convert.ToInt64(sqlReader1["Reports"]);
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
