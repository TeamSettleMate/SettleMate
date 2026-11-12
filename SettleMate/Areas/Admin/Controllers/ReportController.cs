using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SettleMate.Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin report page. Shows ADO.NET DISCONNECTED architecture (Unit 4):
    /// SqlDataAdapter fills a DataSet with two DataTables, the connection closes,
    /// and the view reads the tables from memory.
    /// (The rest of the project uses the CONNECTED style: SqlCommand + SqlDataReader.)
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReportController : Controller
    {
        private readonly IConfiguration configuration;

        public ReportController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        // GET /Admin/Report?kind=Property
        [HttpGet]
        public IActionResult Index(string kind = "All")
        {
            DataSet report = new DataSet("Report");

            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                // Table 1: listings grouped by kind and status (optionally filtered by kind).
                string listingSql = "SELECT Kind, Status, COUNT(*) AS Total, ISNULL(AVG(Price), 0) AS [Average price] " +
                                    "FROM dbo.Listings WHERE (@Kind = 'All' OR Kind = @Kind) " +
                                    "GROUP BY Kind, Status ORDER BY Kind, Status";
                SqlDataAdapter listingAdapter = new SqlDataAdapter(listingSql, connection);
                listingAdapter.SelectCommand.Parameters.AddWithValue("@Kind", kind);
                listingAdapter.Fill(report, "Listings");

                // Table 2: accounts grouped by role.
                SqlDataAdapter accountAdapter = new SqlDataAdapter(
                    "SELECT Role, COUNT(*) AS Total FROM dbo.Accounts GROUP BY Role ORDER BY Role", connection);
                accountAdapter.Fill(report, "Accounts");
                // Fill() opens and closes the connection by itself.
            }

            // Work with the data offline: add a calculated column to the DataTable.
            DataTable listings = report.Tables["Listings"]!;
            listings.Columns.Add("Share", typeof(string));
            int grandTotal = 0;
            foreach (DataRow row in listings.Rows)
            {
                grandTotal += Convert.ToInt32(row["Total"]);
            }
            foreach (DataRow row in listings.Rows)
            {
                int total = Convert.ToInt32(row["Total"]);
                row["Share"] = grandTotal == 0 ? "0%" : (total * 100 / grandTotal) + "%";
            }

            ViewData["Title"] = "Reports";
            ViewData["Kind"] = kind;
            ViewData["GrandTotal"] = grandTotal;
            return View(report);
        }
    }
}
