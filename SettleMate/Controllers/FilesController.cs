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
    public class FilesController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public FilesController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        public IActionResult Download(long id)
        {
            var file = FileLogic_Get(id);
            if (file == null || file.Private && CurrentAccount.Role(User) != "Admin" && file.OwnerId != CurrentAccount.Id(User))
            {
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers.CacheControl = "no-store";
            string? path = FileLogic_PathFor(file);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, file.ContentType, file.OriginalName);
        }

        private StoredFile? FileLogic_Get(long id)
        {
            return FileSql_Get(id);
        }

        private string FileLogic_PathFor(StoredFile row)
        {
            return Path.Combine(row.Private ? Path.Combine(env.ContentRootPath, "App_Data", "private") : Path.Combine(env.WebRootPath, "uploads"), Path.GetFileName(row.Name));
        }

        private StoredFile? FileSql_Get(long id)
        {
            List<StoredFile> sqlResult1 = new List<StoredFile>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [OwnerId], [ListingId], [Name], [OriginalName], [ContentType], [Private], [Purpose], [CreatedAt] FROM dbo.Files WHERE Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            StoredFile databaseRow = new StoredFile();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["OwnerId"] != DBNull.Value)
                            {
                                databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                            }

                            if (sqlReader1["ListingId"] != DBNull.Value)
                            {
                                databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                            }

                            if (sqlReader1["Name"] != DBNull.Value)
                            {
                                databaseRow.Name = Convert.ToString(sqlReader1["Name"])!;
                            }

                            if (sqlReader1["OriginalName"] != DBNull.Value)
                            {
                                databaseRow.OriginalName = Convert.ToString(sqlReader1["OriginalName"])!;
                            }

                            if (sqlReader1["ContentType"] != DBNull.Value)
                            {
                                databaseRow.ContentType = Convert.ToString(sqlReader1["ContentType"])!;
                            }

                            if (sqlReader1["Private"] != DBNull.Value)
                            {
                                databaseRow.Private = Convert.ToBoolean(sqlReader1["Private"]);
                            }

                            if (sqlReader1["Purpose"] != DBNull.Value)
                            {
                                databaseRow.Purpose = Convert.ToString(sqlReader1["Purpose"])!;
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
    }
}
