// using System.Collections.Generic;
// using System.Threading.Tasks;
// using BusInfo.Models;
// using Microsoft.AspNetCore.Mvc.RazorPages;

// namespace BusInfo.Pages.Admin
// {
//     public class ApiManagementModel : PageModel
//     {
//         public List<ApiKeyRequest> PendingRequests { get; private set; } = [];
//         public List<ApiKeyInfo> ActiveKeys { get; private set; } = [];
//         public ApiUsageStats UsageStats { get; private set; } = new();

//         public async Task OnGetAsync()
//         {
//             PendingRequests = await GetPendingRequests();
//             ActiveKeys = await GetActiveKeys();
//             UsageStats = await GetUsageStats();
//         }
//     }
// }