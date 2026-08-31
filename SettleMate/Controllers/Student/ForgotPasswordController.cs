using Microsoft.AspNetCore.Mvc;
using SettleMate.Data.Student;
using SettleMate.Models.Student;

namespace SettleMate.Controllers.Student
{
    // Handles the Forgot Password flow
    [Route("Student/ForgotPassword")]
    public class ForgotPasswordController : Controller
    {
        // Used for database and email operations
        private readonly ForgotPasswordData forgotPasswordData;

        // Create Data Layer object
        public ForgotPasswordController(
            IConfiguration configuration)
        {
            forgotPasswordData =
                new ForgotPasswordData(configuration);
        }

        // Open Forgot Password page
        [HttpGet("")]
        public IActionResult ForgotPassword()
        {
            try
            {
                return View(
                    "~/Views/Student/ForgotPassword.cshtml");
            }
            catch (Exception)
            {
                return View(
                    "~/Views/Shared/Error.cshtml");
            }
        }

        // Validate email and send OTP
        [HttpPost("SendOTP")]
        public IActionResult SendOTP(
            ForgotPasswordModel model)
        {
            try
            {
                // Check model validation
                if (!ModelState.IsValid)
                {
                    return View(
                        "~/Views/Student/ForgotPassword.cshtml",
                        model);
                }

                // Check whether email exists in database
                if (!forgotPasswordData.EmailExists(
                    model.Email))
                {
                    ModelState.AddModelError(
                        "Email",
                        "Email is not registered.");

                    return View(
                        "~/Views/Student/ForgotPassword.cshtml",
                        model);
                }

                // Generate 6-digit OTP
                string otp = Random.Shared
                    .Next(100000, 1000000)
                    .ToString();

                // Store email, OTP and expiry in Session
                HttpContext.Session.SetString(
                    "ForgotPasswordEmail",
                    model.Email);

                HttpContext.Session.SetString(
                    "ForgotPasswordOTP",
                    otp);

                HttpContext.Session.SetString(
                    "ForgotPasswordOTPExpiry",
                    DateTime.Now
                        .AddMinutes(2)
                        .ToString("O"));

                // Send OTP to user's email
                forgotPasswordData.SendOTP(
                    model.Email,
                    otp);

                // Go to OTP verification page
                return RedirectToAction("OTP");
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Could not send OTP. Please try again.");

                return View(
                    "~/Views/Student/ForgotPassword.cshtml",
                    model);
            }
        }

        // Open OTP verification page
        [HttpGet("OTP")]
        public IActionResult OTP()
        {
            try
            {
                // Get email saved during SendOTP
                string email = HttpContext.Session.GetString(
                    "ForgotPasswordEmail");

                // Restart flow if Session expired
                if (email == null)
                {
                    return RedirectToAction(
                        "ForgotPassword");
                }

                // Send email to View
                ViewBag.Email = email;

                return View(
                    "~/Views/Student/ForgotPasswordOTP.cshtml");
            }
            catch (Exception)
            {
                return RedirectToAction(
                    "ForgotPassword");
            }
        }

        // Verify entered OTP
        [HttpPost("VerifyOTP")]
        public IActionResult VerifyOTP(
            ForgotPasswordOTPModel model)
        {
            try
            {
                // Check OTP model validation
                if (!ModelState.IsValid)
                {
                    ViewBag.Email =
                        HttpContext.Session.GetString(
                            "ForgotPasswordEmail");

                    return View(
                        "~/Views/Student/ForgotPasswordOTP.cshtml",
                        model);
                }

                // Get OTP details from Session
                string savedOTP = HttpContext.Session.GetString(
                    "ForgotPasswordOTP");

                string expiryText = HttpContext.Session.GetString(
                    "ForgotPasswordOTPExpiry");

                string email = HttpContext.Session.GetString(
                    "ForgotPasswordEmail");

                // Check whether OTP Session data exists
                if (savedOTP == null ||
                    expiryText == null ||
                    email == null)
                {
                    TempData["Error"] =
                        "OTP session expired. Please try again.";

                    return RedirectToAction(
                        "ForgotPassword");
                }

                // Convert saved expiry time back to DateTime
                DateTime expiry = DateTime.Parse(expiryText);

                // Check whether OTP has expired
                if (DateTime.Now > expiry)
                {
                    ModelState.AddModelError(
                        "OTP",
                        "OTP has expired. Please resend OTP.");

                    ViewBag.Email = email;

                    return View(
                        "~/Views/Student/ForgotPasswordOTP.cshtml",
                        model);
                }

                // Check entered OTP
                if (model.OTP != savedOTP)
                {
                    ModelState.AddModelError(
                        "OTP",
                        "Invalid OTP.");

                    ViewBag.Email = email;

                    return View(
                        "~/Views/Student/ForgotPasswordOTP.cshtml",
                        model);
                }

                // Remember that OTP verification was successful
                HttpContext.Session.SetString(
                    "ForgotPasswordOTPVerified",
                    "true");

                // Move to password reset page
                return RedirectToAction(
                    "ResetPassword");
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Something went wrong. Please try again.");

                ViewBag.Email =
                    HttpContext.Session.GetString(
                        "ForgotPasswordEmail");

                return View(
                    "~/Views/Student/ForgotPasswordOTP.cshtml",
                    model);
            }
        }

