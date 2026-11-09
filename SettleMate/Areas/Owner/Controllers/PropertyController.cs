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
    public class PropertyController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public PropertyController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        private static bool ValidKind(string kind)
        {
            return kind == "Property";
        }

        [HttpGet]
        public IActionResult Manage(string kind = "Property", string search = "", string status = "", int page = 1)
        {
            if (!ValidKind(kind))
            {
                return NotFound();
            }

            return View(WorkspaceQueryLogic_Listings(User, kind, search, status, page));
        }

        [HttpGet]
        public IActionResult Edit(long id = 0, string kind = "Property")
        {
            if (!ValidKind(kind))
            {
                return NotFound();
            }

            return View(ListingLogic_Edit(id, kind, User));
        }

        [HttpPost]
        [RequestSizeLimit(100_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
        public IActionResult Edit(Listing model)
        {
            if (!ValidKind(model.Kind))
            {
                return NotFound();
            }

            if (model.SaveMode == "Draft")
            {
                foreach (string? key in new[]
                {
                    "Description",
                    "City",
                    "State",
                    "Address",
                    "Pincode",
                    "Latitude",
                    "Longitude",
                    "AvailableFrom",
                    "Price"
                }

                )
                {
                    ModelState.Remove(key);
                }

                if (model.Price < 0 || model.Price > 10000000)
                {
                    ModelState.AddModelError("Price", "Enter a non-negative draft price.");
                }
            }

            if (!ModelState.IsValid)
            {
                RestoreAttachments(model);
                return View(model);
            }

            try
            {
                ListingLogic_Save(model, User);
                if (model.Kind == "Property")
                {
                    TempData["Success"] = "Property saved. Submitted properties appear after admin approval.";
                }
                else
                {
                    TempData["Success"] = "Listing saved.";
                }

                return RedirectToAction(nameof(Manage), new { kind = model.Kind });
            }
            catch (RuleException exception)
            {
                ModelState.AddModelError("", exception.Message);
                RestoreAttachments(model);
                return View(model);
            }
        }

        private void RestoreAttachments(Listing model)
        {
            if (model.Id == 0)
            {
                return;
            }

            Listing? existing = ListingLogic_Edit(model.Id, model.Kind, User);
            model.ExistingImages = existing.ExistingImages;
            model.ExistingDocuments = existing.ExistingDocuments;
        }

        private void CheckListingKind(long id)
        {
            var listing = ListingLogic_Accessible(id, User);
            if (!ValidKind(listing.Kind))
            {
                throw new RuleException("Listing not found.");
            }
        }

        [HttpPost]
        public IActionResult Delete(ReasonAction model)
        {
            ValidateForm();
            CheckListingKind(model.Id);
            WorkflowLogic_DeleteListing(User, model.Id, message: model.Reason);
            return Success(model);
        }

        [HttpPost]
        public IActionResult Availability(RecordAction model)
        {
            ValidateForm();
            CheckListingKind(model.Id);
            WorkflowLogic_ToggleAvailability(User, model.Id);
            return Success(model);
        }

        private List<Listing> WorkspaceQueryLogic_Listings(ClaimsPrincipal user, string kind, string search, string status, int page)
        {
            if (CurrentAccount.Role(user) == "Owner" && kind != "Property" || CurrentAccount.Role(user) == "User" && kind != "Item" && kind != "Room" && kind != "Finder")
            {
                throw new RuleException("You cannot manage this listing type.");
            }

            bool admin = CurrentAccount.Role(user) == "Admin";
            long? owner = null;
            if (!admin)
            {
                owner = CurrentAccount.Id(user);
            }

            List<Listing> rows = ListingSql_Browse(new ListingSearch { Kind = kind, Search = search, Page = page }, owner, admin, status);
            ViewData["Title"] = "Roommate posts";
            if (kind == "Property")
            {
                ViewData["Title"] = "Properties";
            }

            if (kind == "Item")
            {
                ViewData["Title"] = "Marketplace listings";
            }

            return rows;
        }

        private Listing ListingLogic_Edit(long id, string kind, ClaimsPrincipal user)
        {
            ListingLogic_KindAllowed(kind, user);
            if (id == 0)
            {
                return new Listing()
                {
                    Kind = kind,
                    Type = kind == "Item" ? "Sell" : "Room",
                    AvailableFrom = DateTime.Today
                };
            }

            var row = ListingLogic_Accessible(id, user);
            if (row.OwnerId != CurrentAccount.Id(user) || row.Kind != kind)
            {
                throw new RuleException("Listing not found.");
            }

            Listing? model = ReadListingDetails(row.Details);
            model.Id = id;
            model.Kind = kind;
            model.RejectionReason = row.Reason;
            var all = ListingSql_Files(id);
            model.ExistingImages = new List<StoredFile>();
            model.ExistingDocuments = new List<StoredFile>();
            foreach (var file in all)
            {
                if (file.Private)
                {
                    model.ExistingDocuments.Add(file);
                }
                else
                {
                    model.ExistingImages.Add(file);
                }
            }

            return model;
        }

        private long ListingLogic_Save(Listing model, ClaimsPrincipal user)
        {
            ListingLogic_KindAllowed(model.Kind, user);
            if (model.Description == null)
            {
                model.Description = "";
            }

            if (model.City == null)
            {
                model.City = "";
            }

            if (model.State == null)
            {
                model.State = "";
            }

            if (model.Address == null)
            {
                model.Address = "";
            }

            Listing? existing;
            if (model.Id > 0)
            {
                existing = ListingLogic_Accessible(model.Id, user);
            }
            else
            {
                existing = null;
            }

            if (existing != null && existing.OwnerId != CurrentAccount.Id(user))
            {
                throw new RuleException("Listing not found.");
            }

            if (model.Kind != "Item" && !new[]
            {
                "Room",
                "Flat",
                "PG",
                "Hostel"
            }.Contains(model.Type))
            {
                throw new RuleException("Choose a valid property type.");
            }

            if (model.Kind == "Item" && (!new[]
            {
                "Sell",
                "Rent"
            }.Contains(model.Type) || !new[]
            {
                "Furniture",
                "Electronics",
                "Kitchen",
                "Books",
                "Other"
            }.Contains(model.Category) || !new[]
            {
                "New",
                "Like New",
                "Good",
                "Fair"
            }.Contains(model.Condition)))
            {
                throw new RuleException("Choose valid item details.");
            }

            if (!new[]
            {
                "Any",
                "Male",
                "Female",
                "Other"
            }.Contains(model.Gender))
            {
                throw new RuleException("Choose a valid preference.");
            }

            if (model.Kind != "Item" && (!new[]
            {
                "Unfurnished",
                "Semi-furnished",
                "Furnished"
            }.Contains(model.Furnishing) || !new[]
            {
                "Single",
                "Double",
                "Triple",
                "Shared"
            }.Contains(model.RoomType) || !new[]
            {
                "Per Room",
                "Per Bed",
                "Entire"
            }.Contains(model.Basis)))
            {
                throw new RuleException("Choose valid room details.");
            }

            if (model.Kind == "Property" && (!new[]
            {
                "Owner",
                "Authorized Representative"
            }.Contains(model.OwnerRelation) || !new[]
            {
                "Government ID",
                "Passport",
                "Driving Licence"
            }.Contains(model.DocumentType)))
            {
                throw new RuleException("Choose valid verification details.");
            }

            foreach (string amenity in model.Amenities)
            {
                if (!ListingLogic_Amenities.Contains(amenity))
                {
                    throw new RuleException("Unknown amenity.");
                }
            }

            List<StoredFile> prior;
            if (model.Id > 0)
            {
                prior = ListingSql_Files(model.Id);
            }
            else
            {
                prior = new List<StoredFile>();
            }

            List<StoredFile> remove = new List<StoredFile>();
            List<long> requestedImageIds = new List<long>();
            foreach (long imageId in model.RemoveImageIds)
            {
                if (!requestedImageIds.Contains(imageId))
                {
                    requestedImageIds.Add(imageId);
                }
            }

            int existingImageCount = 0;
            bool hasIdentity = false;
            bool hasPropertyProof = false;
            bool hasAuthorization = false;
            foreach (var file in prior)
            {
                if (!file.Private)
                {
                    existingImageCount++;
                    if (requestedImageIds.Contains(file.Id))
                    {
                        remove.Add(file);
                    }
                }

                if (file.Purpose == "Identity")
                {
                    hasIdentity = true;
                }

                if (file.Purpose == "PropertyProof")
                {
                    hasPropertyProof = true;
                }

                if (file.Purpose == "Authorization")
                {
                    hasAuthorization = true;
                }
            }

            if (requestedImageIds.Count != remove.Count)
            {
                throw new RuleException("An image does not belong to this listing.");
            }

            int imageCount = existingImageCount - remove.Count + model.Images.Count;
            bool draft = model.Kind == "Property" && model.SaveMode == "Draft";
            if (model.Kind == "Property" && !draft)
            {
                if (model.Type == "Flat" && (model.Bhk < 1 || model.Bedrooms < 1))
                {
                    throw new RuleException("Enter BHK and bedrooms for a flat.");
                }

                if (model.Type == "Hostel" && model.Beds < 1)
                {
                    throw new RuleException("Enter the hostel bed count.");
                }

                if (imageCount < 5 || imageCount > 15)
                {
                    throw new RuleException("Properties require 5–15 images.");
                }

                if (model.Amenities.Length == 0 || model.Latitude == null || model.Longitude == null || string.IsNullOrWhiteSpace(model.Address) || string.IsNullOrWhiteSpace(model.Pincode) || model.AvailableFrom == null)
                {
                    throw new RuleException("Complete location, availability and at least one amenity.");
                }

                if (!model.Declaration1 || !model.Declaration2 || !model.Declaration3 || !model.Declaration4)
                {
                    throw new RuleException("Accept all four declarations.");
                }

                if (model.IdentityDocument == null && !hasIdentity)
                {
                    throw new RuleException("Upload an identity document.");
                }

                if (model.PropertyProof == null && !hasPropertyProof)
                {
                    throw new RuleException("Upload property proof.");
                }

                if (model.OwnerRelation == "Authorized Representative" && model.AuthorizationDocument == null && !hasAuthorization)
                {
                    throw new RuleException("Upload authorization proof.");
                }
            }

            if (model.Kind == "Property" && imageCount > 15)
            {
                throw new RuleException("Use at most 15 images.");
            }

            if (model.Kind == "Item" && (imageCount < 1 || imageCount > 6))
            {
                throw new RuleException("Items require 1–6 images.");
            }

            if ((model.Kind == "Room" || model.Kind == "Finder") && imageCount > 6)
            {
                throw new RuleException("Use at most six images.");
            }

            if (model.AvailableFrom?.Date < DateTime.Today && model.Id == 0)
            {
                throw new RuleException("Availability date cannot be in the past.");
            }

            if (model.Kind == "Item" && model.Type == "Sell")
            {
                model.Deposit = 0;
            }

            List<StoredFile>? saved = new List<StoredFile>();
            bool attached = false;
            try
            {
                foreach (IFormFile? image in model.Images)
                {
                    saved.Add(FileLogic_Save(image, false, "Image"));
                }

                if (model.Kind == "Property")
                {
                    if (model.IdentityDocument != null)
                    {
                        saved.Add(FileLogic_Save(model.IdentityDocument, true, "Identity"));
                    }

                    if (model.PropertyProof != null)
                    {
                        saved.Add(FileLogic_Save(model.PropertyProof, true, "PropertyProof"));
                    }

                    if (model.AuthorizationDocument != null)
                    {
                        saved.Add(FileLogic_Save(model.AuthorizationDocument, true, "Authorization"));
                    }
                }

                model.Images = new List<IFormFile>();
                model.IdentityDocument = null;
                model.PropertyProof = null;
                model.AuthorizationDocument = null;
                model.ExistingImages = new List<StoredFile>();
                model.ExistingDocuments = new List<StoredFile>();
                model.RemoveImageIds = new long[0];
                string? status;
                if (model.Kind == "Property")
                {
                    if (draft)
                    {
                        status = "Draft";
                    }
                    else
                    {
                        status = "PendingVerification";
                    }
                }
                else
                {
                    status = "Approved";
                }

                long[] removedImageIds = new long[remove.Count];
                for (int index = 0; index < remove.Count; index++)
                {
                    removedImageIds[index] = remove[index].Id;
                }

                long id = ListingSql_Save(CurrentAccount.Id(user), model, JsonSerializer.Serialize<ListingData>(model), status, saved, removedImageIds);
                attached = true;
                foreach (var file in remove)
                {
                    try
                    {
                        FileLogic_Delete(new StoredFile(file.Name, file.OriginalName, file.ContentType, false, file.Purpose));
                    }
                    catch (IOException)
                    { /* Detached files can be cleaned during maintenance. */
                    }
                }

                return id;
            }
            catch
            {
                if (!attached)
                {
                    foreach (StoredFile? file in saved)
                    {
                        FileLogic_Delete(file);
                    }
                }

                throw;
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

        private void WorkflowLogic_DeleteListing(ClaimsPrincipal user, long id, string message)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "Owner", "User");
            WorkflowSql_DeleteListing(actor, CurrentAccount.Role(user), id, WorkflowLogic_Text(message, 5, 500));
        }

        private void WorkflowLogic_ToggleAvailability(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            ListingLogic_Availability(id, user);
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

        private static void ListingLogic_KindAllowed(string kind, ClaimsPrincipal user)
        {
            if (CurrentAccount.Role(user) == "Owner" && kind == "Property")
            {
                return;
            }

            if (CurrentAccount.Role(user) == "User" && (kind == "Room" || kind == "Finder" || kind == "Item"))
            {
                return;
            }

            throw new RuleException("You cannot create this listing type.");
        }

        private List<StoredFile> ListingSql_Files(long id)
        {
            List<StoredFile> sqlResult1 = new List<StoredFile>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [OwnerId], [ListingId], [Name], [OriginalName], [ContentType], [Private], [Purpose], [CreatedAt] FROM dbo.Files WHERE ListingId=@id ORDER BY Id", sqlConnection1))
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

        public static readonly string[] ListingLogic_Amenities = new string[]
        {
            "Wi-Fi",
            "Parking",
            "AC",
            "Washing Machine",
            "Refrigerator",
            "TV",
            "Geyser",
            "Power Backup",
            "Security",
            "CCTV",
            "Lift",
            "Housekeeping",
            "Kitchen",
            "Study Table",
            "Bed",
            "Wardrobe",
            "Hot Water",
            "Balcony",
            "Gym",
            "Common Area"
        };
        private StoredFile FileLogic_Save(IFormFile file, bool privateFile, string purpose)
        {
            if (file.Length < 1 || file.Length > 5242880)
            {
                throw new RuleException("Each file must be between 1 byte and 5 MB.");
            }

            string? extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!(privateFile ? new[]
            {
                ".pdf",
                ".jpg",
                ".jpeg",
                ".png"
            }

            : new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            }

            ).Contains(extension))
            {
                throw new RuleException("Unsupported file type.");
            }

            using (Stream? source = file.OpenReadStream())
            {
                byte[]? header = new byte[12];
                int n = source.Read(header);
                source.Position = 0;
                bool valid;
                if (extension is ".pdf")
                {
                    valid = n >= 5 && System.Text.Encoding.ASCII.GetString(header, 0, 5) == "%PDF-";
                }
                else if (extension is ".png")
                {
                    valid = n >= 8 && header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                }
                else if (extension is ".jpg" or ".jpeg")
                {
                    valid = n >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
                }
                else if (extension is ".webp")
                {
                    valid = n >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
                }
                else
                {
                    valid = false;
                }

                if (!valid)
                {
                    throw new RuleException("The file content does not match its extension.");
                }

                string? name = Guid.NewGuid().ToString("N") + extension;
                string? folder;
                if (privateFile)
                {
                    folder = Path.Combine(env.ContentRootPath, "App_Data", "private");
                }
                else
                {
                    folder = Path.Combine(env.WebRootPath, "uploads");
                }

                Directory.CreateDirectory(folder);
                using (FileStream? output = System.IO.File.Create(Path.Combine(folder, name)))
                    source.CopyTo(output);
                string? mime;
                if (extension is ".pdf")
                {
                    mime = "application/pdf";
                }
                else if (extension is ".png")
                {
                    mime = "image/png";
                }
                else if (extension is ".webp")
                {
                    mime = "image/webp";
                }
                else
                {
                    mime = "image/jpeg";
                }

                return new StoredFile(name, Path.GetFileName(file.FileName)[..Math.Min(255, Path.GetFileName(file.FileName).Length)], mime, privateFile, purpose);
            }
        }

        private long ListingSql_Save(long owner, Listing m, string details, string status, List<StoredFile> uploads, long[] remove)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        SqlParameter[]? args = new SqlParameter[]
                        {
                            new SqlParameter("@owner", ((object? )owner ?? DBNull.Value)),
                            new SqlParameter("@Id", ((object? )m.Id ?? DBNull.Value)),
                            new SqlParameter("@Kind", ((object? )m.Kind ?? DBNull.Value)),
                            new SqlParameter("@Title", ((object? )m.Title ?? DBNull.Value)),
                            new SqlParameter("@Description", ((object? )m.Description ?? DBNull.Value)),
                            new SqlParameter("@Type", ((object? )m.Type ?? DBNull.Value)),
                            new SqlParameter("@City", ((object? )m.City ?? DBNull.Value)),
                            new SqlParameter("@State", ((object? )m.State ?? DBNull.Value)),
                            new SqlParameter("@Address", ((object? )m.Address ?? DBNull.Value)),
                            new SqlParameter("@Price", ((object? )m.Price ?? DBNull.Value)),
                            new SqlParameter("@Deposit", ((object? )m.Deposit ?? DBNull.Value)),
                            new SqlParameter("@details", ((object? )details ?? DBNull.Value)),
                            new SqlParameter("@status", ((object? )status ?? DBNull.Value))
                        };
                        string? sql;
                        if (m.Id == 0)
                        {
                            sql = @"INSERT dbo.Listings(OwnerId,Kind,Title,Description,Type,City,State,Address,Price,Deposit,Details,Status)
                OUTPUT INSERTED.Id
                VALUES(@owner,@Kind,@Title,@Description,@Type,@City,@State,@Address,@Price,@Deposit,@details,@status)";
                        }
                        else
                        {
                            sql = @"UPDATE dbo.Listings
                SET Title=@Title,Description=@Description,Type=@Type,City=@City,State=@State,Address=@Address,Price=@Price,Deposit=@Deposit,Details=@details,Status=@status,UpdatedAt=SYSUTCDATETIME()
                OUTPUT INSERTED.Id
                WHERE Id=@Id
                AND OwnerId=@owner
                AND Kind=@Kind
                AND Status NOT IN('Removed','Reserved','Sold')";
                        }

                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(sql, connection, transaction))
                        {
                            foreach (SqlParameter parameter in args)
                            {
                                sqlCommand1.Parameters.AddWithValue(parameter.ParameterName, parameter.Value);
                            }

                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Listing databaseRow = new Listing();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var rows = sqlResult1;
                        if (rows.Count == 0)
                        {
                            throw new RuleException("Listing cannot be edited in its current state.");
                        }

                        long listing = rows[0].Id;
                        foreach (StoredFile? file in uploads)
                        {
                            int sqlResult2;
                            using (SqlCommand sqlCommand2 = new SqlCommand(@"INSERT dbo.Files(OwnerId,ListingId,Name,OriginalName,ContentType,Private,Purpose)
                VALUES(@owner,@listing,@Name,@OriginalName,@ContentType,@Private,@Purpose)", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@owner", ((object? )owner ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@Name", ((object? )file.Name ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@OriginalName", ((object? )file.OriginalName ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@ContentType", ((object? )file.ContentType ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@Private", ((object? )file.Private ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@Purpose", ((object? )file.Purpose ?? DBNull.Value));
                                sqlResult2 = sqlCommand2.ExecuteNonQuery();
                            }
                        }

                        foreach (long fileId in remove)
                        {
                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("DELETE dbo.Files WHERE Id=@fileId AND ListingId=@listing AND OwnerId=@owner AND Private=0", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@fileId", ((object? )fileId ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@owner", ((object? )owner ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }

                            if (sqlResult3 != 1)
                            {
                                throw new RuleException("An image changed while editing. Reload the listing.");
                            }
                        }

                        {
                            long transactionResult = listing;
                            transaction.Commit();
                            return transactionResult;
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

        private void FileLogic_Delete(StoredFile file)
        {
            string? folder;
            if (file.Private)
            {
                folder = Path.Combine(env.ContentRootPath, "App_Data", "private");
            }
            else
            {
                folder = Path.Combine(env.WebRootPath, "uploads");
            }

            string? path = Path.Combine(folder, Path.GetFileName(file.Name));
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
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

        private void WorkflowSql_DeleteListing(long actor, string role, long id, string reason)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [OwnerId], [Kind], [Title], [Description], [Type], [City], [State], [Address], [Price], [Deposit], [Status], [Available], [Reason], [Details], [CreatedAt], [UpdatedAt] FROM dbo.Listings WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id AND OwnerId=@actor", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
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

                                    databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var row = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Listing not found.");
                        if (row.Status == "Reserved" || row.Status == "Sold")
                        {
                            throw new RuleException("Ordered listings cannot be deleted.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"UPDATE dbo.Listings
                SET Status='Removed',Available=0,Reason=@reason
                WHERE Id=@id;INSERT dbo.ModerationHistory(ActorId,Entity,EntityId,Action,Reason)
                VALUES(@actor,'Listing',@id,'Removed',@reason)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@reason", ((object? )reason ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
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

        private void ListingLogic_Availability(long id, ClaimsPrincipal user)
        {
            {
                ListingSql_Availability(CurrentAccount.Id(user), id);
                return;
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
        private void ListingSql_Availability(long owner, long id)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("UPDATE dbo.Listings SET Available=1-Available WHERE Id=@id AND OwnerId=@owner AND Status='Approved'", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@owner", ((object? )owner ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }

            if (sqlResult1 != 1)
            {
                throw new RuleException("Only approved listings can change availability.");
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
