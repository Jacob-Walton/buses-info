using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusInfo.Pages
{
    [Authorize(AuthenticationSchemes = "Cookies")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class BusInfoModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Page();
        }
    }
}