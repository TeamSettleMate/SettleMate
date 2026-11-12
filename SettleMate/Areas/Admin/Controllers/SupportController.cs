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

namespace SettleMate.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SupportController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public SupportController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(int page = 1, string search = "", string status = "")
        {
            return View(WorkspaceQueryLogic_Records(User, "Support", page, search, status));
        }

        [HttpGet]
        public IActionResult Ticket(long id)
        {
            var model = WorkspaceQueryLogic_Ticket(User, id);
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
        public IActionResult Reply(SupportReply model)
        {
            ValidateForm();
            WorkflowLogic_ReplyToTicket(User, model.Id, message: model.Message, email: model.Email);
            return Success(model);
        }

        private List<Ticket> WorkspaceQueryLogic_Records(ClaimsPrincipal user, string section, int page, string search, string status, bool received = false)
        {
            List<Ticket> rows = WorkflowSql_Tickets(CurrentAccount.Id(user), CurrentAccount.Role(user));
            List<Ticket> matches = new List<Ticket>();
            foreach (Ticket row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.AccountId, row.FullName, row.Email, row.Subject, row.Message, row.Status, row.CreatedAt });
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
            ViewData["Title"] = "Help & support";
            ViewData["Section"] = section;
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
        }

        private Ticket? WorkspaceQueryLogic_Ticket(ClaimsPrincipal user, long id)
        {
            Ticket? ticket = WorkflowSql_Ticket(CurrentAccount.Id(user), CurrentAccount.Role(user), id);
            if (ticket != null)
            {
                ViewData["Replies"] = WorkflowSql_Replies(id);
            }

            return ticket;
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

        private List<Ticket> WorkflowSql_Tickets(long actor, string role)
        {
            List<Ticket> sqlResult1 = new List<Ticket>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [AccountId], [FullName], [Email], [Subject], [Message], [Status], [CreatedAt] FROM dbo.Tickets WHERE @role='Admin' OR AccountId=@actor ORDER BY Id DESC", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
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
            }

            return sqlResult1;
        }

        private Ticket? WorkflowSql_Ticket(long actor, string role, long id)
        {
            List<Ticket> sqlResult1 = new List<Ticket>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [AccountId], [FullName], [Email], [Subject], [Message], [Status], [CreatedAt] FROM dbo.Tickets WHERE Id=@id AND (@role='Admin' OR AccountId=@actor)", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
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
            }

            return (sqlResult1).FirstOrDefault();
        }

        private List<TicketReply> WorkflowSql_Replies(long id)
        {
            List<TicketReply> sqlResult1 = new List<TicketReply>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT r.[Id], r.[TicketId], r.[AdminId], r.[Message], r.[CreatedAt],a.FullName
                FROM dbo.TicketReplies r
                JOIN dbo.Accounts a ON a.Id=r.AdminId
                WHERE TicketId=@id
                ORDER BY r.Id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            TicketReply databaseRow = new TicketReply();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["TicketId"] != DBNull.Value)
                            {
                                databaseRow.TicketId = Convert.ToInt64(sqlReader1["TicketId"]);
                            }

                            if (sqlReader1["AdminId"] != DBNull.Value)
                            {
                                databaseRow.AdminId = Convert.ToInt64(sqlReader1["AdminId"]);
                            }

                            if (sqlReader1["Message"] != DBNull.Value)
                            {
                                databaseRow.Message = Convert.ToString(sqlReader1["Message"])!;
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