        // Open Reset Password page
        [HttpGet("ResetPassword")]
        public IActionResult ResetPassword()
        {
            try
            {
                // Check whether OTP was verified
                string verified =
                    HttpContext.Session.GetString(
                        "ForgotPasswordOTPVerified");

                // Do not allow password reset without OTP verification
                if (verified != "true")
                {
                    return RedirectToAction(
                        "ForgotPassword");
                }

                return View(
                    "~/Views/Student/ResetPassword.cshtml");
            }
            catch (Exception)
            {
                return RedirectToAction(
                    "ForgotPassword");
            }
        }

        // Validate and change the password
        [HttpPost("ChangePassword")]
        public IActionResult ChangePassword(
            ResetPasswordModel model)
        {
            try
            {
                // Check password validation
                if (!ModelState.IsValid)
                {
                    return View(
                        "~/Views/Student/ResetPassword.cshtml",
                        model);
                }

                // Get OTP verification status and email
                string verified =
                    HttpContext.Session.GetString(
                        "ForgotPasswordOTPVerified");

                string email =
                    HttpContext.Session.GetString(
                        "ForgotPasswordEmail");

                // Make sure OTP was verified and Session is valid
                if (verified != "true" || email == null)
                {
                    TempData["Error"] =
                        "Session expired. Please try again.";

                    return RedirectToAction(
                        "ForgotPassword");
                }

                // Update password through Data Layer
                bool result =
                    forgotPasswordData.ChangePassword(
                        email,
                        model.NewPassword);

                if (result)
                {
                    // Remove temporary Forgot Password data
                    HttpContext.Session.Remove(
                        "ForgotPasswordEmail");

                    HttpContext.Session.Remove(
                        "ForgotPasswordOTP");

                    HttpContext.Session.Remove(
                        "ForgotPasswordOTPExpiry");

                    HttpContext.Session.Remove(
                        "ForgotPasswordOTPVerified");

                    // Show success message after redirect
                    TempData["Success"] =
                        "Password changed successfully. Please login.";

                    // Go to Login page
                    return RedirectToAction(
                        "Login",
                        "Login");
                }

                ModelState.AddModelError(
                    "",
                    "Password could not be changed.");

                return View(
                    "~/Views/Student/ResetPassword.cshtml",
                    model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(
                    "",
                    "Something went wrong. Please try again.");

                return View(
                    "~/Views/Student/ResetPassword.cshtml",
                    model);
            }
        }

        // Generate and send a new OTP
        [HttpPost("ResendOTP")]
        public IActionResult ResendOTP()
        {
            try
            {
                // Get email from current Session
                string email =
                    HttpContext.Session.GetString(
                        "ForgotPasswordEmail");

                // Restart if Session has expired
                if (email == null)
                {
                    TempData["Error"] =
                        "Session expired. Please try again.";

                    return RedirectToAction(
                        "ForgotPassword");
                }

                // Generate new 6-digit OTP
                string otp = Random.Shared
                    .Next(100000, 1000000)
                    .ToString();

                // Replace old OTP and expiry
                HttpContext.Session.SetString(
                    "ForgotPasswordOTP",
                    otp);

                HttpContext.Session.SetString(
                    "ForgotPasswordOTPExpiry",
                    DateTime.Now
                        .AddMinutes(2)
                        .ToString("O"));

                // Send new OTP
                forgotPasswordData.SendOTP(
                    email,
                    otp);

                TempData["Success"] =
                    "New OTP sent successfully.";

                // Return to OTP page
                return RedirectToAction("OTP");
            }
            catch (Exception)
            {
                TempData["Error"] =
                    "Could not resend OTP. Please try again.";

                return RedirectToAction("OTP");
            }
        }
    }
}