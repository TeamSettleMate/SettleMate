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
    public class WishlistController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public WishlistController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(int page = 1)
        {
            return View(CatalogQueryLogic_Wishlist(User, page));
        }

        [HttpPost]
        public IActionResult Toggle(RecordAction model)
        {
            ValidateForm();
            WorkflowLogic_ToggleWishlist(User, model.Id);
            return Success(model);
        }

        private List<Listing> CatalogQueryLogic_Wishlist(ClaimsPrincipal user, int page)
        {
            if (CurrentAccount.Role(user) != "User")
            {
                throw new RuleException("Sign in as a User.");
            }

            List<Listing> listings = ListingSql_Wishlist(CurrentAccount.Id(user));
            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(listings.Count / 10.0)));
            ViewData["Title"] = "Your wishlist";
            ViewData["Kind"] = "Saved";
            ViewData["Total"] = listings.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return listings.GetRange(start, Math.Min(10, listings.Count - start));
        }

        private void WorkflowLogic_ToggleWishlist(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            ListingLogic_Wishlist(id, user);
        }

        private List<Listing> ListingSql_Wishlist(long user)
        {
            List<Listing> sqlResult1 = new List<Listing>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(ListingSql_select + " JOIN dbo.Wishlists w ON w.ListingId=l.Id WHERE w.AccountId=@user AND l.Status='Approved' AND a.Status IN('Active','Approved') ORDER BY l.Id DESC", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
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

            return sqlResult1;
        }

        private void ListingLogic_Wishlist(long id, ClaimsPrincipal user)
        {
            if (CurrentAccount.Role(user) != "User")
            {
                throw new RuleException("Sign in as a User.");
            }

            var row = ListingLogic_Accessible(id, user);
            if (row.Status != "Approved")
            {
                throw new RuleException("This listing is unavailable.");
            }

            ListingSql_ToggleWishlist(CurrentAccount.Id(user), id);
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

        private void ListingSql_ToggleWishlist(long user, long id)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        int sqlResult1;
                        using (SqlCommand sqlCommand1 = new SqlCommand("DELETE dbo.Wishlists WHERE AccountId=@user AND ListingId=@id", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlResult1 = sqlCommand1.ExecuteNonQuery();
                        }

                        int n = sqlResult1;
                        if (n == 0)
                        {
                            int sqlResult2;
                            using (SqlCommand sqlCommand2 = new SqlCommand("INSERT dbo.Wishlists VALUES(@user,@id)", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@user", ((object? )user ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult2 = sqlCommand2.ExecuteNonQuery();
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
