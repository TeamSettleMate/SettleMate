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
    public class NotificationController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public NotificationController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(int page = 1, string search = "", string status = "")
        {
            return View(WorkspaceQueryLogic_Records(User, "Notifications", page, search, status));
        }

        [HttpPost]
        public IActionResult MarkRead(RecordAction model)
        {
            ValidateForm();
            WorkflowLogic_MarkNotificationRead(User, model.Id);
            return Success(model);
        }

        private List<Notification> WorkspaceQueryLogic_Records(ClaimsPrincipal user, string section, int page, string search, string status, bool received = false)
        {
            List<Notification> rows = WorkflowSql_Notifications(CurrentAccount.Id(user));
            List<Notification> matches = new List<Notification>();
            foreach (Notification row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.AccountId, row.Text, row.Url, row.IsRead, row.CreatedAt });
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
            ViewData["Title"] = "Notifications";
            ViewData["Section"] = section;
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
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

        private List<Notification> WorkflowSql_Notifications(long actor)
        {
            List<Notification> sqlResult1 = new List<Notification>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [AccountId], [Text], [Url], [IsRead], [CreatedAt] FROM dbo.Notifications WHERE AccountId=@actor ORDER BY Id DESC", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Notification databaseRow = new Notification();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["AccountId"] != DBNull.Value)
                            {
                                databaseRow.AccountId = Convert.ToInt64(sqlReader1["AccountId"]);
                            }

                            if (sqlReader1["Text"] != DBNull.Value)
                            {
                                databaseRow.Text = Convert.ToString(sqlReader1["Text"])!;
                            }

                            if (sqlReader1["Url"] != DBNull.Value)
                            {
                                databaseRow.Url = Convert.ToString(sqlReader1["Url"])!;
                            }

                            if (sqlReader1["IsRead"] != DBNull.Value)
                            {
                                databaseRow.IsRead = Convert.ToBoolean(sqlReader1["IsRead"]);
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
