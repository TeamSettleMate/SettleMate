using Microsoft.AspNetCore.Mvc;
using SettleMate.Data.Student;
using SettleMate.Models.Student;

namespace SettleMate.Controllers.Student
{
    // Handles Student Login
    [Route("Student/Login")]
    public class LoginController : Controller
    {
        // Used for database login operation
        private readonly LoginData loginData;

        // Create LoginData object
        public LoginController(IConfiguration configuration)
        {
            loginData = new LoginData(configuration);
        }

        // Open Login page
        [HttpGet("")]
        public IActionResult Login()
        {
            try
            {
                // Check if user is already logged in
                if (HttpContext.Session.GetInt32("UserID") != null)
                {
                    return Redirect("/Student/Home");
                }

                // Show Login page
                return View("~/Views/Student/Login.cshtml");
            }
            catch (Exception)
            {
                return View("~/Views/Shared/Error.cshtml");
            }
        }

        // Process Login form
        [HttpPost("")]
        public IActionResult Login(LoginModel model)
        {
            try
            {
                // Prevent already logged-in user from logging in again
                if (HttpContext.Session.GetInt32("UserID") != null)
                {
                    return Redirect("/Student/Home");
                }

                // Check model validation
                if (!ModelState.IsValid)
                {
                    return View(
                        "~/Views/Student/Login.cshtml",
                        model);
                }

                // Check email and password in database
                int userID = loginData.Login(model);

                // Login successful
                if (userID > 0)
                {
                    // Store user ID in Session
                    HttpContext.Session.SetInt32(
                        "UserID",
                        userID);

                    // Store user email in Session
                    HttpContext.Session.SetString(
                        "UserEmail",
                        model.Email);

                    // Show success message
                    TempData["Success"] =
                        "Login successful.";

                    // Go to Student Home
                    return Redirect("/Student/Home");
                }

                // Login failed
                ModelState.AddModelError(
                    "",
                    "Invalid email or password.");

                return View(
                    "~/Views/Student/Login.cshtml",
                    model);
            }
            catch (Exception)
            {
                // Handle unexpected errors
                ModelState.AddModelError(
                    "",
                    "Something went wrong. Please try again.");

                return View(
                    "~/Views/Student/Login.cshtml",
                    model);
            }
        }
    }
}