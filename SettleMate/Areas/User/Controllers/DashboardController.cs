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
    public class DashboardController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public DashboardController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(WorkspaceQueryLogic_Dashboard(User));
        }

        private List<DashboardMetric> WorkspaceQueryLogic_Dashboard(ClaimsPrincipal user)
        {
            List<DashboardMetric> rows = WorkflowSql_Dashboard(CurrentAccount.Id(user), CurrentAccount.Role(user));
            ViewData["Title"] = "Dashboard";
            ViewData["Related"] = WorkflowSql_Charts(CurrentAccount.Id(user), CurrentAccount.Role(user));
            ViewData["RoomPosts"] = new List<Listing>();
            ViewData["MarketItems"] = new List<Listing>();
            ViewData["Extra"] = new List<Listing>();
            if (CurrentAccount.Role(user) == "User")
            {
                List<Listing> rooms = WorkspaceQueryLogic_Browse("Room");
                List<Listing> items = WorkspaceQueryLogic_Browse("Item");
                List<Listing> properties = WorkspaceQueryLogic_Browse("Property");
                ViewData["RoomPosts"] = rooms.GetRange(0, Math.Min(3, rooms.Count));
                ViewData["MarketItems"] = items.GetRange(0, Math.Min(3, items.Count));
                ViewData["Extra"] = properties.GetRange(0, Math.Min(3, properties.Count));
            }

            List<VisitRequest> visits = WorkflowSql_Visits(CurrentAccount.Id(user), CurrentAccount.Role(user));
            ViewData["Activity"] = visits.GetRange(0, Math.Min(5, visits.Count));
            List<Account> pending = new List<Account>();
            if (CurrentAccount.Role(user) == "Admin")
            {
                foreach (Account owner in WorkflowSql_Accounts("Owner"))
                {
                    if (owner.Status == "PendingApproval" && pending.Count < 5)
                    {
                        pending.Add(owner);
                    }
                }
            }

            ViewData["Pending"] = pending;
            return rows;
        }

        private List<DashboardMetric> WorkflowSql_Dashboard(long actor, string role)
        {
            List<DashboardMetric> sqlResult1 = new List<DashboardMetric>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT 'Properties' AS Label,COUNT(*) AS Value,'/Workspace/Listings?kind=Property' AS Url
                FROM dbo.Listings
                WHERE Kind='Property'
                AND (@role='Admin'
                OR OwnerId=@actor)
UNION ALL SELECT 'Pending verification',COUNT(*),'/Workspace/Listings?kind=Property&status=PendingVerification'
                FROM dbo.Listings
                WHERE Status='PendingVerification'
                AND (@role='Admin'
                OR OwnerId=@actor)
UNION ALL SELECT 'Visit requests',COUNT(*),'/Workspace/Visits'
                FROM dbo.VisitRequests v
                JOIN dbo.Listings l ON l.Id=v.ListingId
                WHERE @role='Admin'
                OR l.OwnerId=@actor
                OR v.UserId=@actor
UNION ALL SELECT 'Orders',COUNT(*),'/Workspace/Orders'
                FROM dbo.OrderItems i
                JOIN dbo.Orders o ON o.Id=i.OrderId
                WHERE @role='Admin'
                OR i.SellerId=@actor
                OR o.BuyerId=@actor
UNION ALL SELECT 'Open support',COUNT(*),'/Workspace/Support'
                FROM dbo.Tickets
                WHERE Status='Open'
                AND (@role='Admin'
                OR AccountId=@actor)
UNION ALL SELECT 'Reviews',COUNT(*),'/Workspace/Reviews'
                FROM dbo.Reviews r
                JOIN dbo.Listings l ON l.Id=r.ListingId
                WHERE r.Removed=0
                AND (@role='Admin'
                OR l.OwnerId=@actor
                OR r.UserId=@actor)
UNION ALL SELECT 'Users',COUNT(*),'/Admin/Accounts?role=User'
                FROM dbo.Accounts
                WHERE @role='Admin'
                AND Role='User'
UNION ALL SELECT 'Owners awaiting approval',COUNT(*),'/Admin/Accounts?role=Owner&status=PendingApproval'
                FROM dbo.Accounts
                WHERE @role='Admin'
                AND Role='Owner'
                AND Status='PendingApproval'", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            DashboardMetric databaseRow = new DashboardMetric();
                            if (sqlReader1["Label"] != DBNull.Value)
                            {
                                databaseRow.Label = Convert.ToString(sqlReader1["Label"])!;
                            }

                            if (sqlReader1["Value"] != DBNull.Value)
                            {
                                databaseRow.Value = Convert.ToDecimal(sqlReader1["Value"]);
                            }

                            if (sqlReader1["Url"] != DBNull.Value)
                            {
                                databaseRow.Url = Convert.ToString(sqlReader1["Url"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
        }

        private List<DashboardMetric> WorkflowSql_Charts(long actor, string role)
        {
            List<DashboardMetric> sqlResult1 = new List<DashboardMetric>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT 'Property types' AS Chart,Type AS Label,COUNT(*) AS Value
                FROM dbo.Listings
                WHERE Kind='Property'
                AND (@role='Admin'
                OR OwnerId=@actor)
                GROUP BY Type
UNION ALL SELECT 'Property status',Status,COUNT(*)
                FROM dbo.Listings
                WHERE Kind='Property'
                AND (@role='Admin'
                OR OwnerId=@actor)
                GROUP BY Status
UNION ALL SELECT 'Visits by week',CONCAT(YEAR(v.CreatedAt),'-W',DATEPART(iso_week,v.CreatedAt)),COUNT(*)
                FROM dbo.VisitRequests v
                JOIN dbo.Listings l ON l.Id=v.ListingId
                WHERE (@role='Admin'
                OR l.OwnerId=@actor)
                AND v.CreatedAt>DATEADD(day,-35,SYSUTCDATETIME())
                GROUP BY YEAR(v.CreatedAt),DATEPART(iso_week,v.CreatedAt)
UNION ALL SELECT 'Property views',CONVERT(varchar(10),v.ViewDate,23),COUNT(*)
                FROM dbo.PropertyViews v
                JOIN dbo.Listings l ON l.Id=v.ListingId
                WHERE (@role='Admin'
                OR l.OwnerId=@actor)
                AND v.ViewDate>DATEADD(day,-30,SYSUTCDATETIME())
                GROUP BY v.ViewDate
UNION ALL SELECT 'Registrations',CONCAT(CONVERT(varchar(10),CreatedAt,23),' ',Role),COUNT(*)
                FROM dbo.Accounts
                WHERE @role='Admin'
                AND CreatedAt>DATEADD(day,-30,SYSUTCDATETIME())
                GROUP BY CONVERT(varchar(10),CreatedAt,23),Role", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            DashboardMetric databaseRow = new DashboardMetric();
                            if (sqlReader1["Label"] != DBNull.Value)
                            {
                                databaseRow.Label = Convert.ToString(sqlReader1["Label"])!;
                            }

                            if (sqlReader1["Value"] != DBNull.Value)
                            {
                                databaseRow.Value = Convert.ToDecimal(sqlReader1["Value"]);
                            }

                            if (sqlReader1["Chart"] != DBNull.Value)
                            {
                                databaseRow.Chart = Convert.ToString(sqlReader1["Chart"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
        }

        private List<Listing> WorkspaceQueryLogic_Browse(string kind)
        {
            return ListingSql_Browse(new ListingSearch { Kind = kind });
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
