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
    }
}
