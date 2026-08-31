using Microsoft.Data.SqlClient;
using SettleMate.Models.Student;
using System.Net;
using System.Net.Mail;

namespace SettleMate.Data.Student
{
    // Handles Student Registration database and email operations
    public class RegisterData
    {
        private readonly string connectionString;
        private readonly IConfiguration configuration;

        // Get database connection and email settings
        public RegisterData(IConfiguration configuration)
        {
            this.configuration = configuration;

            connectionString =
                configuration.GetConnectionString(
                    "DefaultConnection");
        }

        // Send OTP to user's email
        public bool SendOTP(string email, string otp)
        {
            try
            {
                // Get SMTP settings from appsettings.json
                string senderEmail =
                    configuration["EmailSettings:Email"];

                string senderPassword =
                    configuration["EmailSettings:Password"];

                string smtpServer =
                    configuration["EmailSettings:SmtpServer"];

                int port = Convert.ToInt32(
                    configuration["EmailSettings:Port"]);

                // Create email message
                using MailMessage message =
                    new MailMessage();

                message.From =
                    new MailAddress(senderEmail);

                message.To.Add(email);

                message.Subject =
                    "SettleMate Email Verification";

                message.Body =
                    "Hello,\n\n" +
                    "Your SettleMate verification OTP is: " +
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

                // Send OTP email
                smtp.Send(message);

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Check if email already exists
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

                // Add email parameter
                command.Parameters.AddWithValue(
                    "@Email",
                    email);

                connection.Open();

                // Get matching email count
                int count = Convert.ToInt32(
                    command.ExecuteScalar());

                return count > 0;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Insert verified student into database
        public bool Register(RegisterModel model)
        {
            try
            {
                // Create database connection
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                // Insert student into Users table
                string query = @"
                    INSERT INTO Users
                    (
                        FullName,
                        Email,
                        MobileNumber,
                        Gender,
                        City,
                        Address,
                        ProfileAvatar,
                        Password,
                        Role,
                        IsActive,
                        CreatedDate
                    )
                    VALUES
                    (
                        @FullName,
                        @Email,
                        @MobileNumber,
                        @Gender,
                        @City,
                        @Address,
                        @ProfileAvatar,
                        @Password,
                        @Role,
                        @IsActive,
                        @CreatedDate
                    )";

                using SqlCommand command =
                    new SqlCommand(query, connection);

                // Add student details
                command.Parameters.AddWithValue(
                    "@FullName",
                    model.Name);

                command.Parameters.AddWithValue(
                    "@Email",
                    model.Email);

                command.Parameters.AddWithValue(
                    "@MobileNumber",
                    model.Phone);

                command.Parameters.AddWithValue(
                    "@Gender",
                    model.Gender);

                command.Parameters.AddWithValue(
                    "@City",
                    model.City);

                command.Parameters.AddWithValue(
                    "@Address",
                    model.Address);

                // Save profile photo or NULL
                command.Parameters.AddWithValue(
                    "@ProfileAvatar",
                    (object?)model.ProfilePhoto ?? DBNull.Value);

                // Hash password before saving
                command.Parameters.AddWithValue(
                    "@Password",
                    PasswordHelper.HashPassword(
                        model.Password));

                // Set default student values
                command.Parameters.AddWithValue(
                    "@Role",
                    "Student");

                command.Parameters.AddWithValue(
                    "@IsActive",
                    true);

                command.Parameters.AddWithValue(
                    "@CreatedDate",
                    DateTime.Now);

                connection.Open();

                // Execute INSERT query
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