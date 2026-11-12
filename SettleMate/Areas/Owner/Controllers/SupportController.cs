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
    }
}
