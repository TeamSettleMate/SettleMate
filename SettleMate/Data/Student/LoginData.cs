using Microsoft.Data.SqlClient;
using SettleMate.Models.Student;

namespace SettleMate.Data.Student
{
    // Handles Student Login database operations
    public class LoginData
    {
        private readonly string connectionString;

        // Get database connection string
        public LoginData(IConfiguration configuration)
        {
            connectionString =
                configuration.GetConnectionString(
                    "DefaultConnection");
        }

        // Check student login details
        public int Login(LoginModel model)
        {
            try
            {
                // Create database connection
                using SqlConnection connection =
                    new SqlConnection(connectionString);

                // Find active Student by email and password
                string query = @"
                    SELECT UserID
                    FROM Users
                    WHERE Email = @Email
                    AND Password = @Password
                    AND Role = 'Student'
                    AND IsActive = 1";

                using SqlCommand command =
                    new SqlCommand(query, connection);

                // Add email parameter
                command.Parameters.AddWithValue(
                    "@Email",
                    model.Email);

                // Hash password before checking
                command.Parameters.AddWithValue(
                    "@Password",
                    PasswordHelper.HashPassword(
                        model.Password));

                connection.Open();

                // Get matching UserID
                object result =
                    command.ExecuteScalar();

                // Return UserID if login is valid
                if (result != null)
                {
                    return Convert.ToInt32(result);
                }

                // Return 0 if login fails
                return 0;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}