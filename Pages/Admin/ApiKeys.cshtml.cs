using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusInfo.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ApiKeysModel : PageModel
    {
        public void OnGet()
        {
            // API key data will be loaded via JavaScript/API
        }
    }
}
