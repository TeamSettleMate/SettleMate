using Microsoft.AspNetCore.Mvc;

namespace SettleMate.Web.Controllers.Api
{
    /// <summary>
    /// Read-only REST API for approved listings (Practical-11).
    /// GET /api/listings            -> all approved listings (optional ?city=Ahmedabad)
    /// GET /api/listings/{id}       -> one approved listing
    /// Responses are JSON. No login is needed because the same data is public on the website.
    /// </summary>
    [ApiController]
    [Route("api/listings")]
    public class ListingsApiController : ControllerBase
    {
        private readonly IConfiguration configuration;

        // Dependency Injection: ASP.NET Core passes IConfiguration in automatically.
        public ListingsApiController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        // Small output class so the API returns only safe, public fields.
        public class ListingDto
        {
            public long Id { get; set; }
            public string Kind { get; set; } = "";
            public string Title { get; set; } = "";
            public string Type { get; set; } = "";
            public string City { get; set; } = "";
            public string State { get; set; } = "";
            public decimal Price { get; set; }
            public decimal Deposit { get; set; }
            public bool Available { get; set; }
        }

        private const string SelectApproved =
            "SELECT l.Id, l.Kind, l.Title, l.Type, l.City, l.State, l.Price, l.Deposit, l.Available " +
            "FROM dbo.Listings l JOIN dbo.Accounts a ON a.Id = l.OwnerId " +
            "WHERE l.Status = 'Approved' AND a.Status IN ('Active', 'Approved')";

        // GET api/listings?city=Surat
        [HttpGet]
        public ActionResult<List<ListingDto>> GetAll(string? city)
        {
            List<ListingDto> list = new List<ListingDto>();
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                string sql = SelectApproved;
                if (!string.IsNullOrWhiteSpace(city))
                {
                    sql += " AND l.City = @City";
                }
                sql += " ORDER BY l.CreatedAt DESC";

                using (SqlCommand command = new SqlCommand(sql, connection))
                {
                    if (!string.IsNullOrWhiteSpace(city))
                    {
                        command.Parameters.AddWithValue("@City", city.Trim());
                    }
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(ReadListing(reader));
                        }
                    }
                }
            }
            return Ok(list);
        }

        // GET api/listings/5
        [HttpGet("{id:long}")]
        public ActionResult<ListingDto> GetById(long id)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand(SelectApproved + " AND l.Id = @Id", connection))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return Ok(ReadListing(reader));
                        }
                    }
                }
            }
            return NotFound(new { message = "Listing not found." });
        }

        private static ListingDto ReadListing(SqlDataReader reader)
        {
            return new ListingDto
            {
                Id = reader.GetInt64(0),
                Kind = reader.GetString(1),
                Title = reader.GetString(2),
                Type = reader.GetString(3),
                City = reader.GetString(4),
                State = reader.GetString(5),
                Price = reader.GetDecimal(6),
                Deposit = reader.GetDecimal(7),
                Available = reader.GetBoolean(8)
            };
        }
    }
}
