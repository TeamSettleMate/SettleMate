using Microsoft.AspNetCore.Mvc;
using SettleMate.Data.Student;
using SettleMate.Models.Student;
using System.Text.Json;

namespace SettleMate.Controllers.Student
{
    // Handles Student Registration
    [Route("Student/Register")]
    public class RegisterController : Controller
    {
        // Used for database and email operations
        private readonly RegisterData registerData;

        // Create RegisterData object
        public RegisterController(IConfiguration configuration)
        {
            registerData = new RegisterData(configuration);
        }

        // Open Registration page
        [HttpGet("")]
        public IActionResult Register()
        {
            try
            {
                return View("~/Views/Student/Register.cshtml");
            }
            catch (Exception)
            {
                return View("~/Views/Shared/Error.cshtml");
            }
        }

        // Validate form and send OTP
        [HttpPost("SendOTP")]
        public IActionResult SendOTP(RegisterModel model)
        {
            try
            {
                // Check model validation
                if (!ModelState.IsValid)
                {
                    return View(
                        "~/Views/Student/Register.cshtml",
                        model);
                }

                // Check if email already exists
                if (registerData.EmailExists(model.Email))
                {
                    ModelState.AddModelError(
                        "Email",
                        "Email already exists.");

                    return View(
                        "~/Views/Student/Register.cshtml",
                        model);
                }

                // Generate 6-digit OTP
                string otp = Random.Shared
                    .Next(100000, 1000000)
                    .ToString();

                // Save registration data, OTP and expiry in Session
                HttpContext.Session.SetString(
                    "PendingRegistration",
                    JsonSerializer.Serialize(model));

                HttpContext.Session.SetString(
                    "RegisterOTP",
                    otp);

                HttpContext.Session.SetString(
                    "OTPExpiry",
                    DateTime.Now
                        .AddMinutes(2)
                        .ToString("O"));

                // Send OTP to user's email
                registerData.SendOTP(
                    model.Email,
                    otp);

                // Open OTP section in registration page
                ViewBag.ShowOTP = true;
                ViewBag.OTPEmail = model.Email;

                return View(
                    "~/Views/Student/Register.cshtml",
                    model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Could not send OTP. Please try again.");

                return View(
                    "~/Views/Student/Register.cshtml",
                    model);
            }
        }

        // Verify OTP and complete registration
        [HttpPost("VerifyOTP")]
        public IActionResult VerifyOTP(string otp)
        {
            try
            {
                // Get registration data from Session
                string savedOTP = HttpContext.Session.GetString(
                    "RegisterOTP");

                string expiryText = HttpContext.Session.GetString(
                    "OTPExpiry");

                string registrationText = HttpContext.Session.GetString(
                    "PendingRegistration");

                // Check if OTP session data exists
                if (savedOTP == null ||
                    expiryText == null ||
                    registrationText == null)
                {
                    TempData["Error"] =
                        "OTP session expired. Please register again.";

                    return Redirect("/Student/Register");
                }

                // Convert saved expiry and registration data
                DateTime expiry = DateTime.Parse(expiryText);

                RegisterModel registerModel =
                    JsonSerializer.Deserialize<RegisterModel>(
                        registrationText);

                // Check OTP expiry
                if (DateTime.Now > expiry)
                {
                    ModelState.AddModelError(
                        "",
                        "OTP has expired. Please resend OTP.");

                    ViewBag.ShowOTP = true;
                    ViewBag.OTPEmail = registerModel.Email;

                    return View(
                        "~/Views/Student/Register.cshtml",
                        registerModel);
                }

                // Check OTP format
                if (string.IsNullOrWhiteSpace(otp) ||
                    otp.Length != 6 ||
                    !otp.All(char.IsDigit))
                {
                    ModelState.AddModelError(
                        "",
                        "Please enter a valid 6 digit OTP.");

                    ViewBag.ShowOTP = true;
                    ViewBag.OTPEmail = registerModel.Email;

                    return View(
                        "~/Views/Student/Register.cshtml",
                        registerModel);
                }

                // Check entered OTP
                if (otp != savedOTP)
                {
                    ModelState.AddModelError(
                        "",
                        "Invalid OTP.");

                    ViewBag.ShowOTP = true;
                    ViewBag.OTPEmail = registerModel.Email;

                    return View(
                        "~/Views/Student/Register.cshtml",
                        registerModel);
                }

                // Check email again before database insert
                if (registerData.EmailExists(registerModel.Email))
                {
                    HttpContext.Session.Remove("RegisterOTP");
                    HttpContext.Session.Remove("OTPExpiry");
                    HttpContext.Session.Remove("PendingRegistration");

                    TempData["Error"] =
                        "Email already exists. Please register again.";

                    return Redirect("/Student/Register");
                }

                // Save verified user in database
                bool result = registerData.Register(
                    registerModel);

                if (result)
                {
                    // Remove temporary registration data
                    HttpContext.Session.Remove("RegisterOTP");
                    HttpContext.Session.Remove("OTPExpiry");
                    HttpContext.Session.Remove("PendingRegistration");

                    TempData["Success"] =
                        "Registration successful. Please login.";

                    // Go to Login page
                    return RedirectToAction(
                        "Login",
                        "Login");
                }

                ModelState.AddModelError(
                    "",
                    "Registration failed. Please try again.");

                ViewBag.ShowOTP = true;
                ViewBag.OTPEmail = registerModel.Email;

                return View(
                    "~/Views/Student/Register.cshtml",
                    registerModel);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Something went wrong. Please try again.");

                return View(
                    "~/Views/Student/Register.cshtml");
            }
        }

        // Generate and send a new OTP
        [HttpPost("ResendOTP")]
        public IActionResult ResendOTP()
        {
            try
            {
                // Get saved registration data
                string registrationText = HttpContext.Session.GetString(
                    "PendingRegistration");

                // Restart registration if Session expired
                if (registrationText == null)
                {
                    TempData["Error"] =
                        "Session expired. Please register again.";

                    return Redirect("/Student/Register");
                }

                // Convert saved data back into model
                RegisterModel model =
                    JsonSerializer.Deserialize<RegisterModel>(
                        registrationText);

                // Generate new 6-digit OTP
                string otp = Random.Shared
                    .Next(100000, 1000000)
                    .ToString();

                // Replace old OTP and expiry
                HttpContext.Session.SetString(
                    "RegisterOTP",
                    otp);

                HttpContext.Session.SetString(
                    "OTPExpiry",
                    DateTime.Now
                        .AddMinutes(2)
                        .ToString("O"));

                // Send new OTP
                registerData.SendOTP(
                    model.Email,
                    otp);

                // Keep OTP section open
                ViewBag.ShowOTP = true;
                ViewBag.OTPEmail = model.Email;
                ViewBag.ResendMessage =
                    "New OTP sent successfully.";

                return View(
                    "~/Views/Student/Register.cshtml",
                    model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Could not resend OTP. Please try again.");

                return View(
                    "~/Views/Student/Register.cshtml");
            }
        }
    }
}