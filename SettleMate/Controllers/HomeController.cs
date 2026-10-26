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
    public class HomeController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public HomeController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        public IActionResult Index()
        {
            return View(CatalogQueryLogic_Home());
        }

        [HttpGet("/About")]
        public IActionResult About()
        {
            ViewData["Title"] = "A place to belong";
            return View("Info", "About");
        }

        [HttpGet("/Services")]
        public IActionResult Services()
        {
            ViewData["Title"] = "Built around your move";
            return View("Info", "Services");
        }

        [HttpGet("/Terms")]
        public IActionResult Terms()
        {
            ViewData["Title"] = "Terms and conditions";
            return View("Info", "Terms");
        }

        [HttpGet("/Privacy")]
        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy policy";
            return View("Info", "Privacy");
        }

        [HttpGet("/Contact")]
        public IActionResult Contact()
        {
            return View(new SupportRequest());
        }

        [HttpPost("/Contact"), EnableRateLimiting("contact")]
        public IActionResult Contact(SupportRequest m)
        {
            if (!ModelState.IsValid)
            {
                return View(m);
            }

            long id = WorkflowLogic_Contact(User, m);
            TempData["Success"] = $"Message received. Your ticket is SM-{id:D6}.";
            return Redirect("/Contact");
        }

        [Route("/Error/{code:int}"), IgnoreAntiforgeryToken]
        public IActionResult Error(int code = 500)
        {
            Response.StatusCode = code;
            string message = "Something went wrong. Please try again.";
            if (code == 403)
            {
                message = "You do not have access to this page.";
            }
            else if (code == 404)
            {
                message = "This page could not be found.";
            }
            else if (code == 429)
            {
                message = "Too many requests. Please wait a minute.";
            }

            return View("~/Views/Shared/Error.cshtml", message);
        }

        private List<Listing> CatalogQueryLogic_Home()
        {
            List<Listing> properties = CatalogQueryLogic_Browse(new ListingSearch { Kind = "Property" });
            int count = Convert.ToInt32(ViewData["Total"]);
            List<Listing> items = CatalogQueryLogic_Browse(new ListingSearch { Kind = "Item" });
            ViewData["Total"] = count;
            ViewData["Title"] = "Find your place";
            ViewData["Related"] = items.GetRange(0, Math.Min(3, items.Count));
            return properties.GetRange(0, Math.Min(3, properties.Count));
        }

        private long WorkflowLogic_Contact(ClaimsPrincipal user, SupportRequest m)
        {
            m.FullName = WorkflowLogic_Text(m.FullName, 1, 100);
            m.Subject = WorkflowLogic_Text(m.Subject, 1, 150);
            m.Message = WorkflowLogic_Text(m.Message, 10, 2000);
            return WorkflowSql_Contact(CurrentAccount.Id(user) > 0 ? CurrentAccount.Id(user) : null, m);
        }

        private List<Listing> CatalogQueryLogic_Browse(ListingSearch query)
        {
            if (query.Kind != "Property" && query.Kind != "Room" && query.Kind != "Finder" && query.Kind != "Item")
            {
                throw new RuleException("Unknown listing type.");
            }

            List<Listing> listings = ListingSql_Browse(query);
            ViewData["Title"] = "Find your people";
            if (query.Kind == "Property")
            {
                ViewData["Title"] = "Find your next place";
            }

            if (query.Kind == "Item")
            {
                ViewData["Title"] = "Everyday essentials";
            }

            return listings;
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

        private long WorkflowSql_Contact(long? actor, SupportRequest m)
        {
            List<Ticket> sqlResult1 = new List<Ticket>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"INSERT dbo.Tickets(AccountId,FullName,Email,Subject,Message)
                OUTPUT INSERTED.Id
                VALUES(@actor,@FullName,@Email,@Subject,@Message)", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@FullName", ((object? )m.FullName ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Email", ((object? )m.Email ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Subject", ((object? )m.Subject ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@Message", ((object? )m.Message ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Ticket databaseRow = new Ticket();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return (sqlResult1)[0].Id;
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
