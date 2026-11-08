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
    /// <summary>Compatibility only: old bookmarks and notification URLs stay usable.</summary>
    [Authorize]
    public class WorkspaceController : Controller
    {
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment env;
        public WorkspaceController(IConfiguration configuration, IWebHostEnvironment env)
        {
            this.configuration = configuration;
            this.env = env;
        }

        [AcceptVerbs("GET", "POST")]
        [Route("/Workspace/{legacyAction=Index}/{id?}")]
        public IActionResult Legacy(string legacyAction, long? id)
        {
            string? source = Request.Path + Request.QueryString;
            string? destination = WorkspaceRoutes.Resolve(CurrentAccount.Role(User), source);
            if (destination == source)
            {
                return NotFound();
            }

            // HTTP 307 preserves form bodies, file uploads and antiforgery tokens.
            return LocalRedirectPreserveMethod(destination);
        }
    }
}
