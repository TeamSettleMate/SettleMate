using Microsoft.AspNetCore.Mvc;
using SettleMate.Data.Student;
using SettleMate.Models.Student;

namespace SettleMate.Controllers.Student
{
    [Route("Student/Roommate")]
    public class RoommateController : Controller
    {
        private readonly RoommateData roommateData;


        // =========================================================
        // CONSTRUCTOR
        // =========================================================

        public RoommateController(
            IConfiguration configuration)
        {
            roommateData =
                new RoommateData(configuration);
        }


        // =========================================================
        // CHECK STUDENT LOGIN
        // =========================================================

        private int? GetStudentID()
        {
            return HttpContext.Session.GetInt32(
                "UserID");
        }


        // =========================================================
        // 6.1.1 ROOMMATE LISTINGS
        // GET: /Student/Roommate
        // =========================================================

        [HttpGet("")]
        public IActionResult Listings(
            string? location,
            string? gender,
            decimal? minBudget,
            decimal? maxBudget)
        {
            try
            {
                if (GetStudentID() == null)
                {
                    return Redirect("/Student/Login");
                }


                List<RoommateListingModel> listings =
                    roommateData.GetListings(
                        location,
                        gender,
                        minBudget,
                        maxBudget);


                RoommateListingModel filterModel =
                    new RoommateListingModel
                    {
                        FilterLocation =
                            location ?? "",

                        FilterGender =
                            gender ?? "",

                        MinBudget =
                            minBudget,

                        MaxBudget =
                            maxBudget
                    };


                ViewBag.Listings = listings;


                return View(
                    "~/Views/Student/RoommateListing.cshtml",
                    filterModel);
            }
            catch (Exception)
            {
                return View(
                    "~/Views/Shared/Error.cshtml");
            }
        }


        // =========================================================
        // 6.1.3 ROOMMATE DETAILS
        // GET: /Student/Roommate/Details/5
        // =========================================================

        [HttpGet("Details/{listingID:int}")]
        public IActionResult Details(
            int listingID)
        {
            try
            {
                if (GetStudentID() == null)
                {
                    return Redirect("/Student/Login");
                }


                RoommateListingModel? listing =
                    roommateData.GetListing(
                        listingID);


                if (listing == null)
                {
                    TempData["Error"] =
                        "Roommate listing not found.";

                    return Redirect(
                        "/Student/Roommate");
                }


                return View(
                    "~/Views/Student/RoommateDetails.cshtml",
                    listing);
            }
            catch (Exception)
            {
                return View(
                    "~/Views/Shared/Error.cshtml");
            }
        }


        // =========================================================
        // 6.2.1 MY LISTING
        // GET: /Student/Roommate/MyListing
        // =========================================================

        [HttpGet("MyListing")]
        public IActionResult MyListing()
        {
            try
            {
                int? studentID =
                    GetStudentID();


                if (studentID == null)
                {
                    return Redirect("/Student/Login");
                }


                RoommateListingModel? listing =
                    roommateData.GetMyListing(
                        studentID.Value);


                return View(
                    "~/Views/Student/MyRoommateListing.cshtml",
                    listing);
            }
            catch (Exception)
            {
                return View(
                    "~/Views/Shared/Error.cshtml");
            }
        }


        // =========================================================
        // 6.2.2 ADD LISTING
        // GET
        // =========================================================

        [HttpGet("Add")]
        public IActionResult Add()
        {
            try
            {
                int? studentID =
                    GetStudentID();


                if (studentID == null)
                {
                    return Redirect("/Student/Login");
                }


                // One student can have only one listing
                RoommateListingModel? existingListing =
                    roommateData.GetMyListing(
                        studentID.Value);


                if (existingListing != null)
                {
                    TempData["Error"] =
                        "You already have a roommate listing.";

                    return Redirect(
                        "/Student/Roommate/MyListing");
                }


                return View(
                    "~/Views/Student/AddRoommateListing.cshtml",
                    new RoommateListingModel());
            }
            catch (Exception)
            {
                return View(
                    "~/Views/Shared/Error.cshtml");
            }
        }


        // =========================================================
        // 6.2.2 ADD LISTING
        // POST
        // =========================================================

