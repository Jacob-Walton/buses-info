using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusInfo.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class NotificationsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
