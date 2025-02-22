// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Mvc.RazorPages;

// namespace BusInfo.Pages.Admin
// {
//     public class UserManagementModel : PageModel
//     {
//         public List<UserInfo> Users { get; private set; } = new();
//         public UserStats Stats { get; private set; } = new();

//         public async Task OnGetAsync()
//         {
//             Users = await GetUsers();
//             Stats = await GetUserStats();
//         }
//     }
// }