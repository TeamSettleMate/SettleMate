using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SettleMate.Web.Common;
using SettleMate.Web.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace SettleMate.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class OrderController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public OrderController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index(bool received = false, int page = 1, string search = "", string status = "")
        {
            return View(WorkspaceQueryLogic_Records(User, "Orders", page, search, status, received));
        }

        [HttpGet]
        public IActionResult Details(long id)
        {
            var model = WorkspaceQueryLogic_Order(User, id);
            if (model == null)
            {
                return NotFound();
            }
            else
            {
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult UpdateStatus(OrderStatus model)
        {
            ValidateForm();
            WorkflowLogic_UpdateOrder(User, model.Id, value: model.Value);
            return Success(model);
        }

        private List<OrderItem> WorkspaceQueryLogic_Records(ClaimsPrincipal user, string section, int page, string search, string status, bool received = false)
        {
            if (CurrentAccount.Role(user) != "User" && CurrentAccount.Role(user) != "Admin" || section == "Payments" && CurrentAccount.Role(user) != "User")
            {
                throw new RuleException("You do not have access to orders.");
            }

            List<OrderItem> rows = WorkflowSql_Orders(CurrentAccount.Id(user), CurrentAccount.Role(user), received || section == "Payments");
            List<OrderItem> matches = new List<OrderItem>();
            foreach (OrderItem row in rows)
            {
                string text = string.Join(" ", new object? [] { row.Id, row.OrderId, row.ListingId, row.SellerId, row.Title, row.ListingType, row.UnitPrice, row.Deposit, row.Months, row.Amount, row.Status, row.Collected, row.CreatedAt, row.ProcessedAt, row.CompletedAt, row.BuyerId, row.OrderDate, row.BuyerName, row.SellerName, row.Address, row.City, row.State, row.Pincode, row.Phone, row.BuyerEmail });
                if (!string.IsNullOrWhiteSpace(search) && !text.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(status) && row.Status != status)
                {
                    continue;
                }

                matches.Add(row);
            }

            page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(matches.Count / 10.0)));
            ViewData["Title"] = section == "Payments" ? "COD payments" : received ? "Orders received" : "My orders";
            ViewData["Section"] = section;
            ViewData["Total"] = matches.Count;
            ViewData["Page"] = page;
            int start = (page - 1) * 10;
            return matches.GetRange(start, Math.Min(10, matches.Count - start));
        }

        private List<OrderItem>? WorkspaceQueryLogic_Order(ClaimsPrincipal user, long id)
        {
            if (CurrentAccount.Role(user) != "User" && CurrentAccount.Role(user) != "Admin")
            {
                throw new RuleException("You do not have access to orders.");
            }

            List<OrderItem> rows = WorkflowSql_Order(CurrentAccount.Id(user), CurrentAccount.Role(user), id);
            if (rows.Count == 0)
            {
                return null;
            }

            ViewData["Title"] = "Order #" + id;
            return rows;
        }

        private void WorkflowLogic_UpdateOrder(ClaimsPrincipal user, long id, string value)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User", "Admin");
            WorkflowSql_OrderStatus(actor, CurrentAccount.Role(user), id, value);
        }

        private List<OrderItem> WorkflowSql_Orders(long actor, string role, bool received)
        {
            List<OrderItem> sqlResult1 = new List<OrderItem>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT i.[Id], i.[OrderId], i.[ListingId], i.[SellerId], i.[Title], i.[ListingType], i.[UnitPrice], i.[Deposit], i.[Months], i.[Amount], i.[Status], i.[Collected], i.[CreatedAt], i.[ProcessedAt], i.[CompletedAt],o.BuyerId,o.CreatedAt AS OrderDate,a.FullName AS BuyerName,b.FullName AS SellerName
                FROM dbo.OrderItems i
                JOIN dbo.Orders o ON o.Id=i.OrderId
                JOIN dbo.Accounts a ON a.Id=o.BuyerId
                JOIN dbo.Accounts b ON b.Id=i.SellerId
                WHERE @role='Admin'
                OR (@received=1
                AND i.SellerId=@actor)
                OR (@received=0
                AND o.BuyerId=@actor)
                ORDER BY o.Id DESC,i.Id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@received", ((object? )received ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            OrderItem databaseRow = new OrderItem();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["OrderId"] != DBNull.Value)
                            {
                                databaseRow.OrderId = Convert.ToInt64(sqlReader1["OrderId"]);
                            }

                            if (sqlReader1["ListingId"] != DBNull.Value)
                            {
                                databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                            }

                            if (sqlReader1["SellerId"] != DBNull.Value)
                            {
                                databaseRow.SellerId = Convert.ToInt64(sqlReader1["SellerId"]);
                            }

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["ListingType"] != DBNull.Value)
                            {
                                databaseRow.ListingType = Convert.ToString(sqlReader1["ListingType"])!;
                            }

                            if (sqlReader1["UnitPrice"] != DBNull.Value)
                            {
                                databaseRow.UnitPrice = Convert.ToDecimal(sqlReader1["UnitPrice"]);
                            }

                            if (sqlReader1["Deposit"] != DBNull.Value)
                            {
                                databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                            }

                            if (sqlReader1["Months"] != DBNull.Value)
                            {
                                databaseRow.Months = Convert.ToInt32(sqlReader1["Months"]);
                            }

                            if (sqlReader1["Amount"] != DBNull.Value)
                            {
                                databaseRow.Amount = Convert.ToDecimal(sqlReader1["Amount"]);
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Collected"] != DBNull.Value)
                            {
                                databaseRow.Collected = Convert.ToBoolean(sqlReader1["Collected"]);
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["ProcessedAt"] != DBNull.Value)
                            {
                                databaseRow.ProcessedAt = Convert.ToDateTime(sqlReader1["ProcessedAt"]);
                            }

                            if (sqlReader1["CompletedAt"] != DBNull.Value)
                            {
                                databaseRow.CompletedAt = Convert.ToDateTime(sqlReader1["CompletedAt"]);
                            }

                            if (sqlReader1["BuyerId"] != DBNull.Value)
                            {
                                databaseRow.BuyerId = Convert.ToInt64(sqlReader1["BuyerId"]);
                            }

                            if (sqlReader1["OrderDate"] != DBNull.Value)
                            {
                                databaseRow.OrderDate = Convert.ToDateTime(sqlReader1["OrderDate"]);
                            }

                            if (sqlReader1["BuyerName"] != DBNull.Value)
                            {
                                databaseRow.BuyerName = Convert.ToString(sqlReader1["BuyerName"])!;
                            }

                            if (sqlReader1["SellerName"] != DBNull.Value)
                            {
                                databaseRow.SellerName = Convert.ToString(sqlReader1["SellerName"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
        }

        private List<OrderItem> WorkflowSql_Order(long actor, string role, long id)
        {
            List<OrderItem> sqlResult1 = new List<OrderItem>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT i.[Id], i.[OrderId], i.[ListingId], i.[SellerId], i.[Title], i.[ListingType], i.[UnitPrice], i.[Deposit], i.[Months], i.[Amount], i.[Status], i.[Collected], i.[CreatedAt], i.[ProcessedAt], i.[CompletedAt],o.BuyerId,o.Address,o.City,o.State,o.Pincode,o.Phone,a.FullName AS BuyerName,a.Email AS BuyerEmail,b.FullName AS SellerName
                FROM dbo.OrderItems i
                JOIN dbo.Orders o ON o.Id=i.OrderId
                JOIN dbo.Accounts a ON a.Id=o.BuyerId
                JOIN dbo.Accounts b ON b.Id=i.SellerId
                WHERE o.Id=@id
                AND (@role='Admin'
                OR o.BuyerId=@actor
                OR i.SellerId=@actor)
                ORDER BY i.Id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            OrderItem databaseRow = new OrderItem();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["OrderId"] != DBNull.Value)
                            {
                                databaseRow.OrderId = Convert.ToInt64(sqlReader1["OrderId"]);
                            }

                            if (sqlReader1["ListingId"] != DBNull.Value)
                            {
                                databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                            }

                            if (sqlReader1["SellerId"] != DBNull.Value)
                            {
                                databaseRow.SellerId = Convert.ToInt64(sqlReader1["SellerId"]);
                            }

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["ListingType"] != DBNull.Value)
                            {
                                databaseRow.ListingType = Convert.ToString(sqlReader1["ListingType"])!;
                            }

                            if (sqlReader1["UnitPrice"] != DBNull.Value)
                            {
                                databaseRow.UnitPrice = Convert.ToDecimal(sqlReader1["UnitPrice"]);
                            }

                            if (sqlReader1["Deposit"] != DBNull.Value)
                            {
                                databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                            }

                            if (sqlReader1["Months"] != DBNull.Value)
                            {
                                databaseRow.Months = Convert.ToInt32(sqlReader1["Months"]);
                            }

                            if (sqlReader1["Amount"] != DBNull.Value)
                            {
                                databaseRow.Amount = Convert.ToDecimal(sqlReader1["Amount"]);
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Collected"] != DBNull.Value)
                            {
                                databaseRow.Collected = Convert.ToBoolean(sqlReader1["Collected"]);
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["ProcessedAt"] != DBNull.Value)
                            {
                                databaseRow.ProcessedAt = Convert.ToDateTime(sqlReader1["ProcessedAt"]);
                            }

                            if (sqlReader1["CompletedAt"] != DBNull.Value)
                            {
                                databaseRow.CompletedAt = Convert.ToDateTime(sqlReader1["CompletedAt"]);
                            }

                            if (sqlReader1["BuyerId"] != DBNull.Value)
                            {
                                databaseRow.BuyerId = Convert.ToInt64(sqlReader1["BuyerId"]);
                            }

                            if (sqlReader1["Address"] != DBNull.Value)
                            {
                                databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                            }

                            if (sqlReader1["City"] != DBNull.Value)
                            {
                                databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                            }

                            if (sqlReader1["State"] != DBNull.Value)
                            {
                                databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                            }

                            if (sqlReader1["Pincode"] != DBNull.Value)
                            {
                                databaseRow.Pincode = Convert.ToString(sqlReader1["Pincode"])!;
                            }

                            if (sqlReader1["Phone"] != DBNull.Value)
                            {
                                databaseRow.Phone = Convert.ToString(sqlReader1["Phone"])!;
                            }

                            if (sqlReader1["BuyerName"] != DBNull.Value)
                            {
                                databaseRow.BuyerName = Convert.ToString(sqlReader1["BuyerName"])!;
                            }

                            if (sqlReader1["BuyerEmail"] != DBNull.Value)
                            {
                                databaseRow.BuyerEmail = Convert.ToString(sqlReader1["BuyerEmail"])!;
                            }

                            if (sqlReader1["SellerName"] != DBNull.Value)
                            {
                                databaseRow.SellerName = Convert.ToString(sqlReader1["SellerName"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return sqlResult1;
        }

        private static void WorkflowLogic_Require(ClaimsPrincipal user, params string[] roles)
        {
            if (!roles.Contains(CurrentAccount.Role(user)))
            {
                throw new RuleException("You do not have access to this action.");
            }
        }

        private void WorkflowSql_OrderStatus(long actor, string role, long line, string status)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<OrderItem> sqlResult1 = new List<OrderItem>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT i.[Id], i.[OrderId], i.[ListingId], i.[SellerId], i.[Title], i.[ListingType], i.[UnitPrice], i.[Deposit], i.[Months], i.[Amount], i.[Status], i.[Collected], i.[CreatedAt], i.[ProcessedAt], i.[CompletedAt],o.BuyerId
                FROM dbo.OrderItems i WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.Orders o ON o.Id=i.OrderId
                WHERE i.Id=@line
                AND (@role='Admin'
                OR i.SellerId=@actor)", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@role", ((object? )role ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@line", ((object? )line ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    OrderItem databaseRow = new OrderItem();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OrderId"] != DBNull.Value)
                                    {
                                        databaseRow.OrderId = Convert.ToInt64(sqlReader1["OrderId"]);
                                    }

                                    if (sqlReader1["ListingId"] != DBNull.Value)
                                    {
                                        databaseRow.ListingId = Convert.ToInt64(sqlReader1["ListingId"]);
                                    }

                                    if (sqlReader1["SellerId"] != DBNull.Value)
                                    {
                                        databaseRow.SellerId = Convert.ToInt64(sqlReader1["SellerId"]);
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["ListingType"] != DBNull.Value)
                                    {
                                        databaseRow.ListingType = Convert.ToString(sqlReader1["ListingType"])!;
                                    }

                                    if (sqlReader1["UnitPrice"] != DBNull.Value)
                                    {
                                        databaseRow.UnitPrice = Convert.ToDecimal(sqlReader1["UnitPrice"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Months"] != DBNull.Value)
                                    {
                                        databaseRow.Months = Convert.ToInt32(sqlReader1["Months"]);
                                    }

                                    if (sqlReader1["Amount"] != DBNull.Value)
                                    {
                                        databaseRow.Amount = Convert.ToDecimal(sqlReader1["Amount"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Collected"] != DBNull.Value)
                                    {
                                        databaseRow.Collected = Convert.ToBoolean(sqlReader1["Collected"]);
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["ProcessedAt"] != DBNull.Value)
                                    {
                                        databaseRow.ProcessedAt = Convert.ToDateTime(sqlReader1["ProcessedAt"]);
                                    }

                                    if (sqlReader1["CompletedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CompletedAt = Convert.ToDateTime(sqlReader1["CompletedAt"]);
                                    }

                                    if (sqlReader1["BuyerId"] != DBNull.Value)
                                    {
                                        databaseRow.BuyerId = Convert.ToInt64(sqlReader1["BuyerId"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var row = (sqlResult1).FirstOrDefault() ?? throw new RuleException("Order not found.");
                        if (!(row.Status == "Pending" && status == "Processing" || row.Status == "Processing" && status == "Completed"))
                        {
                            throw new RuleException("Invalid order transition.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"UPDATE dbo.OrderItems
                SET Status=@status,ProcessedAt=CASE WHEN @status='Processing' THEN SYSUTCDATETIME() ELSE ProcessedAt END,CompletedAt=CASE WHEN @status='Completed' THEN SYSUTCDATETIME() ELSE CompletedAt END,Collected=CASE WHEN @status='Completed' THEN 1 ELSE 0 END
                WHERE Id=@line;INSERT dbo.OrderHistory(OrderItemId,Status,ActorId)
                VALUES(@line,@status,@actor)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@line", ((object? )line ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@status", ((object? )status ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        if (status == "Completed")
                        {
                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("UPDATE dbo.Listings SET Status='Sold',Available=0 WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@id", ((object? )row.ListingId ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }
                        }

                        WorkflowSql_Notify(connection, transaction, row.BuyerId, "Order #" + row.OrderId + " is " + (status ?? "").ToLowerInvariant(), "/Workspace/Order/" + row.OrderId);
                        {
                            transaction.Commit();
                            return;
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static void WorkflowSql_Notify(SqlConnection c, SqlTransaction tx, long account, string message, string url, bool email = true)
        {
            int sqlResult1;
            using (SqlCommand sqlCommand1 = new SqlCommand("INSERT dbo.Notifications(AccountId,Text,Url) VALUES(@account,@message,@url)", c, tx))
            {
                sqlCommand1.Parameters.AddWithValue("@account", ((object? )account ?? DBNull.Value));
                sqlCommand1.Parameters.AddWithValue("@message", ((object? )message[..Math.Min(500, message.Length)] ?? DBNull.Value));
                sqlCommand1.Parameters.AddWithValue("@url", ((object? )url ?? DBNull.Value));
                sqlResult1 = sqlCommand1.ExecuteNonQuery();
            }

            if (email)
            {
                int sqlResult2;
                using (SqlCommand sqlCommand2 = new SqlCommand(@"INSERT dbo.EmailOutbox(Recipient,Subject,Body) SELECT Email,'SettleMate update',@message
                FROM dbo.Accounts
                WHERE Id=@account", c, tx))
                {
                    sqlCommand2.Parameters.AddWithValue("@message", ((object? )message ?? DBNull.Value));
                    sqlCommand2.Parameters.AddWithValue("@account", ((object? )account ?? DBNull.Value));
                    sqlResult2 = sqlCommand2.ExecuteNonQuery();
                }
            }
        }

        private void ValidateForm()
        {
            if (ModelState.IsValid)
            {
                return;
            }

            List<string> messages = new List<string>();
            foreach (ModelStateEntry? field in ModelState.Values)
            {
                foreach (ModelError? error in field.Errors)
                {
                    if (string.IsNullOrWhiteSpace(error.ErrorMessage))
                    {
                        messages.Add("Check the submitted values.");
                    }
                    else
                    {
                        messages.Add(error.ErrorMessage);
                    }
                }
            }

            throw new RuleException(string.Join(" ", messages));
        }

        private IActionResult Success(RecordAction model)
        {
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Updated successfully." });
            }

            TempData["Success"] = "Updated successfully.";
            string destination = "/Workspace";
            if (Url.IsLocalUrl(model.ReturnUrl))
            {
                destination = model.ReturnUrl;
            }

            destination = WorkspaceRoutes.Resolve(CurrentAccount.Role(User), destination);
            return LocalRedirect(destination);
        }
    }
}
