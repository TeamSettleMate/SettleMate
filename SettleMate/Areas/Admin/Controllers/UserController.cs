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
    public class UserController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public UserController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(string search = "", string status = "", int page = 1)
        {
            return View(WorkspaceQueryLogic_Accounts("User", search, status, page));
        }

        [HttpGet]
        public IActionResult Details(long id)
        {
            var model = WorkspaceQueryLogic_Account(id);
            if (model == null || model.Role != "User")
            {
                return NotFound();
            }
            else
            {
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult Moderate(ModerationAction model)
        {
            ValidateForm();
            WorkflowLogic_ModerateFeature(User, "User", model.Id, model.Value, model.Reason);
            return Success(model);
        }

        private List<Account> WorkspaceQueryLogic_Accounts(string role, string search, string status, int page)
        {
            ViewData["Kind"] = role;
            List<Account> rows = WorkflowSql_Accounts(role);
            List<Account> matches = new List<Account>();
            foreach (Account row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.FullName, row.Email, row.Phone, row.PasswordHash, row.Role, row.Status, row.Reason, row.EmailVerified, row.SecurityStamp, row.DateOfBirth, row.Gender, row.Address, row.City, row.State, row.Pincode, row.Avatar, row.DocumentType, row.CreatedAt, row.NormalizedEmail, row.Listings });
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
            ViewData["Title"] = role == "Owner" ? "Property owners" : "Users & sellers";
            ViewData["Section"] = "Accounts";
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
        }

        private Account? WorkspaceQueryLogic_Account(long id)
        {
            Account? account = AccountSql_Get(id);
            if (account == null || account.Role == "Admin")
            {
                return null;
            }

            ViewData["Listings"] = ListingSql_Browse(new ListingSearch { Kind = "" }, id, true);
            ViewData["Documents"] = FileLogic_ForOwner(id);
            ViewData["Orders"] = WorkflowSql_Orders(id, "User", true);
            ViewData["Title"] = account.FullName;
            return account;
        }

        private void WorkflowLogic_ModerateFeature(ClaimsPrincipal user, string feature, long id, string value, string reason)
        {
            WorkflowLogic_Require(user, "Admin");
            string? entity;
            if (feature == "User" || feature == "Owner")
            {
                entity = "Account";
            }
            else
            {
                if (feature == "Review")
                {
                    entity = "Review";
                }
                else
                {
                    entity = "Listing";
                }
            }

            if (!value.StartsWith(entity + ":", StringComparison.Ordinal))
            {
                throw new RuleException("Invalid moderation target.");
            }

            if (entity == "Account")
            {
                var account = AccountSql_Get(id);
                if (account == null || account.Role != feature)
                {
                    throw new RuleException("Account not found.");
                }
            }

            if (entity == "Listing")
            {
                var listing = ListingLogic_Accessible(id, user);
                string? kind = listing.Kind;
                if (feature == "Roommate" ? !(kind == "Room" || kind == "Finder") : kind != feature)
                {
                    throw new RuleException("Listing not found.");
                }
            }

            WorkflowLogic_Moderate(user, id, value, reason);
        }

        private List<Account> WorkflowSql_Accounts(string role)
        {
            List<Account> sqlResult1 = new List<Account>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT a.Id,a.FullName,a.Email,a.Phone,a.Role,a.Status,a.Reason,a.City,a.State,a.Gender,a.CreatedAt,(SELECT COUNT(*)
                FROM dbo.Listings
                WHERE OwnerId=a.Id) AS Listings
                FROM dbo.Accounts a
                WHERE @role=''
                OR a.Role=@role
                ORDER BY a.Id DESC", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
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

                            if (sqlReader1["City"] != DBNull.Value)
                            {
                                databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                            }

                            if (sqlReader1["State"] != DBNull.Value)
                            {
                                databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                            }

                            if (sqlReader1["Gender"] != DBNull.Value)
                            {
                                databaseRow.Gender = Convert.ToString(sqlReader1["Gender"])!;
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["Listings"] != DBNull.Value)
                            {
                                databaseRow.Listings = Convert.ToInt64(sqlReader1["Listings"]);
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
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

        private List<Listing> ListingSql_Browse(ListingSearch query, long? owner = null, bool admin = false, string status = "")
        {
            // One search model carries the filters instead of a long argument list.
            string kind = query.Kind;
            string search = query.Search;
            string city = query.City;
            string type = query.Type;
            string gender = query.Gender;
            string furnishing = query.Furnishing;
            bool food = query.Food;
            int bhk = query.Bhk;
            string amenity = query.Amenity;
            DateTime? moveIn = query.MoveIn;
            decimal? min = query.Min;
            decimal? max = query.Max;
            string sort = query.Sort;
            int page = query.Page;
            string category = query.Category;
            string condition = query.Condition;
            string roomType = query.RoomType;
            string? where = @"WHERE (@kind=''
                OR l.Kind=@kind)
                AND (@owner IS NULL
                OR l.OwnerId=@owner)
                AND ((@admin=1
                OR @owner IS NOT NULL)
                OR (l.Status='Approved'
                AND a.Status IN('Active','Approved')))
                AND (@status=''
                OR l.Status=@status)
                AND (@search=''
                OR l.Title LIKE @term
                OR l.City LIKE @term)
                AND (@city=''
                OR l.City LIKE @cityTerm)
                AND (@type=''
                OR l.Type=@type)
                AND (@gender=''
                OR JSON_VALUE(l.Details,'$.Gender')=@gender
                OR JSON_VALUE(l.Details,'$.Gender')='Any')
                AND (@furnishing=''
                OR JSON_VALUE(l.Details,'$.Furnishing')=@furnishing)
                AND (@food=0
                OR JSON_VALUE(l.Details,'$.Food')='true')
                AND (@bhk=0
                OR TRY_CONVERT(int,JSON_VALUE(l.Details,'$.Bhk'))=@bhk)
                AND (@amenity=''
                OR EXISTS(SELECT 1
                FROM OPENJSON(l.Details,'$.Amenities') j
                WHERE j.value=@amenity))
                AND (@moveIn IS NULL
                OR TRY_CONVERT(date,JSON_VALUE(l.Details,'$.AvailableFrom'))<=@moveIn)
                AND (@category=''
                OR JSON_VALUE(l.Details,'$.Category')=@category)
                AND (@condition=''
                OR JSON_VALUE(l.Details,'$.Condition')=@condition)
                AND (@roomType=''
                OR JSON_VALUE(l.Details,'$.RoomType')=@roomType)
                AND (@min IS NULL
                OR l.Price>=@min)
                AND (@max IS NULL
                OR l.Price<=@max)";
            SqlParameter[]? args = new SqlParameter[]
            {
                new SqlParameter("@kind", ((object? )kind ?? DBNull.Value)),
                new SqlParameter("@search", ((object? )search ?? DBNull.Value)),
                new SqlParameter("@term", ((object? )("%" + search + "%") ?? DBNull.Value)),
                new SqlParameter("@city", ((object? )city ?? DBNull.Value)),
                new SqlParameter("@cityTerm", ((object? )("%" + city + "%") ?? DBNull.Value)),
                new SqlParameter("@type", ((object? )type ?? DBNull.Value)),
                new SqlParameter("@gender", ((object? )gender ?? DBNull.Value)),
                new SqlParameter("@furnishing", ((object? )furnishing ?? DBNull.Value)),
                new SqlParameter("@food", ((object? )food ?? DBNull.Value)),
                new SqlParameter("@bhk", ((object? )bhk ?? DBNull.Value)),
                new SqlParameter("@amenity", ((object? )amenity ?? DBNull.Value)),
                new SqlParameter("@moveIn", ((object? )moveIn ?? DBNull.Value)),
                new SqlParameter("@min", ((object? )min ?? DBNull.Value)),
                new SqlParameter("@max", ((object? )max ?? DBNull.Value)),
                new SqlParameter("@owner", ((object? )owner ?? DBNull.Value)),
                new SqlParameter("@admin", ((object? )admin ?? DBNull.Value)),
                new SqlParameter("@status", ((object? )status ?? DBNull.Value)),
                new SqlParameter("@category", ((object? )category ?? DBNull.Value)),
                new SqlParameter("@condition", ((object? )condition ?? DBNull.Value)),
                new SqlParameter("@roomType", ((object? )roomType ?? DBNull.Value))
            };
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("SELECT COUNT(*) AS Total FROM dbo.Listings l JOIN dbo.Accounts a ON a.Id=l.OwnerId " + where, sqlConnection1))
                {
                    foreach (SqlParameter parameter in args)
                    {
                        sqlCommand1.Parameters.AddWithValue(parameter.ParameterName, parameter.Value);
                    }

                    sqlResult1 = Convert.ToInt32(sqlCommand1.ExecuteScalar());
                }
            }

            int count = sqlResult1;
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / 10d)));
            string? order;
            if (sort is "price")
            {
                order = "l.Price ASC,l.Id DESC";
            }
            else if (sort is "price-desc")
            {
                order = "l.Price DESC,l.Id DESC";
            }
            else if (sort is "rating")
            {
                order = "Rating DESC,l.Id DESC";
            }
            else
            {
                order = "l.Id DESC";
            }

            List<Listing> sqlResult2 = new List<Listing>();
            using (SqlConnection sqlConnection2 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection2.Open();
                using (SqlCommand sqlCommand2 = new SqlCommand(ListingSql_select + where + " ORDER BY " + order + " OFFSET @skip ROWS FETCH NEXT 10 ROWS ONLY", sqlConnection2))
                {
                    sqlCommand2.Parameters.AddWithValue("@kind", ((object? )kind ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@search", ((object? )search ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@term", ((object? )("%" + search + "%") ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@city", ((object? )city ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@cityTerm", ((object? )("%" + city + "%") ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@type", ((object? )type ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@gender", ((object? )gender ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@furnishing", ((object? )furnishing ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@food", ((object? )food ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@bhk", ((object? )bhk ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@amenity", ((object? )amenity ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@moveIn", ((object? )moveIn ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@min", ((object? )min ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@max", ((object? )max ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@owner", ((object? )owner ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@admin", ((object? )admin ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@category", ((object? )category ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@condition", ((object? )condition ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@roomType", ((object? )roomType ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@skip", ((object? )((page - 1) * 10) ?? DBNull.Value));
                    using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                    {
                        while (sqlReader2.Read())
                        {
                            Listing databaseRow = new Listing();
                            if (sqlReader2["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                            }

                            if (sqlReader2["OwnerId"] != DBNull.Value)
                            {
                                databaseRow.OwnerId = Convert.ToInt64(sqlReader2["OwnerId"]);
                            }

                            if (sqlReader2["Kind"] != DBNull.Value)
                            {
                                databaseRow.Kind = Convert.ToString(sqlReader2["Kind"])!;
                            }

                            if (sqlReader2["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader2["Title"])!;
                            }

                            if (sqlReader2["Description"] != DBNull.Value)
                            {
                                databaseRow.Description = Convert.ToString(sqlReader2["Description"])!;
                            }

                            if (sqlReader2["Type"] != DBNull.Value)
                            {
                                databaseRow.Type = Convert.ToString(sqlReader2["Type"])!;
                            }

                            if (sqlReader2["City"] != DBNull.Value)
                            {
                                databaseRow.City = Convert.ToString(sqlReader2["City"])!;
                            }

                            if (sqlReader2["State"] != DBNull.Value)
                            {
                                databaseRow.State = Convert.ToString(sqlReader2["State"])!;
                            }

                            if (sqlReader2["Address"] != DBNull.Value)
                            {
                                databaseRow.Address = Convert.ToString(sqlReader2["Address"])!;
                            }

                            if (sqlReader2["Price"] != DBNull.Value)
                            {
                                databaseRow.Price = Convert.ToDecimal(sqlReader2["Price"]);
                            }

                            if (sqlReader2["Deposit"] != DBNull.Value)
                            {
                                databaseRow.Deposit = Convert.ToDecimal(sqlReader2["Deposit"]);
                            }

                            if (sqlReader2["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader2["Status"])!;
                            }

                            if (sqlReader2["Available"] != DBNull.Value)
                            {
                                databaseRow.Available = Convert.ToBoolean(sqlReader2["Available"]);
                            }

                            if (sqlReader2["Reason"] != DBNull.Value)
                            {
                                databaseRow.Reason = Convert.ToString(sqlReader2["Reason"])!;
                            }

                            if (sqlReader2["Details"] != DBNull.Value)
                            {
                                databaseRow.Details = Convert.ToString(sqlReader2["Details"])!;
                            }

                            if (sqlReader2["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader2["CreatedAt"]);
                            }

                            if (sqlReader2["UpdatedAt"] != DBNull.Value)
                            {
                                databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader2["UpdatedAt"]);
                            }

                            if (sqlReader2["OwnerName"] != DBNull.Value)
                            {
                                databaseRow.OwnerName = Convert.ToString(sqlReader2["OwnerName"])!;
                            }

                            if (sqlReader2["OwnerEmail"] != DBNull.Value)
                            {
                                databaseRow.OwnerEmail = Convert.ToString(sqlReader2["OwnerEmail"])!;
                            }

                            if (sqlReader2["OwnerPhone"] != DBNull.Value)
                            {
                                databaseRow.OwnerPhone = Convert.ToString(sqlReader2["OwnerPhone"])!;
                            }

                            if (sqlReader2["OwnerStatus"] != DBNull.Value)
                            {
                                databaseRow.OwnerStatus = Convert.ToString(sqlReader2["OwnerStatus"])!;
                            }

                            if (sqlReader2["Image"] != DBNull.Value)
                            {
                                databaseRow.Image = Convert.ToString(sqlReader2["Image"])!;
                            }

                            if (sqlReader2["Rating"] != DBNull.Value)
                            {
                                databaseRow.Rating = Convert.ToDecimal(sqlReader2["Rating"]);
                            }

                            databaseRow.Data = ReadListingDetails(databaseRow.Details);
                            sqlResult2.Add(databaseRow);
                        }
                    }
                }
            }

            var rows = sqlResult2;
            {
                ViewData["Kind"] = kind;
                ViewData["Page"] = page;
                ViewData["Total"] = count;
                return rows;
            }
        }

        private List<StoredFile> FileLogic_ForOwner(long id)
        {
            return FileSql_ForOwner(id);
        }

        private List<OrderItem> WorkflowSql_Orders(long actor, string role, bool received)
        {
            List<OrderItem> sqlResult1 = new List<OrderItem>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT i.[Id], i.[OrderId], i.[ListingId], i.[SellerId], i.[Title], i.[ListingType], i.[UnitPrice], i.[Deposit], i.[Months], i.[Amount], i.[Status], i.[Collected], i.[CreatedAt], i.[ProcessedAt], i.[CompletedAt],o.BuyerId,o.CreatedAt AS OrderDate,a.FullName AS BuyerName,b.FullName AS SellerName
                FROM dbo.OrderItems i
                JOIN dbo.Orders o ON o.Id=i.OrderId
                JOIN dbo.Accounts a ON a.Id=o.BuyerId
                JOIN dbo.Accounts b ON b.Id=i.SellerId
                WHERE @role='Admin'
                OR (@received=1
                AND i.SellerId=@actor)
                OR (@received=0
                AND o.BuyerId=@actor)
                ORDER BY o.Id DESC,i.Id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@received", ((object? )received ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            OrderItem databaseRow = new OrderItem();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["OrderId"] != DBNull.Value)
                            {
                                databaseRow.OrderId = Convert.ToInt64(sqlReader1["OrderId"]);
                            }

                            if (sqlReader1["ListingId"] != DBNull.Value)
                            {
                                databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                            }

                            if (sqlReader1["SellerId"] != DBNull.Value)
                            {
                                databaseRow.SellerId = Convert.ToInt64(sqlReader1["SellerId"]);
                            }

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["ListingType"] != DBNull.Value)
                            {
                                databaseRow.ListingType = Convert.ToString(sqlReader1["ListingType"])!;
                            }

                            if (sqlReader1["UnitPrice"] != DBNull.Value)
                            {
                                databaseRow.UnitPrice = Convert.ToDecimal(sqlReader1["UnitPrice"]);
                            }

                            if (sqlReader1["Deposit"] != DBNull.Value)
                            {
                                databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                            }

                            if (sqlReader1["Months"] != DBNull.Value)
                            {
                                databaseRow.Months = Convert.ToInt32(sqlReader1["Months"]);
                            }

                            if (sqlReader1["Amount"] != DBNull.Value)
                            {
                                databaseRow.Amount = Convert.ToDecimal(sqlReader1["Amount"]);
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Collected"] != DBNull.Value)
                            {
                                databaseRow.Collected = Convert.ToBoolean(sqlReader1["Collected"]);
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["ProcessedAt"] != DBNull.Value)
                            {
                                databaseRow.ProcessedAt = Convert.ToDateTime(sqlReader1["ProcessedAt"]);
                            }

                            if (sqlReader1["CompletedAt"] != DBNull.Value)
                            {
                                databaseRow.CompletedAt = Convert.ToDateTime(sqlReader1["CompletedAt"]);
                            }

                            if (sqlReader1["BuyerId"] != DBNull.Value)
                            {
                                databaseRow.BuyerId = Convert.ToInt64(sqlReader1["BuyerId"]);
                            }

                            if (sqlReader1["OrderDate"] != DBNull.Value)
                            {
                                databaseRow.OrderDate = Convert.ToDateTime(sqlReader1["OrderDate"]);
                            }

                            if (sqlReader1["BuyerName"] != DBNull.Value)
                            {
                                databaseRow.BuyerName = Convert.ToString(sqlReader1["BuyerName"])!;
                            }

                            if (sqlReader1["SellerName"] != DBNull.Value)
                            {
                                databaseRow.SellerName = Convert.ToString(sqlReader1["SellerName"])!;
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

        private Listing ListingLogic_Accessible(long id, ClaimsPrincipal user)
        {
            var row = ListingSql_Get(id);
            if (row == null)
            {
                throw new RuleException("Listing not found.");
            }

            if (CurrentAccount.Role(user) == "Admin" || row.OwnerId == CurrentAccount.Id(user))
            {
                return row;
            }

            if (row.Status != "Approved" || !(row.OwnerStatus == "Active" || row.OwnerStatus == "Approved"))
            {
                throw new RuleException("Listing not found.");
            }

            return row;
        }

        private void WorkflowLogic_Moderate(ClaimsPrincipal user, long id, string value, string message)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "Admin");
            string[]? parts = value.Split(':');
            if (parts.Length != 2)
            {
                throw new RuleException("Invalid action.");
            }

            message = WorkflowLogic_Text(message, new[] { "Rejected", "Removed", "Blocked", "Unpublished" }.Contains(parts[1]) ? 5 : 0, 500);
            WorkflowSql_Moderation(actor, parts[0], id, parts[1], message);
        }

        const string ListingSql_select = @"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt],a.FullName AS OwnerName,a.Email AS OwnerEmail,a.Phone AS OwnerPhone,a.Status AS OwnerStatus,(SELECT TOP 1 Name
                FROM dbo.Files
                WHERE ListingId=l.Id
                AND Private=0
                ORDER BY Id) AS Image,(SELECT AVG(CAST(Rating AS decimal(4,2)))
                FROM dbo.Reviews
                WHERE ListingId=l.Id
                AND Removed=0) AS Rating
                FROM dbo.Listings l
                JOIN dbo.Accounts a ON a.Id=l.OwnerId ";
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

        private Listing? ListingSql_Get(long id)
        {
            List<Listing> sqlResult1 = new List<Listing>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(ListingSql_select + " WHERE l.Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
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

                            if (sqlReader1["OwnerName"] != DBNull.Value)
                            {
                                databaseRow.OwnerName = Convert.ToString(sqlReader1["OwnerName"])!;
                            }

                            if (sqlReader1["OwnerEmail"] != DBNull.Value)
                            {
                                databaseRow.OwnerEmail = Convert.ToString(sqlReader1["OwnerEmail"])!;
                            }

                            if (sqlReader1["OwnerPhone"] != DBNull.Value)
                            {
                                databaseRow.OwnerPhone = Convert.ToString(sqlReader1["OwnerPhone"])!;
                            }

                            if (sqlReader1["OwnerStatus"] != DBNull.Value)
                            {
                                databaseRow.OwnerStatus = Convert.ToString(sqlReader1["OwnerStatus"])!;
                            }

                            if (sqlReader1["Image"] != DBNull.Value)
                            {
                                databaseRow.Image = Convert.ToString(sqlReader1["Image"])!;
                            }

                            if (sqlReader1["Rating"] != DBNull.Value)
                            {
                                databaseRow.Rating = Convert.ToDecimal(sqlReader1["Rating"]);
                            }

                            databaseRow.Data = ReadListingDetails(databaseRow.Details);
                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return (sqlResult1).FirstOrDefault();
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

        private void WorkflowSql_Moderation(long actor, string entity, long id, string status, string reason)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        long recipient = 0;
                        string label = "";
                        if (entity == "Account")
                        {
                            List<Account> sqlResult1 = new List<Account>();
                            using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [FullName], [Email], [Phone], [PasswordHash], [Role], [Status], [Reason], [EmailVerified], [SecurityStamp], [DateOfBirth], [Gender], [Address], [City], [State], [Pincode], [Avatar], [DocumentType], [CreatedAt], [NormalizedEmail] FROM dbo.Accounts WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND Role<>'Admin'", connection, transaction))
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

                            var a = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Account not found.");
                            string? from = a.Status;
                            string? role = a.Role;
                            bool allowed;
                            if (role == "User")
                            {
                                allowed = (from == "Active" && status == "Blocked" || from == "Blocked" && status == "Active");
                            }
                            else
                            {
                                allowed = (from == "PendingApproval" && (status == "Approved" || status == "Rejected") || from == "Approved" && status == "Blocked" || from == "Blocked" && status == "Approved" || from == "Rejected" && status == "PendingApproval");
                            }

                            if (!allowed)
                            {
                                throw new RuleException("Invalid account transition.");
                            }

                            List<StoredFile> sqlResult2 = new List<StoredFile>();
                            using (SqlCommand sqlCommand2 = new SqlCommand("SELECT Id FROM dbo.Files WHERE OwnerId=@id AND ListingId IS NULL AND Private=1", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                                {
                                    while (sqlReader2.Read())
                                    {
                                        StoredFile databaseRow = new StoredFile();
                                        if (sqlReader2["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                                        }

                                        sqlResult2.Add(databaseRow);
                                    }
                                }
                            }

                            if (role == "Owner" && status == "Approved" && (sqlResult2).Count == 0)
                            {
                                throw new RuleException("Review the owner identity document before approval.");
                            }

                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("UPDATE dbo.Accounts SET Status=@status,Reason=@reason,SecurityStamp=NEWID() WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }

                            recipient = id;
                            label = "Your account";
                        }
                        else if (entity == "Listing")
                        {
                            List<Listing> sqlResult4 = new List<Listing>();
                            using (SqlCommand sqlCommand4 = new SqlCommand(@"SELECT [Id], [OwnerId], [Kind], [Title], [Description], [Type], [City], [State], [Address], [Price], [Deposit], [Status], [Available], [Reason], [Details], [CreatedAt], [UpdatedAt] FROM dbo.Listings WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand4.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader4 = sqlCommand4.ExecuteReader())
                                {
                                    while (sqlReader4.Read())
                                    {
                                        Listing databaseRow = new Listing();
                                        if (sqlReader4["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader4["Id"]);
                                        }

                                        if (sqlReader4["OwnerId"] != DBNull.Value)
                                        {
                                            databaseRow.OwnerId = Convert.ToInt64(sqlReader4["OwnerId"]);
                                        }

                                        if (sqlReader4["Kind"] != DBNull.Value)
                                        {
                                            databaseRow.Kind = Convert.ToString(sqlReader4["Kind"])!;
                                        }

                                        if (sqlReader4["Title"] != DBNull.Value)
                                        {
                                            databaseRow.Title = Convert.ToString(sqlReader4["Title"])!;
                                        }

                                        if (sqlReader4["Description"] != DBNull.Value)
                                        {
                                            databaseRow.Description = Convert.ToString(sqlReader4["Description"])!;
                                        }

                                        if (sqlReader4["Type"] != DBNull.Value)
                                        {
                                            databaseRow.Type = Convert.ToString(sqlReader4["Type"])!;
                                        }

                                        if (sqlReader4["City"] != DBNull.Value)
                                        {
                                            databaseRow.City = Convert.ToString(sqlReader4["City"])!;
                                        }

                                        if (sqlReader4["State"] != DBNull.Value)
                                        {
                                            databaseRow.State = Convert.ToString(sqlReader4["State"])!;
                                        }

                                        if (sqlReader4["Address"] != DBNull.Value)
                                        {
                                            databaseRow.Address = Convert.ToString(sqlReader4["Address"])!;
                                        }

                                        if (sqlReader4["Price"] != DBNull.Value)
                                        {
                                            databaseRow.Price = Convert.ToDecimal(sqlReader4["Price"]);
                                        }

                                        if (sqlReader4["Deposit"] != DBNull.Value)
                                        {
                                            databaseRow.Deposit = Convert.ToDecimal(sqlReader4["Deposit"]);
                                        }

                                        if (sqlReader4["Status"] != DBNull.Value)
                                        {
                                            databaseRow.Status = Convert.ToString(sqlReader4["Status"])!;
                                        }

                                        if (sqlReader4["Available"] != DBNull.Value)
                                        {
                                            databaseRow.Available = Convert.ToBoolean(sqlReader4["Available"]);
                                        }

                                        if (sqlReader4["Reason"] != DBNull.Value)
                                        {
                                            databaseRow.Reason = Convert.ToString(sqlReader4["Reason"])!;
                                        }

                                        if (sqlReader4["Details"] != DBNull.Value)
                                        {
                                            databaseRow.Details = Convert.ToString(sqlReader4["Details"])!;
                                        }

                                        if (sqlReader4["CreatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.CreatedAt = Convert.ToDateTime(sqlReader4["CreatedAt"]);
                                        }

                                        if (sqlReader4["UpdatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader4["UpdatedAt"]);
                                        }

                                        databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                        sqlResult4.Add(databaseRow);
                                    }
                                }
                            }

                            var l = (sqlResult4).FirstOrDefault() ?? throw new RuleException("Listing not found.");
                            string? from = l.Status;
                            bool allowed = from == "PendingVerification" && (status == "Approved" || status == "Rejected") || from == "Approved" && (status == "Unpublished" || status == "Rejected" || status == "Removed") || from == "Rejected" && status == "Approved" || from == "Unpublished" && status == "Approved";
                            if (!allowed)
                            {
                                throw new RuleException("Invalid listing transition.");
                            }

                            int sqlResult5;
                            using (SqlCommand sqlCommand5 = new SqlCommand("UPDATE dbo.Listings SET Status=@status,Reason=@reason WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand5.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                                sqlCommand5.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                                sqlCommand5.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult5 = sqlCommand5.ExecuteNonQuery();
                            }

                            recipient = l.OwnerId;
                            label = l.Title;
                        }
                        else if (entity == "Review" && status == "Removed")
                        {
                            List<Review> sqlResult6 = new List<Review>();
                            using (SqlCommand sqlCommand6 = new SqlCommand(@"SELECT [Id], [UserId], [ListingId], [Rating], [Text], [Removed], [Reason], [CreatedAt] FROM dbo.Reviews WHERE Id=@id AND Removed=0", connection, transaction))
                            {
                                sqlCommand6.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                using (SqlDataReader sqlReader6 = sqlCommand6.ExecuteReader())
                                {
                                    while (sqlReader6.Read())
                                    {
                                        Review databaseRow = new Review();
                                        if (sqlReader6["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader6["Id"]);
                                        }

                                        if (sqlReader6["UserId"] != DBNull.Value)
                                        {
                                            databaseRow.UserId = Convert.ToInt64(sqlReader6["UserId"]);
                                        }

                                        if (sqlReader6["ListingId"] != DBNull.Value)
                                        {
                                            databaseRow.ListingId = Convert.ToInt64(sqlReader6["ListingId"]);
                                        }

                                        if (sqlReader6["Rating"] != DBNull.Value)
                                        {
                                            databaseRow.Rating = Convert.ToInt32(sqlReader6["Rating"]);
                                        }

                                        if (sqlReader6["Text"] != DBNull.Value)
                                        {
                                            databaseRow.Text = Convert.ToString(sqlReader6["Text"])!;
                                        }

                                        if (sqlReader6["Removed"] != DBNull.Value)
                                        {
                                            databaseRow.Removed = Convert.ToBoolean(sqlReader6["Removed"]);
                                        }

                                        if (sqlReader6["Reason"] != DBNull.Value)
                                        {
                                            databaseRow.Reason = Convert.ToString(sqlReader6["Reason"])!;
                                        }

                                        if (sqlReader6["CreatedAt"] != DBNull.Value)
                                        {
                                            databaseRow.CreatedAt = Convert.ToDateTime(sqlReader6["CreatedAt"]);
                                        }

                                        sqlResult6.Add(databaseRow);
                                    }
                                }
                            }

                            var review = (sqlResult6).FirstOrDefault() ?? throw new RuleException("Review not found.");
                            int sqlResult7;
                            using (SqlCommand sqlCommand7 = new SqlCommand("UPDATE dbo.Reviews SET Removed=1,Reason=@reason WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand7.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                                sqlCommand7.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult7 = sqlCommand7.ExecuteNonQuery();
                            }

                            recipient = review.UserId;
                            label = "Your review";
                        }
                        else
                        {
                            throw new RuleException("Unknown moderation action.");
                        }

                        int sqlResult8;
                        using (SqlCommand sqlCommand8 = new SqlCommand(@"INSERT dbo.ModerationHistory(ActorId,Entity,EntityId,Action,Reason)
                VALUES(@actor,@entity,@id,@status,@reason)", connection, transaction))
                        {
                            sqlCommand8.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@entity", ((object? )entity ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                            sqlCommand8.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                            sqlResult8 = sqlCommand8.ExecuteNonQuery();
                        }

                        WorkflowSql_Notify(connection, transaction, recipient, label + " is " + (status ?? "").ToLowerInvariant() + ". " + reason, "/Workspace/Notifications");
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
