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

namespace SettleMate.Web.Areas.User.Controllers
{
    [Area("User")]
    [Authorize(Roles = "User")]
    public class CartController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public CartController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(WorkspaceQueryLogic_Cart(User));
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            return View(new OrderCheckout { Items = WorkspaceQueryLogic_Cart(User) });
        }

        [HttpPost]
        public IActionResult Checkout(OrderCheckout model)
        {
            model.Items = WorkspaceQueryLogic_Cart(User);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                long id = WorkflowLogic_Checkout(User, model);
                TempData["Success"] = "Order placed. Payment and any rental deposit are due on delivery.";
                return RedirectToAction("Details", "Order", new { area = "User", id });
            }
            catch (RuleException exception)
            {
                ModelState.AddModelError("", exception.Message);
                return View(model);
            }
        }

        [HttpPost]
        public IActionResult Add(CartInput model)
        {
            ValidateForm();
            WorkflowLogic_AddToCart(User, model.Id, number: model.Number);
            return Success(model);
        }

        [HttpPost]
        public IActionResult Remove(RecordAction model)
        {
            ValidateForm();
            WorkflowLogic_RemoveFromCart(User, model.Id);
            return Success(model);
        }

        private List<CartItem> WorkspaceQueryLogic_Cart(ClaimsPrincipal user)
        {
            if (CurrentAccount.Role(user) != "User")
            {
                throw new RuleException("Sign in as a User.");
            }

            return WorkflowSql_Cart(CurrentAccount.Id(user));
        }

        private long WorkflowLogic_Checkout(ClaimsPrincipal user, OrderCheckout m)
        {
            WorkflowLogic_Require(user, "User");
            if (m.CheckoutToken == Guid.Empty)
            {
                throw new RuleException("Invalid checkout. Refresh this page.");
            }

            return WorkflowSql_Checkout(CurrentAccount.Id(user), m);
        }

        private void WorkflowLogic_AddToCart(ClaimsPrincipal user, long id, int number)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            if (number < 1 || number > 24)
            {
                throw new RuleException("Rental duration must be 1–24 months.");
            }

            WorkflowSql_AddCart(actor, id, number);
        }

        private void WorkflowLogic_RemoveFromCart(ClaimsPrincipal user, long id)
        {
            long actor = CurrentAccount.Id(user);
            if (actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            WorkflowLogic_Require(user, "User");
            WorkflowSql_RemoveCart(actor, id);
        }

        private List<CartItem> WorkflowSql_Cart(long actor)
        {
            List<CartEntry> sqlResult1 = new List<CartEntry>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt],c.Months,a.FullName AS SellerName,(SELECT TOP 1 Name
                FROM dbo.Files
                WHERE ListingId=l.Id
                AND Private=0
                ORDER BY Id) AS Image
                FROM dbo.CartItems c
                JOIN dbo.Listings l ON l.Id=c.ListingId
                JOIN dbo.Accounts a ON a.Id=l.OwnerId
                WHERE c.UserId=@actor
                ORDER BY l.Id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            CartEntry databaseRow = new CartEntry();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["OwnerId"] != DBNull.Value)
                            {
                                databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                            }

                            if (sqlReader1["Kind"] != DBNull.Value)
                            {
                                databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                            }

                            if (sqlReader1["Title"] != DBNull.Value)
                            {
                                databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                            }

                            if (sqlReader1["Description"] != DBNull.Value)
                            {
                                databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                            }

                            if (sqlReader1["Type"] != DBNull.Value)
                            {
                                databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                            }

                            if (sqlReader1["City"] != DBNull.Value)
                            {
                                databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                            }

                            if (sqlReader1["State"] != DBNull.Value)
                            {
                                databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                            }

                            if (sqlReader1["Address"] != DBNull.Value)
                            {
                                databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                            }

                            if (sqlReader1["Price"] != DBNull.Value)
                            {
                                databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                            }

                            if (sqlReader1["Deposit"] != DBNull.Value)
                            {
                                databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Available"] != DBNull.Value)
                            {
                                databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                            }

                            if (sqlReader1["Reason"] != DBNull.Value)
                            {
                                databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                            }

                            if (sqlReader1["Details"] != DBNull.Value)
                            {
                                databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["UpdatedAt"] != DBNull.Value)
                            {
                                databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                            }

                            if (sqlReader1["Months"] != DBNull.Value)
                            {
                                databaseRow.Months = Convert.ToInt32(sqlReader1["Months"]);
                            }

                            if (sqlReader1["SellerName"] != DBNull.Value)
                            {
                                databaseRow.SellerName = Convert.ToString(sqlReader1["SellerName"])!;
                            }

                            if (sqlReader1["Image"] != DBNull.Value)
                            {
                                databaseRow.Image = Convert.ToString(sqlReader1["Image"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            var rows = sqlResult1;
            List<CartItem>? items = new List<CartItem>();
            foreach (var row in rows)
            {
                items.Add(new CartItem { Id = row.Id, Title = row.Title, Type = row.Type, Status = row.Status, SellerName = row.SellerName, Image = row.Image, Price = row.Price, Deposit = row.Deposit, Months = (int)row.Months });
            }

            return items;
        }

        private static void WorkflowLogic_Require(ClaimsPrincipal user, params string[] roles)
        {
            if (!roles.Contains(CurrentAccount.Role(user)))
            {
                throw new RuleException("You do not have access to this action.");
            }
        }

        private long WorkflowSql_Checkout(long actor, OrderCheckout m)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Order> sqlResult1 = new List<Order>();
                        using (SqlCommand sqlCommand1 = new SqlCommand("SELECT Id,BuyerId FROM dbo.Orders WHERE CheckoutToken=@CheckoutToken", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@CheckoutToken", ((object? )m.CheckoutToken ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Order databaseRow = new Order();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["BuyerId"] != DBNull.Value)
                                    {
                                        databaseRow.BuyerId = Convert.ToInt64(sqlReader1["BuyerId"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var prior = sqlResult1;
                        if (prior.Count > 0)
                        {
                            if (prior[0].BuyerId != actor)
                            {
                                throw new RuleException("Invalid checkout reference.");
                            }

                            {
                                long transactionResult = prior[0].Id;
                                transaction.Commit();
                                return transactionResult;
                            }
                        }

                        List<CartEntry> sqlResult2 = new List<CartEntry>();
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt],ci.Months,a.Status AS SellerStatus
                FROM dbo.CartItems ci
                JOIN dbo.Listings l WITH(UPDLOCK,HOLDLOCK) ON l.Id=ci.ListingId
                JOIN dbo.Accounts a ON a.Id=l.OwnerId
                WHERE ci.UserId=@actor
                ORDER BY l.Id", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                            {
                                while (sqlReader2.Read())
                                {
                                    CartEntry databaseRow = new CartEntry();
                                    if (sqlReader2["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                                    }

                                    if (sqlReader2["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader2["OwnerId"]);
                                    }

                                    if (sqlReader2["Kind"] != DBNull.Value)
                                    {
                                        databaseRow.Kind = Convert.ToString(sqlReader2["Kind"])!;
                                    }

                                    if (sqlReader2["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader2["Title"])!;
                                    }

                                    if (sqlReader2["Description"] != DBNull.Value)
                                    {
                                        databaseRow.Description = Convert.ToString(sqlReader2["Description"])!;
                                    }

                                    if (sqlReader2["Type"] != DBNull.Value)
                                    {
                                        databaseRow.Type = Convert.ToString(sqlReader2["Type"])!;
                                    }

                                    if (sqlReader2["City"] != DBNull.Value)
                                    {
                                        databaseRow.City = Convert.ToString(sqlReader2["City"])!;
                                    }

                                    if (sqlReader2["State"] != DBNull.Value)
                                    {
                                        databaseRow.State = Convert.ToString(sqlReader2["State"])!;
                                    }

                                    if (sqlReader2["Address"] != DBNull.Value)
                                    {
                                        databaseRow.Address = Convert.ToString(sqlReader2["Address"])!;
                                    }

                                    if (sqlReader2["Price"] != DBNull.Value)
                                    {
                                        databaseRow.Price = Convert.ToDecimal(sqlReader2["Price"]);
                                    }

                                    if (sqlReader2["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader2["Deposit"]);
                                    }

                                    if (sqlReader2["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader2["Status"])!;
                                    }

                                    if (sqlReader2["Available"] != DBNull.Value)
                                    {
                                        databaseRow.Available = Convert.ToBoolean(sqlReader2["Available"]);
                                    }

                                    if (sqlReader2["Reason"] != DBNull.Value)
                                    {
                                        databaseRow.Reason = Convert.ToString(sqlReader2["Reason"])!;
                                    }

                                    if (sqlReader2["Details"] != DBNull.Value)
                                    {
                                        databaseRow.Details = Convert.ToString(sqlReader2["Details"])!;
                                    }

                                    if (sqlReader2["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader2["CreatedAt"]);
                                    }

                                    if (sqlReader2["UpdatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader2["UpdatedAt"]);
                                    }

                                    if (sqlReader2["Months"] != DBNull.Value)
                                    {
                                        databaseRow.Months = Convert.ToInt32(sqlReader2["Months"]);
                                    }

                                    if (sqlReader2["SellerStatus"] != DBNull.Value)
                                    {
                                        databaseRow.SellerStatus = Convert.ToString(sqlReader2["SellerStatus"])!;
                                    }

                                    sqlResult2.Add(databaseRow);
                                }
                            }
                        }

                        var items = sqlResult2;
                        if (items.Count == 0)
                        {
                            throw new RuleException("Your cart is empty.");
                        }

                        foreach (var item in items)
                        {
                            if (item.Status != "Approved" || !item.Available || item.OwnerId == actor || item.SellerStatus != "Active" || item.Kind != "Item")
                            {
                                throw new RuleException("An item is no longer available. Remove it from your cart.");
                            }
                        }

                        List<Order> sqlResult3 = new List<Order>();
                        using (SqlCommand sqlCommand3 = new SqlCommand(@"INSERT dbo.Orders(BuyerId,CheckoutToken,Address,City,State,Pincode,Phone)
                OUTPUT INSERTED.Id
                VALUES(@actor,@CheckoutToken,@Address,@City,@State,@Pincode,@Phone)", connection, transaction))
                        {
                            sqlCommand3.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@CheckoutToken", ((object? )m.CheckoutToken ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@Address", ((object? )m.Address ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@City", ((object? )m.City ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@State", ((object? )m.State ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@Pincode", ((object? )m.Pincode ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@Phone", ((object? )m.Phone ?? DBNull.Value));
                            using (SqlDataReader sqlReader3 = sqlCommand3.ExecuteReader())
                            {
                                while (sqlReader3.Read())
                                {
                                    Order databaseRow = new Order();
                                    if (sqlReader3["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader3["Id"]);
                                    }

                                    sqlResult3.Add(databaseRow);
                                }
                            }
                        }

                        long order = (sqlResult3)[0].Id;
                        foreach (var item in items)
                        {
                            int months;
                            if (item.Type == "Rent")
                            {
                                months = (int)item.Months;
                            }
                            else
                            {
                                months = 1;
                            }

                            decimal deposit;
                            if (item.Type == "Rent")
                            {
                                deposit = item.Deposit;
                            }
                            else
                            {
                                deposit = 0;
                            }

                            decimal amount = item.Price * months + deposit;
                            List<OrderItem> sqlResult4 = new List<OrderItem>();
                            using (SqlCommand sqlCommand4 = new SqlCommand(@"INSERT dbo.OrderItems(OrderId,ListingId,SellerId,Title,ListingType,UnitPrice,Deposit,Months,Amount)
                OUTPUT INSERTED.Id
                VALUES(@order,@listing,@seller,@title,@type,@price,@deposit,@months,@amount)", connection, transaction))
                            {
                                sqlCommand4.Parameters.AddWithValue("@order", ((object? )order ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@listing", ((object? )item.Id ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@seller", ((object? )item.OwnerId ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@title", ((object? )item.Title ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@type", ((object? )item.Type ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@price", ((object? )item.Price ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@deposit", ((object? )deposit ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@months", ((object? )months ?? DBNull.Value));
                                sqlCommand4.Parameters.AddWithValue("@amount", ((object? )amount ?? DBNull.Value));
                                using (SqlDataReader sqlReader4 = sqlCommand4.ExecuteReader())
                                {
                                    while (sqlReader4.Read())
                                    {
                                        OrderItem databaseRow = new OrderItem();
                                        if (sqlReader4["Id"] != DBNull.Value)
                                        {
                                            databaseRow.Id = Convert.ToInt64(sqlReader4["Id"]);
                                        }

                                        sqlResult4.Add(databaseRow);
                                    }
                                }
                            }

                            long line = (sqlResult4)[0].Id;
                            int sqlResult5;
                            using (SqlCommand sqlCommand5 = new SqlCommand(@"UPDATE dbo.Listings
                SET Status='Reserved',Available=0
                WHERE Id=@listing;INSERT dbo.OrderHistory(OrderItemId,Status,ActorId)
                VALUES(@line,'Pending',@actor)", connection, transaction))
                            {
                                sqlCommand5.Parameters.AddWithValue("@listing", ((object? )item.Id ?? DBNull.Value));
                                sqlCommand5.Parameters.AddWithValue("@line", ((object? )line ?? DBNull.Value));
                                sqlCommand5.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                                sqlResult5 = sqlCommand5.ExecuteNonQuery();
                            }

                            WorkflowSql_Notify(connection, transaction, item.OwnerId, "New order #" + order + " for " + item.Title, "/Workspace/Order/" + order);
                        }

                        int sqlResult6;
                        using (SqlCommand sqlCommand6 = new SqlCommand("DELETE dbo.CartItems WHERE UserId=@actor", connection, transaction))
                        {
                            sqlCommand6.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlResult6 = sqlCommand6.ExecuteNonQuery();
                        }

                        {
                            long transactionResult = order;
                            transaction.Commit();
                            return transactionResult;
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

        private void WorkflowSql_AddCart(long actor, long listing, int months)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<Listing> sqlResult1 = new List<Listing>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT l.[Id], l.[OwnerId], l.[Kind], l.[Title], l.[Description], l.[Type], l.[City], l.[State], l.[Address], l.[Price], l.[Deposit], l.[Status], l.[Available], l.[Reason], l.[Details], l.[CreatedAt], l.[UpdatedAt]
                FROM dbo.Listings l
                JOIN dbo.Accounts a ON a.Id=l.OwnerId
                WHERE l.Id=@listing
                AND l.Kind='Item'
                AND l.Status='Approved'
                AND l.Available=1
                AND l.OwnerId<>@actor
                AND a.Status='Active'", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    Listing databaseRow = new Listing();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["OwnerId"] != DBNull.Value)
                                    {
                                        databaseRow.OwnerId = Convert.ToInt64(sqlReader1["OwnerId"]);
                                    }

                                    if (sqlReader1["Kind"] != DBNull.Value)
                                    {
                                        databaseRow.Kind = Convert.ToString(sqlReader1["Kind"])!;
                                    }

                                    if (sqlReader1["Title"] != DBNull.Value)
                                    {
                                        databaseRow.Title = Convert.ToString(sqlReader1["Title"])!;
                                    }

                                    if (sqlReader1["Description"] != DBNull.Value)
                                    {
                                        databaseRow.Description = Convert.ToString(sqlReader1["Description"])!;
                                    }

                                    if (sqlReader1["Type"] != DBNull.Value)
                                    {
                                        databaseRow.Type = Convert.ToString(sqlReader1["Type"])!;
                                    }

                                    if (sqlReader1["City"] != DBNull.Value)
                                    {
                                        databaseRow.City = Convert.ToString(sqlReader1["City"])!;
                                    }

                                    if (sqlReader1["State"] != DBNull.Value)
                                    {
                                        databaseRow.State = Convert.ToString(sqlReader1["State"])!;
                                    }

                                    if (sqlReader1["Address"] != DBNull.Value)
                                    {
                                        databaseRow.Address = Convert.ToString(sqlReader1["Address"])!;
                                    }

                                    if (sqlReader1["Price"] != DBNull.Value)
                                    {
                                        databaseRow.Price = Convert.ToDecimal(sqlReader1["Price"]);
                                    }

                                    if (sqlReader1["Deposit"] != DBNull.Value)
                                    {
                                        databaseRow.Deposit = Convert.ToDecimal(sqlReader1["Deposit"]);
                                    }

                                    if (sqlReader1["Status"] != DBNull.Value)
                                    {
                                        databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                                    }

                                    if (sqlReader1["Available"] != DBNull.Value)
                                    {
                                        databaseRow.Available = Convert.ToBoolean(sqlReader1["Available"]);
                                    }

                                    if (sqlReader1["Reason"] != DBNull.Value)
                                    {
                                        databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                                    }

                                    if (sqlReader1["Details"] != DBNull.Value)
                                    {
                                        databaseRow.Details = Convert.ToString(sqlReader1["Details"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    if (sqlReader1["UpdatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.UpdatedAt = Convert.ToDateTime(sqlReader1["UpdatedAt"]);
                                    }

                                    databaseRow.Data = ReadListingDetails(databaseRow.Details);
                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var l = (sqlResult1).FirstOrDefault() ?? throw new RuleException("This item is unavailable or belongs to you.");
                        if (l.Type != "Rent")
                        {
                            months = 1;
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"DELETE dbo.CartItems
                WHERE UserId=@actor
                AND ListingId=@listing;INSERT dbo.CartItems(UserId,ListingId,Months)
                VALUES(@actor,@listing,@months)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@months", ((object? )months ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

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

        private void WorkflowSql_RemoveCart(long actor, long listing)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("DELETE dbo.CartItems WHERE UserId=@actor AND ListingId=@listing", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@listing", ((object? )listing ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
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

        private Listing ReadListingDetails(string json)
        {
            Listing listing = new Listing();
            if (string.IsNullOrWhiteSpace(json))
            {
                return listing;
            }

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement data = document.RootElement;
                JsonElement value;
                if (data.TryGetProperty("Id", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Id = value.GetInt64();
                }

                if (data.TryGetProperty("Kind", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Kind = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Title", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Title = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Description", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Description = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Type", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Type = value.GetString() ?? "";
                }

                if (data.TryGetProperty("City", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.City = value.GetString() ?? "";
                }

                if (data.TryGetProperty("State", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.State = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Address", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Address = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Pincode", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Pincode = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Price", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Price = value.GetDecimal();
                }

                if (data.TryGetProperty("Deposit", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Deposit = value.GetDecimal();
                }

                if (data.TryGetProperty("Maintenance", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Maintenance = value.GetDecimal();
                }

                if (data.TryGetProperty("Basis", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Basis = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Gender", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Gender = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Furnishing", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Furnishing = value.GetString() ?? "";
                }

                if (data.TryGetProperty("RoomType", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.RoomType = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Bhk", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Bhk = value.GetInt32();
                }

                if (data.TryGetProperty("Bedrooms", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Bedrooms = value.GetInt32();
                }

                if (data.TryGetProperty("Floor", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Floor = value.GetInt32();
                }

                if (data.TryGetProperty("Beds", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Beds = value.GetInt32();
                }

                if (data.TryGetProperty("AttachedBathroom", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AttachedBathroom = value.GetBoolean();
                }

                if (data.TryGetProperty("Food", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Food = value.GetBoolean();
                }

                if (data.TryGetProperty("Meals", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Meals = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Curfew", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Curfew = value.GetString() ?? "";
                }

                if (data.TryGetProperty("AvailableFrom", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AvailableFrom = value.GetDateTime();
                }

                if (data.TryGetProperty("MinimumStay", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.MinimumStay = value.GetInt32();
                }

                if (data.TryGetProperty("NoticePeriod", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.NoticePeriod = value.GetInt32();
                }

                if (data.TryGetProperty("Electricity", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Electricity = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Water", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Water = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Landmark", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Landmark = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Latitude", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Latitude = value.GetDecimal();
                }

                if (data.TryGetProperty("Longitude", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Longitude = value.GetDecimal();
                }

                if (data.TryGetProperty("Rules", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Rules = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Visitors", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Visitors = value.GetBoolean();
                }

                if (data.TryGetProperty("Smoking", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Smoking = value.GetBoolean();
                }

                if (data.TryGetProperty("Pets", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Pets = value.GetBoolean();
                }

                if (data.TryGetProperty("AgreementType", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AgreementType = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Condition", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Condition = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Category", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Category = value.GetString() ?? "";
                }

                if (data.TryGetProperty("OwnerRelation", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.OwnerRelation = value.GetString() ?? "";
                }

                if (data.TryGetProperty("DocumentType", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.DocumentType = value.GetString() ?? "";
                }

                if (data.TryGetProperty("DocumentReference", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.DocumentReference = value.GetString() ?? "";
                }

                if (data.TryGetProperty("AdminNote", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.AdminNote = value.GetString() ?? "";
                }

                if (data.TryGetProperty("Declaration1", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration1 = value.GetBoolean();
                }

                if (data.TryGetProperty("Declaration2", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration2 = value.GetBoolean();
                }

                if (data.TryGetProperty("Declaration3", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration3 = value.GetBoolean();
                }

                if (data.TryGetProperty("Declaration4", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    listing.Declaration4 = value.GetBoolean();
                }

                if (data.TryGetProperty("Amenities", out value) && value.ValueKind != JsonValueKind.Null)
                {
                    List<string> values = new List<string>();
                    foreach (JsonElement item in value.EnumerateArray())
                    {
                        values.Add(item.GetString() ?? "");
                    }

                    listing.Amenities = values.ToArray();
                }
            }

            return listing;
        }
    }
}
