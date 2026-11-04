using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace SettleMate.Web.Common
{
    // Turns a RuleException that an action did not catch into a friendly response.
    public class AppExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not RuleException ex)
            {
                return;
            }

            HttpRequest request = context.HttpContext.Request;
            bool wantsJson = request.Headers.XRequestedWith == "XMLHttpRequest" || request.Headers.Accept.ToString().Contains("application/json");
            if (wantsJson)
            {
                context.Result = new BadRequestObjectResult(new Result(false, ex.Message));
            }
            else
            {
                ITempDataDictionary tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(context.HttpContext);
                tempData["Error"] = ex.Message;
                string referer = request.Headers.Referer.ToString();
                context.Result = new LocalRedirectResult(Uri.TryCreate(referer, UriKind.Absolute, out Uri? uri) && uri.Host == request.Host.Host ? uri.PathAndQuery : "/");
            }

            context.ExceptionHandled = true;
        }
    }
}
