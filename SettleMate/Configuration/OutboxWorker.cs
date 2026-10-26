using Microsoft.Data.SqlClient;
using SettleMate.Web.Models;
using System.Net;
using System.Net.Mail;

namespace SettleMate.Web.Configuration
{
    // Background scheduling uses the framework's async contract; SQL and SMTP calls are synchronous.
    public class OutboxWorker : BackgroundService
    {
        private readonly IConfiguration configuration;
        private readonly ILogger<OutboxWorker> logger;
        public OutboxWorker(IConfiguration configuration, ILogger<OutboxWorker> logger)
        {
            this.configuration = configuration;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (EmailOutbox message in ClaimMessages())
                    {
                        if (stoppingToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            SendEmail(message);
                            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
                            {
                                connection.Open();
                                using (SqlCommand command = new SqlCommand("UPDATE dbo.EmailOutbox SET SentAt=SYSUTCDATETIME() WHERE Id=@Id", connection))
                                {
                                    command.Parameters.AddWithValue("@Id", message.Id);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }
                        catch (Exception)
                        {
                            logger.LogWarning("Email {Id} will retry.", message.Id);
                        }
                    }
                }
                catch (Exception)
                {
                    logger.LogWarning("Email worker is waiting for database and email configuration.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private List<EmailOutbox> ClaimMessages()
        {
            List<EmailOutbox> messages = new List<EmailOutbox>();
            string query = @";WITH batch AS
                (SELECT TOP (10) Id,Recipient,Subject,Body,Attempts,NextAttemptAt
                FROM dbo.EmailOutbox WITH(UPDLOCK,READPAST,ROWLOCK)
                WHERE SentAt IS NULL AND Attempts<5 AND NextAttemptAt<=SYSUTCDATETIME() ORDER BY Id)
                UPDATE batch SET Attempts=Attempts+1,NextAttemptAt=DATEADD(minute,5,SYSUTCDATETIME())
                OUTPUT INSERTED.Id,INSERTED.Recipient,INSERTED.Subject,INSERTED.Body";
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(query, connection))
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        EmailOutbox message = new EmailOutbox();
                        message.Id = reader.GetInt64(0);
                        message.Recipient = reader.GetString(1);
                        message.Subject = reader.GetString(2);
                        message.Body = reader.GetString(3);
                        messages.Add(message);
                    }
                }
            }

            return messages;
        }

        private void SendEmail(EmailOutbox message)
        {
            string from = configuration["EmailSettings:Email"] ?? "";
            string password = configuration["EmailSettings:Password"] ?? "";
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Configure email in user secrets.");
            }

            using (SmtpClient client = new SmtpClient(configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com", configuration.GetValue<int>("EmailSettings:Port", 587)))
            {
                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(from, password.Replace(" ", ""));
                client.Timeout = 20000;
                using (MailMessage mail = new MailMessage(from, message.Recipient, message.Subject, message.Body))
                {
                    mail.IsBodyHtml = false;
                    client.Send(mail);
                }
            }
        }
    }
}
