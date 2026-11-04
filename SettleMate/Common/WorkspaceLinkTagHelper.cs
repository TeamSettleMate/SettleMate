using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SettleMate.Web.Models;

namespace SettleMate.Web.Common
{
    /// <summary>Canonicalizes links from shared partials and persisted dashboard URLs.</summary>
    [HtmlTargetElement("a", Attributes = "href")]
    [HtmlTargetElement("form", Attributes = "action")]
    public class WorkspaceLinkTagHelper : TagHelper
    {
        [ViewContext, HtmlAttributeNotBound]
        public ViewContext ViewContext { get; set; } = default !;

        public override int Order
        {
            get
            {
                return 1000;
            }
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            string? attribute;
            if (output.TagName == "form")
            {
                attribute = "action";
            }
            else
            {
                attribute = "href";
            }

            if (output.Attributes.TryGetAttribute(attribute, out var value))
            {
                output.Attributes.SetAttribute(attribute, WorkspaceRoutes.Resolve(CurrentAccount.Role(ViewContext.HttpContext.User), value.Value?.ToString() ?? ""));
            }
        }
    }
}
