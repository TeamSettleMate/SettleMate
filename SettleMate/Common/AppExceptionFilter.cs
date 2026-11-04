using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.SqlClient;
using SettleMate.Web.Models;

namespace SettleMate.Web.Common
{
    public class AppExceptionFilter : IExceptionFilter
    {
        private readonly ITempDataDictionaryFactory tempData;
        private readonly ILogger<AppExceptionFilter> log;
        public AppExceptionFilter(ITempDataDictionaryFactory tempData, ILogger<AppExceptionFilter> log)
        {
            this.tempData = tempData;
            this.log = log;
        }

        public void OnException(ExceptionContext context)
        {
            Exception? ex = context.Exception;
            string message;
            if (ex is RuleException)
            {
                message = ex.Message;
            }
            else if ((ex is SqlException sql) && (sql.Number == 2601 || sql.Number == 2627))
            {
                message = "This record already exists. Check your email, visit request or review.";
            }
            else if (ex is SqlException)
            {
                message = "The database operation could not be completed. Check the connection and schema, then try again.";
            }
            else
            {
                message = "The operation could not be completed. Please try again.";
            }

            log.LogWarning("Request failed with exception type {Type}", ex.GetType().Name);
            HttpContext? http = context.HttpContext;
            if (http.Request.Headers.Accept.ToString().Contains("application/json"))
            {
                context.Result = new JsonResult(new { success = false, message })
                {
                    StatusCode = 400
                };
            }
            else if (HttpMethods.IsPost(http.Request.Method))
            {
                tempData.GetTempData(http)["Error"] = message;
                string? back = "/";
                if (Uri.TryCreate(http.Request.Headers.Referer.ToString(), UriKind.Absolute, out var uri) && uri.Authority == http.Request.Host.Value)
                {
                    back = uri.PathAndQuery;
                }

                context.Result = new LocalRedirectResult(back);
            }
            else
            {
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/Error.cshtml",
                    StatusCode = ex is RuleException ? 404 : 503,
                    ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
                    {
                        Model = message
                    }
                };
            }

            context.ExceptionHandled = true;
        }
    }
}
