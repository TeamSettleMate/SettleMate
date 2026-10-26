using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SettleMate.Web.Configuration;
using SettleMate.Web.Common;

// Create the ASP.NET Core application builder.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<AppExceptionFilter>();
    options.Filters.Add<NotificationCountFilter>();
});
// Register the cookie validation event.
builder.Services.AddScoped<CookieValidator>();
// Authentication cookies remember which account has signed in.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login/User";
    options.AccessDeniedPath = "/Error/403";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
    else
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }

    options.EventsType = typeof(CookieValidator);
});
builder.Services.AddAuthorization();
// Keep the existing limits on repeated login, OTP and contact requests.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    foreach (string name in new string[]
    {
        "auth",
        "otp",
        "contact"
    }

    )
    {
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter((context.Connection.RemoteIpAddress?.ToString() ?? "unknown") + name, _ => new FixedWindowRateLimiterOptions { PermitLimit = name == "otp" ? 10 : 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    }
});
builder.Services.AddHostedService<OutboxWorker>();
WebApplication app = builder.Build();
foreach (string argument in args)
{
    if (argument == "--initialize-database" || argument == "--check-database" || argument == "--seed-admin")
    {
        DatabaseSetup.Run(argument, app.Configuration, app.Environment.ContentRootPath);
        return;
    }
}

app.UseExceptionHandler("/Error/500");
app.UseStatusCodePagesWithReExecute("/Error/{0}");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});
// Request pipeline: files, routes, authentication, then authorization.
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("areas", "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
