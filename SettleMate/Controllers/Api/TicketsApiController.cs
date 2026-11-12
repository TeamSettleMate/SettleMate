using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SettleMate.Web.Controllers.Api
{
    /// <summary>
    /// Full CRUD REST API for support tickets (Practical-11).
    /// POST   /api/tickets              -> create a ticket (anyone)
    /// GET    /api/tickets              -> list tickets (Admin)
    /// GET    /api/tickets/{id}         -> one ticket (Admin)
    /// PUT    /api/tickets/{id}         -> change status Open/Resolved (Admin)
    /// DELETE /api/tickets/{id}         -> delete ticket and its replies (Admin)
    /// Admin endpoints use the normal login cookie, so sign in as Admin in the browser first.
    /// </summary>
    [ApiController]
    [Route("api/tickets")]
    [IgnoreAntiforgeryToken] // JSON API: [ApiController] only accepts JSON bodies, and the cookie is SameSite=Lax.
    public class TicketsApiController : ControllerBase
    {
        private readonly IConfiguration configuration;

        public TicketsApiController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public class TicketDto
        {
            public long Id { get; set; }
            public string FullName { get; set; } = "";
            public string Email { get; set; } = "";
            public string Subject { get; set; } = "";
            public string Message { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime CreatedAt { get; set; }
        }

        // Input for POST. Data Annotations give automatic 400 errors from [ApiController].
        public class CreateTicketInput
        {
            [Required, StringLength(100)]
            public string FullName { get; set; } = "";

            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; } = "";

            [Required, StringLength(150)]
            public string Subject { get; set; } = "";

            [Required, StringLength(2000)]
            public string Message { get; set; } = "";
        }

        // Input for PUT.
        public class UpdateTicketInput
        {
            [Required, RegularExpression("Open|Resolved", ErrorMessage = "Status must be Open or Resolved.")]
            public string Status { get; set; } = "";
        }

        private SqlConnection OpenConnection()
        {
            SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
            connection.Open();
            return connection;
        }

        // GET api/tickets
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public ActionResult<List<TicketDto>> GetAll()
        {
            List<TicketDto> list = new List<TicketDto>();
            using (SqlConnection connection = OpenConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT Id, FullName, Email, Subject, Message, Status, CreatedAt FROM dbo.Tickets ORDER BY CreatedAt DESC", connection))
            using (SqlDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(ReadTicket(reader));
                }
            }
            return Ok(list);
        }

        // GET api/tickets/5
        [HttpGet("{id:long}")]
        [Authorize(Roles = "Admin")]
        public ActionResult<TicketDto> GetById(long id)
        {
            using (SqlConnection connection = OpenConnection())
            using (SqlCommand command = new SqlCommand(
                "SELECT Id, FullName, Email, Subject, Message, Status, CreatedAt FROM dbo.Tickets WHERE Id = @Id", connection))
            {
                command.Parameters.AddWithValue("@Id", id);
                using (SqlDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return Ok(ReadTicket(reader));
                    }
                }
            }
            return NotFound(new { message = "Ticket not found." });
        }

        // POST api/tickets (limited to 5 per minute per IP, same as the Contact form)
        [HttpPost]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("contact")]
        public ActionResult<TicketDto> Create(CreateTicketInput input)
        {
            long newId;
            using (SqlConnection connection = OpenConnection())
            using (SqlCommand command = new SqlCommand(
                "INSERT INTO dbo.Tickets (FullName, Email, Subject, Message) OUTPUT INSERTED.Id " +
                "VALUES (@FullName, @Email, @Subject, @Message)", connection))
            {
                command.Parameters.AddWithValue("@FullName", input.FullName.Trim());
                command.Parameters.AddWithValue("@Email", input.Email.Trim());
                command.Parameters.AddWithValue("@Subject", input.Subject.Trim());
                command.Parameters.AddWithValue("@Message", input.Message.Trim());
                newId = (long)command.ExecuteScalar()!;
            }
            // 201 Created + Location header pointing at GET api/tickets/{id}
            return CreatedAtAction(nameof(GetById), new { id = newId }, new { id = newId, status = "Open" });
        }

        // PUT api/tickets/5
        [HttpPut("{id:long}")]
        [Authorize(Roles = "Admin")]
        public IActionResult UpdateStatus(long id, UpdateTicketInput input)
        {
            int rows;
            using (SqlConnection connection = OpenConnection())
            using (SqlCommand command = new SqlCommand("UPDATE dbo.Tickets SET Status = @Status WHERE Id = @Id", connection))
            {
                command.Parameters.AddWithValue("@Status", input.Status);
                command.Parameters.AddWithValue("@Id", id);
                rows = command.ExecuteNonQuery();
            }
            if (rows == 0)
            {
                return NotFound(new { message = "Ticket not found." });
            }
            return NoContent();
        }

        // DELETE api/tickets/5
        [HttpDelete("{id:long}")]
        [Authorize(Roles = "Admin")]
        public IActionResult Delete(long id)
        {
            int rows;
            using (SqlConnection connection = OpenConnection())
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                // Replies reference the ticket, so delete them first.
                using (SqlCommand replies = new SqlCommand("DELETE FROM dbo.TicketReplies WHERE TicketId = @Id", connection, transaction))
                {
                    replies.Parameters.AddWithValue("@Id", id);
                    replies.ExecuteNonQuery();
                }
                using (SqlCommand ticket = new SqlCommand("DELETE FROM dbo.Tickets WHERE Id = @Id", connection, transaction))
                {
                    ticket.Parameters.AddWithValue("@Id", id);
                    rows = ticket.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            if (rows == 0)
            {
                return NotFound(new { message = "Ticket not found." });
            }
            return NoContent();
        }

        private static TicketDto ReadTicket(SqlDataReader reader)
        {
            return new TicketDto
            {
                Id = reader.GetInt64(0),
                FullName = reader.GetString(1),
                Email = reader.GetString(2),
                Subject = reader.GetString(3),
                Message = reader.GetString(4),
                Status = reader.GetString(5),
                CreatedAt = reader.GetDateTime(6)
            };
        }
    }
}
