// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Mvc.RazorPages;

// namespace BusInfo.Pages.Admin
// {
//     public class MaintenanceModel : PageModel
//     {
//         public SystemHealth Health { get; private set; } = new();
//         public List<MaintenanceLog> MaintenanceLogs { get; private set; } = new();
//         public List<ScheduledTask> ScheduledTasks { get; private set; } = new();

//         public async Task OnGetAsync()
//         {
//             Health = await GetSystemHealth();
//             MaintenanceLogs = await GetMaintenanceLogs();
//             ScheduledTasks = await GetScheduledTasks();
//         }
//     }
// }