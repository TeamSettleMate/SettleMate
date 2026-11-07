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
    public class ProfileController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public ProfileController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(ProfileQueryLogic_Get(CurrentAccount.Id(User)));
        }

        [HttpPost]
        public IActionResult Index(AccountProfile model)
        {
            ProfileQueryLogic_Get(CurrentAccount.Id(User));
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                AuthLogic_SaveProfile(CurrentAccount.Id(User), model);
                TempData["Success"] = "Profile saved.";
                return RedirectToAction(nameof(Index));
            }
            catch (RuleException exception)
            {
                ModelState.AddModelError("", exception.Message);
                return View(model);
            }
        }

        private AccountProfile ProfileQueryLogic_Get(long id)
        {
            Account? account = AccountSql_Get(id);
            if (account == null)
            {
                throw new RuleException("Account not found.");
            }

            ViewData["Account"] = account;
            ViewData["Documents"] = FileLogic_ForOwner(id);
            return new AccountProfile
            {
                FullName = account.FullName,
                Phone = account.Phone,
                Address = account.Address,
                City = account.City,
                State = account.State,
                Pincode = account.Pincode,
                DateOfBirth = account.DateOfBirth,
                Gender = account.Gender,
                Avatar = account.Avatar
            };
        }

        private void AuthLogic_SaveProfile(long id, AccountProfile model)
        {
            if (model.Address == null)
            {
                model.Address = "";
            }

            if (!(model.Gender == "Male" || model.Gender == "Female" || model.Gender == "Other"))
            {
                throw new RuleException("Choose a valid gender.");
            }

            if (model.DateOfBirth >= DateTime.Today)
            {
                throw new RuleException("Date of birth must be in the past.");
            }

            AccountSql_UpdateProfile(id, model);
        }

        private Account? AccountSql_Get(long id)
        {
            List<Account> sqlResult1 = new List<Account>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [FullName], [Email], [Phone], [PasswordHash], [Role], [Status], [Reason], [EmailVerified], [SecurityStamp], [DateOfBirth], [Gender], [Address], [City], [State], [Pincode], [Avatar], [DocumentType], [CreatedAt], [NormalizedEmail] FROM dbo.Accounts WHERE Id=@id", sqlConnection1))
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
            }

            return (sqlResult1).FirstOrDefault();
        }

        private List<StoredFile> FileLogic_ForOwner(long id)
        {
            return FileSql_ForOwner(id);
        }

        private void AccountSql_UpdateProfile(long id, AccountProfile m)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"UPDATE dbo.Accounts
                SET FullName=@FullName,Phone=@Phone,Address=@Address,City=@City,State=@State,Pincode=@Pincode,DateOfBirth=@DateOfBirth,Gender=@Gender,Avatar=@Avatar
                WHERE Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@FullName", ((object? )m.FullName ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Phone", ((object? )m.Phone ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Address", ((object? )m.Address ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@City", ((object? )m.City ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@State", ((object? )m.State ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Pincode", ((object? )m.Pincode ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@DateOfBirth", ((object? )m.DateOfBirth ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Gender", ((object? )m.Gender ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Avatar", ((object? )m.Avatar ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }
        }

        private List<StoredFile> FileSql_ForOwner(long id)
        {
            List<StoredFile> sqlResult1 = new List<StoredFile>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [OwnerId], [ListingId], [Name], [OriginalName], [ContentType], [Private], [Purpose], [CreatedAt] FROM dbo.Files WHERE OwnerId=@id AND ListingId IS NULL AND Private=1 ORDER BY Id DESC", sqlConnection1))
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

            return sqlResult1;
        }
    }
}
