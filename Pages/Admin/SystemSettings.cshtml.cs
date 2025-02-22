// using System.Collections.Generic;
// using System.Threading.Tasks;
// using BusInfo.Models;
// using Microsoft.AspNetCore.Mvc.RazorPages;

// namespace BusInfo.Pages.Admin
// {
//     public class SystemSettingsModel : PageModel
//     {
//         public AdminSettings Settings { get; private set; } = new();
//         public List<SettingsHistoryEntry> History { get; private set; } = new();

//         public async Task OnGetAsync()
//         {
//             Settings = await GetSettings();
//             History = await GetHistory();
//         }
//     }
// }