using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BusInfo.TagHelpers
{
    [HtmlTargetElement(Attributes = "cdn")]
    public class CdnTagHelper(IConfiguration configuration, IHostEnvironment hostEnvironment) : TagHelper
    {
        private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
        private readonly IConfiguration _configuration = configuration;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (_hostEnvironment.IsDevelopment())
                return;
            string? cdnBaseUrl = _configuration["Cdn:BaseUrl"];
            if (string.IsNullOrEmpty(cdnBaseUrl))
                return;

            string? url = output.Attributes["href"]?.Value?.ToString()
                      ?? output.Attributes["src"]?.Value?.ToString();

            if (string.IsNullOrEmpty(url))
                return;

            string attributeName = output.Attributes.ContainsName("href") ? "href" : "src";
            output.Attributes.SetAttribute(attributeName, $"{cdnBaseUrl.TrimEnd('/')}{url}");
            output.Attributes.RemoveAll("cdn");
        }
    }
}