        [HttpPost("Add")]
        [ValidateAntiForgeryToken]
        public IActionResult Add(
            RoommateListingModel model)
        {
            try
            {
                int? studentID =
                    GetStudentID();


                if (studentID == null)
                {
                    return Redirect("/Student/Login");
                }


                // Check duplicate listing
                RoommateListingModel? existingListing =
                    roommateData.GetMyListing(
                        studentID.Value);


                if (existingListing != null)
                {
                    TempData["Error"] =
                        "You already have a roommate listing.";

                    return Redirect(
                        "/Student/Roommate/MyListing");
                }


                if (!ModelState.IsValid)
                {
                    return View(
                        "~/Views/Student/AddRoommateListing.cshtml",
                        model);
                }


                model.StudentID =
                    studentID.Value;


                // Use student's profile avatar
                // if listing photo is not provided.
                if (string.IsNullOrWhiteSpace(
                    model.ProfilePhoto))
                {
                    model.ProfilePhoto =
                        "images/Student/female_01.png";
                }


                bool success =
                    roommateData.AddListing(
                        model);


                if (!success)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to create roommate listing.");

                    return View(
                        "~/Views/Student/AddRoommateListing.cshtml",
                        model);
                }


                TempData["Success"] =
                    "Roommate listing created successfully.";


                return Redirect(
                    "/Student/Roommate/MyListing");
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Something went wrong. Please try again.");


                return View(
                    "~/Views/Student/AddRoommateListing.cshtml",
                    model);
            }
        }


        // =========================================================
        // 6.2.3 EDIT LISTING
        // GET
        // =========================================================

        [HttpGet("Edit/{listingID:int}")]
        public IActionResult Edit(
            int listingID)
        {
            try
            {
                int? studentID =
                    GetStudentID();


                if (studentID == null)
                {
                    return Redirect("/Student/Login");
                }


                RoommateListingModel? listing =
                    roommateData.GetListing(
                        listingID);


                if (listing == null ||
                    listing.StudentID != studentID.Value)
                {
                    TempData["Error"] =
                        "You are not allowed to edit this listing.";

                    return Redirect(
                        "/Student/Roommate/MyListing");
                }


                return View(
                    "~/Views/Student/EditRoommateListing.cshtml",
                    listing);
            }
            catch (Exception)
            {
                return View(
                    "~/Views/Shared/Error.cshtml");
            }
        }


        // =========================================================
        // 6.2.3 EDIT LISTING
        // POST
        // =========================================================

        [HttpPost("Edit")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(
            RoommateListingModel model)
        {
            try
            {
                int? studentID =
                    GetStudentID();


                if (studentID == null)
                {
                    return Redirect("/Student/Login");
                }


                if (!ModelState.IsValid)
                {
                    return View(
                        "~/Views/Student/EditRoommateListing.cshtml",
                        model);
                }


                RoommateListingModel? existingListing =
                    roommateData.GetListing(
                        model.ListingID);


                if (existingListing == null ||
                    existingListing.StudentID != studentID.Value)
                {
                    TempData["Error"] =
                        "You are not allowed to edit this listing.";

                    return Redirect(
                        "/Student/Roommate/MyListing");
                }


                bool success =
                    roommateData.UpdateListing(
                        model,
                        studentID.Value);


                if (!success)
                {
                    ModelState.AddModelError(
                        "",
                        "Unable to update roommate listing.");

                    return View(
                        "~/Views/Student/EditRoommateListing.cshtml",
                        model);
                }


                TempData["Success"] =
                    "Roommate listing updated successfully.";


                return Redirect(
                    "/Student/Roommate/MyListing");
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Something went wrong. Please try again.");


                return View(
                    "~/Views/Student/EditRoommateListing.cshtml",
                    model);
            }
        }


        // =========================================================
        // 6.2.4 DELETE LISTING
        // POST
        // =========================================================

        [HttpPost("Delete/{listingID:int}")]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(
            int listingID)
        {
            try
            {
                int? studentID =
                    GetStudentID();


                if (studentID == null)
                {
                    return Redirect("/Student/Login");
                }


                bool success =
                    roommateData.DeleteListing(
                        listingID,
                        studentID.Value);


                if (success)
                {
                    TempData["Success"] =
                        "Roommate listing deleted successfully.";
                }
                else
                {
                    TempData["Error"] =
                        "Unable to delete roommate listing.";
                }


                return Redirect(
                    "/Student/Roommate/MyListing");
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Something went wrong. Please try again.";


                return Redirect(
                    "/Student/Roommate/MyListing");
            }
        }
    }
}