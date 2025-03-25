using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusInfo.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class ApiKeysModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
