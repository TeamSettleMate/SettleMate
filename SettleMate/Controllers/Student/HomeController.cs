using Microsoft.AspNetCore.Mvc;

namespace SettleMate.Controllers.Student
{
    // Handles Student Home and Logout
    [Route("Student/Home")]
    public class HomeController : Controller
    {
        // Open Student Home page
        [HttpGet("")]
        public IActionResult Home()
        {
            try
            {
                // Get logged-in user's ID from Session
                int? userID =
                    HttpContext.Session.GetInt32("UserID");

                // If user is not logged in, go to Login
                if (userID == null)
                {
                    return Redirect("/Student/Login");
                }

                // Show Student Home page
                return View("~/Views/Student/Home.cshtml");
            }
            catch (Exception)
            {
                // Show error page if something goes wrong
                return View("~/Views/Shared/Error.cshtml");
            }
        }

        // Logout the student
        [HttpGet("Logout")]
        public IActionResult Logout()
        {
            try
            {
                // Clear login session
                HttpContext.Session.Clear();

                // Go back to Login page
                return Redirect("/Student/Login");
            }
            catch (Exception)
            {
                return View("~/Views/Shared/Error.cshtml");
            }
        }
    }
}