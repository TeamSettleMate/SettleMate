using Microsoft.Data.SqlClient;
using SettleMate.Models.Student;
using System.Net;
using System.Net.Mail;

namespace SettleMate.Data.Student
{
    // Handles database and email operations for Forgot Password
    public class ForgotPasswordData
    {
        private readonly string connectionString;
        private readonly IConfiguration configuration;

        // Get database connection and email settings
        public ForgotPasswordData(
            IConfiguration configuration)
        {
            this.configuration = configuration;

            connectionString =
                configuration.GetConnectionString(
                    "DefaultConnection");
        }

        // Check if email exists in database
        public bool EmailExists(string email)
        {
            try
            {
                // Create database connection
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                // Search email in Users table
                string query =
                    "SELECT COUNT(*) FROM Users WHERE Email = @Email";

                using SqlCommand command =
                    new SqlCommand(query, connection);

                // Add email safely to SQL query
                command.Parameters.AddWithValue(
                    "@Email",
                    email);

                connection.Open();

                // Get number of matching records
                int count = Convert.ToInt32(
                    command.ExecuteScalar());

                return count > 0;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Send OTP to user's email
        public bool SendOTP(string email, string otp)
        {
            try
            {
                // Get email settings from appsettings.json
                string senderEmail =
                    configuration["EmailSettings:Email"];

                string senderPassword =
                    configuration["EmailSettings:Password"];

                string smtpServer =
                    configuration["EmailSettings:SmtpServer"];

                int port = Convert.ToInt32(
                    configuration["EmailSettings:Port"]);

                // Create email
                using MailMessage message =
                    new MailMessage();

                message.From =
                    new MailAddress(senderEmail);

                message.To.Add(email);

                message.Subject =
                    "SettleMate Password Reset OTP";

                message.Body =
                    "Hello,\n\n" +
                    "Your SettleMate password reset OTP is: " +
                    otp +
                    "\n\n" +
                    "This OTP is valid for 2 minutes.\n\n" +
                    "Thank you,\n" +
                    "SettleMate";

                // Configure SMTP server
                using SmtpClient smtp =
                    new SmtpClient(smtpServer, port);

                smtp.Credentials =
                    new NetworkCredential(
                        senderEmail,
                        senderPassword);

                smtp.EnableSsl = true;

                // Send email
                smtp.Send(message);

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Update user's password
        public bool ChangePassword(
            string email,
            string newPassword)
        {
            try
            {
                // Create database connection
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                // Update password for the given email
                string query = @"
                    UPDATE Users
                    SET Password = @Password
                    WHERE Email = @Email";

                using SqlCommand command =
                    new SqlCommand(query, connection);

                // Hash password before saving
                command.Parameters.AddWithValue(
                    "@Password",
                    PasswordHelper.HashPassword(
                        newPassword));

                // Add email parameter
                command.Parameters.AddWithValue(
                    "@Email",
                    email);

                connection.Open();

                // Execute update query
                int result =
                    command.ExecuteNonQuery();

                return result > 0;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}