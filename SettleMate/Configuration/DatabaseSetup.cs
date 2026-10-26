using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SettleMate.Web.Configuration
{
    public static class DatabaseSetup
    {
        public static void Run(string operation, IConfiguration configuration, string contentRoot)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                if (operation == "--initialize-database")
                {
                    string script = File.ReadAllText(Path.Combine(contentRoot, "Database", "01_schema.sql"));
                    using (SqlCommand command = new SqlCommand(script, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    Console.WriteLine("SettleMate schema installed; existing data preserved.");
                }
                else if (operation == "--check-database")
                {
                    string[] tables =
                    {
                        "Accounts",
                        "OtpChallenges",
                        "Listings",
                        "Files",
                        "Wishlists",
                        "VisitRequests",
                        "Reviews",
                        "ReviewReports",
                        "CartItems",
                        "Orders",
                        "OrderItems",
                        "OrderHistory",
                        "Tickets",
                        "TicketReplies",
                        "Conversations",
                        "Messages",
                        "Notifications",
                        "ModerationHistory",
                        "PropertyViews",
                        "EmailOutbox"
                    };
                    foreach (string table in tables)
                    {
                        using (SqlCommand command = new SqlCommand("SELECT OBJECT_ID(@Table,'U')", connection))
                        {
                            command.Parameters.AddWithValue("@Table", "dbo." + table);
                            if (command.ExecuteScalar() == DBNull.Value)
                            {
                                Console.Error.WriteLine("Missing table: " + table);
                                Environment.ExitCode = 1;
                            }
                        }
                    }

                    if (Environment.ExitCode == 0)
                    {
                        Console.WriteLine("All 20 required tables are present.");
                    }
                }
                else if (operation == "--seed-admin")
                {
                    string email = configuration["AdminSeed:Email"] ?? "";
                    string password = configuration["AdminSeed:Password"] ?? "";
                    if (!new EmailAddressAttribute().IsValid(email) || password.Length < 8 || password.Length > 128 || !Regex.IsMatch(password, @"\d") || !Regex.IsMatch(password, @"[^\w\s]"))
                    {
                        throw new InvalidOperationException("Configure valid AdminSeed email and password in user secrets.");
                    }

                    byte[] salt = RandomNumberGenerator.GetBytes(16);
                    byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
                    string stored = "210000." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
                    using (SqlTransaction transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                    {
                        using (SqlCommand check = new SqlCommand("SELECT COUNT(*) FROM dbo.Accounts WITH(UPDLOCK,HOLDLOCK) WHERE NormalizedEmail=UPPER(@Email)", connection, transaction))
                        {
                            check.Parameters.AddWithValue("@Email", email);
                            if (Convert.ToInt32(check.ExecuteScalar()) != 0)
                            {
                                throw new InvalidOperationException("Email already belongs to an account; existing accounts are not promoted.");
                            }
                        }

                        using (SqlCommand command = new SqlCommand("INSERT dbo.Accounts(FullName,Email,Phone,PasswordHash,Role,Status,City,State) VALUES(@Name,@Email,'0000000000',@Hash,'Admin','Active','Rajkot','Gujarat')", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Name", configuration["AdminSeed:FullName"] ?? "Admin");
                            command.Parameters.AddWithValue("@Email", email);
                            command.Parameters.AddWithValue("@Hash", stored);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }

                    Console.WriteLine("Admin account created.");
                }
            }
        }
    }
}
