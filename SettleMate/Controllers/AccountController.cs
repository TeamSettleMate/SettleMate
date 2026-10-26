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

namespace SettleMate.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public AccountController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        string Session()
        {
            if (!Request.Cookies.TryGetValue("SM.OtpSession", out var session) || session.Length != 64)
            {
                session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                Response.Cookies.Append("SM.OtpSession", session, new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromHours(1) });
            }

            return PasswordLogic_Digest(session);
        }

        [HttpGet("/Account/Login/{role?}")]
        public IActionResult Login(string role = "User", string? returnUrl = null)
        {
            if (!(role == "User" || role == "Owner" || role == "Admin"))
            {
                return NotFound();
            }

            return View(new Login { Role = role, ReturnUrl = returnUrl });
        }

        [HttpPost("/Account/Login/{role?}"), EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(Login model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var a = AuthLogic_Login(model);
                Claim[]? claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, a.Id.ToString()),
                    new Claim(ClaimTypes.Name, a.FullName),
                    new Claim(ClaimTypes.Role, a.Role),
                    new Claim("Stamp", a.SecurityStamp.ToString())
                };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
                return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : "/Workspace");
            }
            catch (RuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpGet("/Account/Register/{role?}")]
        public IActionResult Register(string role = "User")
        {
            if (!(role == "User" || role == "Owner"))
            {
                return NotFound();
            }

            Session();
            return View(new Registration { Role = role });
        }

        [HttpPost("/Account/Register/{role?}"), EnableRateLimiting("auth")]
        public IActionResult Register(Registration model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                AuthLogic_Register(model, Session());
                if (model.Role == "Owner")
                {
                    TempData["Success"] = "Registration received. An admin must approve your owner account before login.";
                }
                else
                {
                    TempData["Success"] = "Registration complete. Sign in to get started.";
                }

                return Redirect("/Account/Login/" + model.Role);
            }
            catch (RuleException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [HttpPost, EnableRateLimiting("otp")]
        public IActionResult SendOtp(string email, string purpose)
        {
            return Json(AuthLogic_SendOtp(email, purpose, Session(), CurrentAccount.Id(User)));
        }

        [HttpPost, EnableRateLimiting("otp")]
        public IActionResult VerifyOtp(string email, string purpose, string code)
        {
            return Json(AuthLogic_VerifyOtp(email, purpose, Session(), CurrentAccount.Id(User), code));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            Session();
            return View();
        }

        [HttpPost, EnableRateLimiting("auth")]
        public IActionResult ResetPassword(string email, string proof, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                throw new RuleException("Passwords do not match.");
            }

            AuthLogic_Reset(email, Session(), proof, password);
            TempData["Success"] = "Password updated. Sign in again.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost, Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return Redirect("/");
        }

        [Authorize, AcceptVerbs("GET", "POST")]
        public IActionResult Profile()
        {
            return LocalRedirectPreserveMethod($"/{CurrentAccount.Role(User)}/Profile/Index");
        }

        [Authorize, HttpGet]
        public IActionResult Settings()
        {
            return LocalRedirect($"/{CurrentAccount.Role(User)}/Settings/Index");
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                throw new RuleException("Passwords do not match.");
            }

            AuthLogic_ChangePassword(CurrentAccount.Id(User), currentPassword, password);
            await HttpContext.SignOutAsync();
            TempData["Success"] = "Password updated. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> ChangeEmail(string email, string proof)
        {
            AuthLogic_ChangeEmail(CurrentAccount.Id(User), email, Session(), proof);
            await HttpContext.SignOutAsync();
            TempData["Success"] = "Email updated. Please sign in again.";
            return RedirectToAction(nameof(Login));
        }

        private static string PasswordLogic_Digest(string s)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
        }

        private Account AuthLogic_Login(Login model)
        {
            var a = AccountSql_ByEmail(AuthLogic_Email(model.Email));
            if (a == null || !PasswordLogic_Verify(a.PasswordHash, model.Password) || a.Role != model.Role)
            {
                throw new RuleException("Email or password is incorrect.");
            }

            if (a.Status == "Blocked")
            {
                throw new RuleException("This account is blocked. Contact support.");
            }

            if (a.Status == "PendingApproval")
            {
                throw new RuleException("Your owner account is awaiting admin approval.");
            }

            if (a.Status == "Rejected")
            {
                throw new RuleException("Your account was rejected: " + a.Reason);
            }

            if (!a.EmailVerified)
            {
                throw new RuleException("Verify your email before signing in.");
            }

            return a;
        }

        private long AuthLogic_Register(Registration model, string session)
        {
            if (model.Address == null)
            {
                model.Address = "";
            }

            model.Email = AuthLogic_Email(model.Email);
            if (!(model.Role == "User" || model.Role == "Owner"))
            {
                throw new RuleException("Choose User or Owner.");
            }

            if (!(model.Gender == "Male" || model.Gender == "Female" || model.Gender == "Other"))
            {
                throw new RuleException("Choose a valid gender.");
            }

            if (!(model.DocumentType == "Government ID" || model.DocumentType == "Passport" || model.DocumentType == "Driving Licence"))
            {
                throw new RuleException("Choose a valid document type.");
            }

            if (!PasswordLogic_Valid(model.Password))
            {
                throw new RuleException("Use at least 8 characters, a number and a symbol.");
            }

            if (model.Role == "User" && (model.DateOfBirth == null || model.DateOfBirth >= DateTime.Today || string.IsNullOrWhiteSpace(model.Address) || string.IsNullOrWhiteSpace(model.Pincode)))
            {
                throw new RuleException("Complete your date of birth and address details.");
            }

            StoredFile? document = null;
            if (model.Role == "Owner")
            {
                if (model.IdentityDocument == null)
                {
                    throw new RuleException("Upload an identity document.");
                }

                document = FileLogic_Save(model.IdentityDocument, true, "Identity");
            }

            try
            {
                long id = AccountSql_Register(model, PasswordLogic_Hash(model.Password), PasswordLogic_Digest(model.OtpProof), session, null, document);
                return id;
            }
            catch
            {
                if (document != null)
                {
                    FileLogic_Delete(document);
                }

                throw;
            }
        }

        private Result AuthLogic_SendOtp(string email, string purpose, string session, long actor)
        {
            email = AuthLogic_Email(email);
            if (!(purpose == "Registration" || purpose == "ForgotPassword" || purpose == "ChangeEmail"))
            {
                throw new RuleException("Invalid verification purpose.");
            }

            if (purpose == "ChangeEmail" && actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            var existing = AccountSql_ByEmail(email);
            if (purpose != "ForgotPassword" && existing != null)
            {
                throw new RuleException("This email is already registered.");
            }

            if (purpose == "ForgotPassword" && existing == null)
            {
                return new Result(true, "If the account exists, a code has been sent.");
            }

            string? code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
            AccountSql_SaveOtp(email, purpose, session, purpose == "ChangeEmail" ? actor : null, PasswordLogic_Hash(code));
            try
            {
                EmailLogic_Send(email, "Your SettleMate verification code", $"Your verification code is {code}. It expires in 5 minutes. Do not share this code.");
            }
            catch (SmtpException)
            {
                throw new RuleException("Email could not be sent. Please try again after 30 seconds.");
            }

            return new Result(true, purpose == "ForgotPassword" ? "If the account exists, a code has been sent." : "A verification code has been sent.");
        }

        private Result AuthLogic_VerifyOtp(string email, string purpose, string session, long actor, string code)
        {
            email = AuthLogic_Email(email);
            if (!(purpose == "Registration" || purpose == "ForgotPassword" || purpose == "ChangeEmail") || !Regex.IsMatch(code ?? "", @"^\d{6}$"))
            {
                throw new RuleException("Enter all six digits.");
            }

            if (purpose == "ChangeEmail" && actor == 0)
            {
                throw new RuleException("Please sign in.");
            }

            string? proof = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            Result? result = AccountSql_VerifyOtp(email, purpose, session, purpose == "ChangeEmail" ? actor : null, code!, PasswordLogic_Digest(proof));
            if (result.Success)
            {
                result.Proof = proof;
                return result;
            }
            else
            {
                return result;
            }
        }

        private void AuthLogic_Reset(string email, string session, string proof, string password)
        {
            if (proof == null || proof.Length != 64)
            {
                throw new RuleException("Verify your email first.");
            }

            if (!PasswordLogic_Valid(password))
            {
                throw new RuleException("Use 8–128 characters, a number and a symbol.");
            }

            if (!AccountSql_UseProof(AuthLogic_Email(email), "ForgotPassword", session, null, PasswordLogic_Digest(proof), PasswordLogic_Hash(password)))
            {
                throw new RuleException("Verification expired. Request a new code.");
            }
        }

        private void AuthLogic_ChangePassword(long id, string current, string next)
        {
            var a = AccountSql_Get(id);
            if (a == null || !PasswordLogic_Verify(a.PasswordHash, current))
            {
                throw new RuleException("Current password is incorrect.");
            }

            if (!PasswordLogic_Valid(next))
            {
                throw new RuleException("Use 8–128 characters, a number and a symbol.");
            }

            AccountSql_Password(id, PasswordLogic_Hash(next));
        }

        private void AuthLogic_ChangeEmail(long actor, string email, string session, string proof)
        {
            if (proof == null || proof.Length != 64)
            {
                throw new RuleException("Verify the new email first.");
            }

            if (!AccountSql_UseProof(AuthLogic_Email(email), "ChangeEmail", session, actor, PasswordLogic_Digest(proof)))
            {
                throw new RuleException("Verify the new email again.");
            }
        }

        private static string AuthLogic_Email(string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            if (value.Length > 254 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(value))
            {
                throw new RuleException("Enter a valid email address.");
            }

            return value;
        }

        private Account? AccountSql_ByEmail(string email)
        {
            List<Account> sqlResult1 = new List<Account>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [FullName], [Email], [Phone], [PasswordHash], [Role], [Status], [Reason], [EmailVerified], [SecurityStamp], [DateOfBirth], [Gender], [Address], [City], [State], [Pincode], [Avatar], [DocumentType], [CreatedAt], [NormalizedEmail] FROM dbo.Accounts WHERE NormalizedEmail=UPPER(@email)", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Account databaseRow = new Account();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["FullName"] != DBNull.Value)
                            {
                                databaseRow.FullName = Convert.ToString(sqlReader1["FullName"])!;
                            }

                            if (sqlReader1["Email"] != DBNull.Value)
                            {
                                databaseRow.Email = Convert.ToString(sqlReader1["Email"])!;
                            }

                            if (sqlReader1["Phone"] != DBNull.Value)
                            {
                                databaseRow.Phone = Convert.ToString(sqlReader1["Phone"])!;
                            }

                            if (sqlReader1["PasswordHash"] != DBNull.Value)
                            {
                                databaseRow.PasswordHash = Convert.ToString(sqlReader1["PasswordHash"])!;
                            }

                            if (sqlReader1["Role"] != DBNull.Value)
                            {
                                databaseRow.Role = Convert.ToString(sqlReader1["Role"])!;
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Reason"] != DBNull.Value)
                            {
                                databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                            }

                            if (sqlReader1["EmailVerified"] != DBNull.Value)
                            {
                                databaseRow.EmailVerified = Convert.ToBoolean(sqlReader1["EmailVerified"]);
                            }

                            if (sqlReader1["SecurityStamp"] != DBNull.Value)
                            {
                                databaseRow.SecurityStamp = (Guid)sqlReader1["SecurityStamp"];
                            }

                            if (sqlReader1["DateOfBirth"] != DBNull.Value)
                            {
                                databaseRow.DateOfBirth = Convert.ToDateTime(sqlReader1["DateOfBirth"]);
                            }

                            if (sqlReader1["Gender"] != DBNull.Value)
                            {
                                databaseRow.Gender = Convert.ToString(sqlReader1["Gender"])!;
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

                            if (sqlReader1["Avatar"] != DBNull.Value)
                            {
                                databaseRow.Avatar = Convert.ToInt32(sqlReader1["Avatar"]);
                            }

                            if (sqlReader1["DocumentType"] != DBNull.Value)
                            {
                                databaseRow.DocumentType = Convert.ToString(sqlReader1["DocumentType"])!;
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["NormalizedEmail"] != DBNull.Value)
                            {
                                databaseRow.NormalizedEmail = Convert.ToString(sqlReader1["NormalizedEmail"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return (sqlResult1).FirstOrDefault();
        }

        private bool PasswordLogic_Verify(string stored, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            try
            {
                string[]? p = stored.Split('.');
                return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(p[2]), Rfc2898DeriveBytes.Pbkdf2(value, Convert.FromBase64String(p[1]), int.Parse(p[0]), HashAlgorithmName.SHA256, 32));
            }
            catch (FormatException)
            {
                return false;
            }
            catch (IndexOutOfRangeException)
            {
                return false;
            }
        }

        private bool PasswordLogic_Valid(string value)
        {
            return value != null && value.Length >= 8 && value.Length <= 128 && Regex.IsMatch(value, @"\d") && Regex.IsMatch(value, @"[^\w\s]");
        }

        private StoredFile FileLogic_Save(IFormFile file, bool privateFile, string purpose)
        {
            if (file.Length < 1 || file.Length > 5242880)
            {
                throw new RuleException("Each file must be between 1 byte and 5 MB.");
            }

            string? extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!(privateFile ? new[]
            {
                ".pdf",
                ".jpg",
                ".jpeg",
                ".png"
            }

            : new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            }

            ).Contains(extension))
            {
                throw new RuleException("Unsupported file type.");
            }

            using (Stream? source = file.OpenReadStream())
            {
                byte[]? header = new byte[12];
                int n = source.Read(header);
                source.Position = 0;
                bool valid;
                if (extension is ".pdf")
                {
                    valid = n >= 5 && System.Text.Encoding.ASCII.GetString(header, 0, 5) == "%PDF-";
                }
                else if (extension is ".png")
                {
                    valid = n >= 8 && header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                }
                else if (extension is ".jpg" or ".jpeg")
                {
                    valid = n >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
                }
                else if (extension is ".webp")
                {
                    valid = n >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
                }
                else
                {
                    valid = false;
                }

                if (!valid)
                {
                    throw new RuleException("The file content does not match its extension.");
                }

                string? name = Guid.NewGuid().ToString("N") + extension;
                string? folder;
                if (privateFile)
                {
                    folder = Path.Combine(env.ContentRootPath, "App_Data", "private");
                }
                else
                {
                    folder = Path.Combine(env.WebRootPath, "uploads");
                }

                Directory.CreateDirectory(folder);
                using (FileStream? output = System.IO.File.Create(Path.Combine(folder, name)))
                    source.CopyTo(output);
                string? mime;
                if (extension is ".pdf")
                {
                    mime = "application/pdf";
                }
                else if (extension is ".png")
                {
                    mime = "image/png";
                }
                else if (extension is ".webp")
                {
                    mime = "image/webp";
                }
                else
                {
                    mime = "image/jpeg";
                }

                return new StoredFile(name, Path.GetFileName(file.FileName)[..Math.Min(255, Path.GetFileName(file.FileName).Length)], mime, privateFile, purpose);
            }
        }

        private string PasswordLogic_Hash(string value)
        {
            byte[]? salt = RandomNumberGenerator.GetBytes(16);
            byte[]? hash = Rfc2898DeriveBytes.Pbkdf2(value, salt, 210000, HashAlgorithmName.SHA256, 32);
            return $"210000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private long AccountSql_Register(Registration m, string hash, string proof, string session, long? actor, StoredFile? document)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        int sqlResult1;
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"UPDATE dbo.OtpChallenges
                SET IsUsed=1
                WHERE Email=@Email
                AND Purpose='Registration'
                AND SessionHash=@session
                AND ProofHash=@proof
                AND IsUsed=0
                AND VerifiedAt>DATEADD(minute,-10,SYSUTCDATETIME())", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@Email", ((object? )m.Email ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@session", ((object? )session ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@proof", ((object? )proof ?? DBNull.Value));
                            sqlResult1 = sqlCommand1.ExecuteNonQuery();
                        }

                        int used = sqlResult1;
                        if (used != 1)
                        {
                            throw new RuleException("Verify your email again before completing registration.");
                        }

                        List<Account> sqlResult2 = new List<Account>();
                        using (SqlCommand sqlCommand2 = new SqlCommand(@"INSERT dbo.Accounts(FullName,Email,Phone,PasswordHash,Role,Status,DateOfBirth,Gender,Address,City,State,Pincode,Avatar,DocumentType)
                OUTPUT INSERTED.Id
                VALUES(@FullName,@Email,@Phone,@hash,@Role,@status,@DateOfBirth,@Gender,@Address,@City,@State,@Pincode,@Avatar,@DocumentType)", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@FullName", ((object? )m.FullName ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Email", ((object? )m.Email ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Phone", ((object? )m.Phone ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@hash", ((object? )hash ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Role", ((object? )m.Role ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@status", ((object? )(m.Role == "Owner" ? "PendingApproval" : "Active") ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@DateOfBirth", ((object? )m.DateOfBirth ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Gender", ((object? )m.Gender ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Address", ((object? )m.Address ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@City", ((object? )m.City ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@State", ((object? )m.State ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Pincode", ((object? )m.Pincode ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@Avatar", ((object? )m.Avatar ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@DocumentType", ((object? )m.DocumentType ?? DBNull.Value));
                            using (SqlDataReader sqlReader2 = sqlCommand2.ExecuteReader())
                            {
                                while (sqlReader2.Read())
                                {
                                    Account databaseRow = new Account();
                                    if (sqlReader2["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader2["Id"]);
                                    }

                                    sqlResult2.Add(databaseRow);
                                }
                            }
                        }

                        var rows = sqlResult2;
                        long accountId = rows[0].Id;
                        if (document != null)
                        {
                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand(@"INSERT dbo.Files(OwnerId,Name,OriginalName,ContentType,Private,Purpose)
                VALUES(@accountId,@Name,@OriginalName,@ContentType,@Private,@Purpose)", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@accountId", ((object? )accountId ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@Name", ((object? )document.Name ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@OriginalName", ((object? )document.OriginalName ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@ContentType", ((object? )document.ContentType ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@Private", ((object? )document.Private ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@Purpose", ((object? )document.Purpose ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }
                        }

                        {
                            long transactionResult = accountId;
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

        private void FileLogic_Delete(StoredFile file)
        {
            string? folder;
            if (file.Private)
            {
                folder = Path.Combine(env.ContentRootPath, "App_Data", "private");
            }
            else
            {
                folder = Path.Combine(env.WebRootPath, "uploads");
            }

            string? path = Path.Combine(folder, Path.GetFileName(file.Name));
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        private long AccountSql_SaveOtp(string email, string purpose, string session, long? actor, string hash)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<OtpChallenge> sqlResult1 = new List<OtpChallenge>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT TOP 1 Id
                FROM dbo.OtpChallenges WITH(UPDLOCK,HOLDLOCK)
                WHERE Email=@email
                AND Purpose=@purpose
                AND CreatedAt>DATEADD(second,-30,SYSUTCDATETIME())", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@purpose", ((object? )purpose ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    OtpChallenge databaseRow = new OtpChallenge();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var recent = sqlResult1;
                        if (recent.Count > 0)
                        {
                            throw new RuleException("Wait 30 seconds before requesting another code.");
                        }

                        int sqlResult2;
                        using (SqlCommand sqlCommand2 = new SqlCommand("UPDATE dbo.OtpChallenges SET IsUsed=1 WHERE Email=@email AND Purpose=@purpose AND IsUsed=0", connection, transaction))
                        {
                            sqlCommand2.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                            sqlCommand2.Parameters.AddWithValue("@purpose", ((object? )purpose ?? DBNull.Value));
                            sqlResult2 = sqlCommand2.ExecuteNonQuery();
                        }

                        List<OtpChallenge> sqlResult3 = new List<OtpChallenge>();
                        using (SqlCommand sqlCommand3 = new SqlCommand(@"INSERT dbo.OtpChallenges(Email,Purpose,ActorId,SessionHash,CodeHash,ExpiresAt)
                OUTPUT INSERTED.Id
                VALUES(@email,@purpose,@actor,@session,@hash,DATEADD(minute,5,SYSUTCDATETIME()))", connection, transaction))
                        {
                            sqlCommand3.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@purpose", ((object? )purpose ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@session", ((object? )session ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@hash", ((object? )hash ?? DBNull.Value));
                            using (SqlDataReader sqlReader3 = sqlCommand3.ExecuteReader())
                            {
                                while (sqlReader3.Read())
                                {
                                    OtpChallenge databaseRow = new OtpChallenge();
                                    if (sqlReader3["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader3["Id"]);
                                    }

                                    sqlResult3.Add(databaseRow);
                                }
                            }
                        }

                        var rows = sqlResult3;
                        {
                            long transactionResult = rows[0].Id;
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

        private void EmailLogic_Send(string to, string subject, string body)
        {
            IConfigurationSection? config = configuration.GetSection("EmailSettings");
            string? from = config["Email"];
            string? pass = config["Password"];
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(pass))
            {
                throw new RuleException("Email is not configured. Set EmailSettings:Password in user secrets.");
            }

            using (SmtpClient? client = new SmtpClient(config["SmtpServer"] ?? "smtp.gmail.com", config.GetValue<int>("Port", 587))
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(from, pass.Replace(" ", "")),
                Timeout = 20000
            }

            )
            {
                using (MailMessage? mail = new MailMessage(from, to, subject, body)
                {
                    IsBodyHtml = false
                }

                )
                {
                    client.Send(mail);
                }
            }
        }

        private Result AccountSql_VerifyOtp(string email, string purpose, string session, long? actor, string code, string proofHash)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        List<OtpChallenge> sqlResult1 = new List<OtpChallenge>();
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT TOP 1 [Id], [Email], [Purpose], [ActorId], [SessionHash], [CodeHash], [ExpiresAt], [Attempts], [IsUsed], [VerifiedAt], [ProofHash], [CreatedAt]
                FROM dbo.OtpChallenges WITH(UPDLOCK,HOLDLOCK)
                WHERE Email=@email
                AND Purpose=@purpose
                AND SessionHash=@session
                AND ((ActorId=@actor)
                OR (ActorId IS NULL
                AND @actor IS NULL))
                ORDER BY Id DESC", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@purpose", ((object? )purpose ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@session", ((object? )session ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                            {
                                while (sqlReader1.Read())
                                {
                                    OtpChallenge databaseRow = new OtpChallenge();
                                    if (sqlReader1["Id"] != DBNull.Value)
                                    {
                                        databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                                    }

                                    if (sqlReader1["Email"] != DBNull.Value)
                                    {
                                        databaseRow.Email = Convert.ToString(sqlReader1["Email"])!;
                                    }

                                    if (sqlReader1["Purpose"] != DBNull.Value)
                                    {
                                        databaseRow.Purpose = Convert.ToString(sqlReader1["Purpose"])!;
                                    }

                                    if (sqlReader1["ActorId"] != DBNull.Value)
                                    {
                                        databaseRow.ActorId = Convert.ToInt64(sqlReader1["ActorId"]);
                                    }

                                    if (sqlReader1["SessionHash"] != DBNull.Value)
                                    {
                                        databaseRow.SessionHash = Convert.ToString(sqlReader1["SessionHash"])!;
                                    }

                                    if (sqlReader1["CodeHash"] != DBNull.Value)
                                    {
                                        databaseRow.CodeHash = Convert.ToString(sqlReader1["CodeHash"])!;
                                    }

                                    if (sqlReader1["ExpiresAt"] != DBNull.Value)
                                    {
                                        databaseRow.ExpiresAt = Convert.ToDateTime(sqlReader1["ExpiresAt"]);
                                    }

                                    if (sqlReader1["Attempts"] != DBNull.Value)
                                    {
                                        databaseRow.Attempts = Convert.ToInt32(sqlReader1["Attempts"]);
                                    }

                                    if (sqlReader1["IsUsed"] != DBNull.Value)
                                    {
                                        databaseRow.IsUsed = Convert.ToBoolean(sqlReader1["IsUsed"]);
                                    }

                                    if (sqlReader1["VerifiedAt"] != DBNull.Value)
                                    {
                                        databaseRow.VerifiedAt = Convert.ToDateTime(sqlReader1["VerifiedAt"]);
                                    }

                                    if (sqlReader1["ProofHash"] != DBNull.Value)
                                    {
                                        databaseRow.ProofHash = Convert.ToString(sqlReader1["ProofHash"])!;
                                    }

                                    if (sqlReader1["CreatedAt"] != DBNull.Value)
                                    {
                                        databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                                    }

                                    sqlResult1.Add(databaseRow);
                                }
                            }
                        }

                        var rows = sqlResult1;
                        if (rows.Count == 0)
                        {
                            Result? transactionResult = new Result(false, "Request a new code.");
                            transaction.Commit();
                            return transactionResult;
                        }

                        var row = rows[0];
                        long id = row.Id;
                        if (row.IsUsed || row.VerifiedAt.GetValueOrDefault() != DateTime.MinValue)
                        {
                            Result? transactionResult = new Result(false, "This code has already been used.");
                            transaction.Commit();
                            return transactionResult;
                        }

                        if (row.Attempts >= 5)
                        {
                            Result? transactionResult = new Result(false, "Too many attempts. Request a new code.");
                            transaction.Commit();
                            return transactionResult;
                        }

                        if (row.ExpiresAt < DateTime.UtcNow)
                        {
                            Result? transactionResult = new Result(false, "This code has expired.");
                            transaction.Commit();
                            return transactionResult;
                        }

                        if (!PasswordLogic_Verify(row.CodeHash, code))
                        {
                            int sqlResult2;
                            using (SqlCommand sqlCommand2 = new SqlCommand("UPDATE dbo.OtpChallenges SET Attempts=Attempts+1 WHERE Id=@id", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                                sqlResult2 = sqlCommand2.ExecuteNonQuery();
                            }

                            {
                                Result? transactionResult = new Result(false, "The code is incorrect.");
                                transaction.Commit();
                                return transactionResult;
                            }
                        }

                        int sqlResult3;
                        using (SqlCommand sqlCommand3 = new SqlCommand("UPDATE dbo.OtpChallenges SET VerifiedAt=SYSUTCDATETIME(),ProofHash=@proofHash WHERE Id=@id", connection, transaction))
                        {
                            sqlCommand3.Parameters.AddWithValue("@proofHash", ((object? )proofHash ?? DBNull.Value));
                            sqlCommand3.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                            sqlResult3 = sqlCommand3.ExecuteNonQuery();
                        }

                        {
                            Result? transactionResult = new Result(true, "Email verified.");
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

        private bool AccountSql_UseProof(string email, string purpose, string session, long? actor, string proof, string? passwordHash = null)
        {
            using (SqlConnection connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                connection.Open();
                using (SqlTransaction transaction = (SqlTransaction)connection.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        int sqlResult1;
                        using (SqlCommand sqlCommand1 = new SqlCommand(@"UPDATE dbo.OtpChallenges
                SET IsUsed=1
                WHERE Email=@email
                AND Purpose=@purpose
                AND SessionHash=@session
                AND ProofHash=@proof
                AND IsUsed=0
                AND VerifiedAt>DATEADD(minute,-10,SYSUTCDATETIME())
                AND ((ActorId=@actor)
                OR (ActorId IS NULL
                AND @actor IS NULL))", connection, transaction))
                        {
                            sqlCommand1.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@purpose", ((object? )purpose ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@session", ((object? )session ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                            sqlCommand1.Parameters.AddWithValue("@proof", ((object? )proof ?? DBNull.Value));
                            sqlResult1 = sqlCommand1.ExecuteNonQuery();
                        }

                        int n = sqlResult1;
                        if (n != 1)
                        {
                            bool transactionResult = false;
                            transaction.Commit();
                            return transactionResult;
                        }

                        if (purpose == "ForgotPassword")
                        {
                            int sqlResult2;
                            using (SqlCommand sqlCommand2 = new SqlCommand(@"UPDATE dbo.Accounts
                SET PasswordHash=@passwordHash,SecurityStamp=NEWID()
                WHERE NormalizedEmail=UPPER(@email)", connection, transaction))
                            {
                                sqlCommand2.Parameters.AddWithValue("@passwordHash", ((object? )passwordHash ?? DBNull.Value));
                                sqlCommand2.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                                sqlResult2 = sqlCommand2.ExecuteNonQuery();
                            }
                        }

                        if (purpose == "ChangeEmail")
                        {
                            int sqlResult3;
                            using (SqlCommand sqlCommand3 = new SqlCommand("UPDATE dbo.Accounts SET Email=@email,SecurityStamp=NEWID() WHERE Id=@actor", connection, transaction))
                            {
                                sqlCommand3.Parameters.AddWithValue("@email", ((object? )email ?? DBNull.Value));
                                sqlCommand3.Parameters.AddWithValue("@actor", ((object? )actor ?? DBNull.Value));
                                sqlResult3 = sqlCommand3.ExecuteNonQuery();
                            }
                        }

                        {
                            bool transactionResult = true;
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

        private Account? AccountSql_Get(long id)
        {
            List<Account> sqlResult1 = new List<Account>();
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand(@"SELECT [Id], [FullName], [Email], [Phone], [PasswordHash], [Role], [Status], [Reason], [EmailVerified], [SecurityStamp], [DateOfBirth], [Gender], [Address], [City], [State], [Pincode], [Avatar], [DocumentType], [CreatedAt], [NormalizedEmail] FROM dbo.Accounts WHERE Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    using (SqlDataReader sqlReader1 = sqlCommand1.ExecuteReader())
                    {
                        while (sqlReader1.Read())
                        {
                            Account databaseRow = new Account();
                            if (sqlReader1["Id"] != DBNull.Value)
                            {
                                databaseRow.Id = Convert.ToInt64(sqlReader1["Id"]);
                            }

                            if (sqlReader1["FullName"] != DBNull.Value)
                            {
                                databaseRow.FullName = Convert.ToString(sqlReader1["FullName"])!;
                            }

                            if (sqlReader1["Email"] != DBNull.Value)
                            {
                                databaseRow.Email = Convert.ToString(sqlReader1["Email"])!;
                            }

                            if (sqlReader1["Phone"] != DBNull.Value)
                            {
                                databaseRow.Phone = Convert.ToString(sqlReader1["Phone"])!;
                            }

                            if (sqlReader1["PasswordHash"] != DBNull.Value)
                            {
                                databaseRow.PasswordHash = Convert.ToString(sqlReader1["PasswordHash"])!;
                            }

                            if (sqlReader1["Role"] != DBNull.Value)
                            {
                                databaseRow.Role = Convert.ToString(sqlReader1["Role"])!;
                            }

                            if (sqlReader1["Status"] != DBNull.Value)
                            {
                                databaseRow.Status = Convert.ToString(sqlReader1["Status"])!;
                            }

                            if (sqlReader1["Reason"] != DBNull.Value)
                            {
                                databaseRow.Reason = Convert.ToString(sqlReader1["Reason"])!;
                            }

                            if (sqlReader1["EmailVerified"] != DBNull.Value)
                            {
                                databaseRow.EmailVerified = Convert.ToBoolean(sqlReader1["EmailVerified"]);
                            }

                            if (sqlReader1["SecurityStamp"] != DBNull.Value)
                            {
                                databaseRow.SecurityStamp = (Guid)sqlReader1["SecurityStamp"];
                            }

                            if (sqlReader1["DateOfBirth"] != DBNull.Value)
                            {
                                databaseRow.DateOfBirth = Convert.ToDateTime(sqlReader1["DateOfBirth"]);
                            }

                            if (sqlReader1["Gender"] != DBNull.Value)
                            {
                                databaseRow.Gender = Convert.ToString(sqlReader1["Gender"])!;
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

                            if (sqlReader1["Avatar"] != DBNull.Value)
                            {
                                databaseRow.Avatar = Convert.ToInt32(sqlReader1["Avatar"]);
                            }

                            if (sqlReader1["DocumentType"] != DBNull.Value)
                            {
                                databaseRow.DocumentType = Convert.ToString(sqlReader1["DocumentType"])!;
                            }

                            if (sqlReader1["CreatedAt"] != DBNull.Value)
                            {
                                databaseRow.CreatedAt = Convert.ToDateTime(sqlReader1["CreatedAt"]);
                            }

                            if (sqlReader1["NormalizedEmail"] != DBNull.Value)
                            {
                                databaseRow.NormalizedEmail = Convert.ToString(sqlReader1["NormalizedEmail"])!;
                            }

                            sqlResult1.Add(databaseRow);
                        }
                    }
                }
            }

            return (sqlResult1).FirstOrDefault();
        }

        private void AccountSql_Password(long id, string hash)
        {
            int sqlResult1;
            using (SqlConnection sqlConnection1 = new SqlConnection(configuration.GetConnectionString("DefaultConnection")))
            {
                sqlConnection1.Open();
                using (SqlCommand sqlCommand1 = new SqlCommand("UPDATE dbo.Accounts SET PasswordHash=@hash,SecurityStamp=NEWID() WHERE Id=@id", sqlConnection1))
                {
                    sqlCommand1.Parameters.AddWithValue("@id", ((object? )id ?? DBNull.Value));
                    sqlCommand1.Parameters.AddWithValue("@hash", ((object? )hash ?? DBNull.Value));
                    sqlResult1 = sqlCommand1.ExecuteNonQuery();
                }
            }
        }
    }
}
