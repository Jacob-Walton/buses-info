using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BusInfo.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using BusInfo.Models;

namespace BusInfo.Pages.Account
{
    [Authorize]
    public class SettingsModel(ApplicationDbContext context) : PageModel
    {
        private readonly ApplicationDbContext _context = context;
        private List<string> _availableRoutes = [];
        public IReadOnlyCollection<string> AvailableRoutes => _availableRoutes;
        public DateTime? LastLogin { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            ApplicationUser? user = await _context.Users.FirstOrDefaultAsync(u => u.Email == (User.Identity != null ? User.Identity.Name : null));
            if (user != null)
            {
                LastLogin = user.LastLoginAt;
            }

            // Only load routes as fallback - client will try API first
            _availableRoutes = await _context.BusArrivals
                .AsNoTracking()
                .Select(b => b.Service)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            if (_availableRoutes.Count == 0)
            {
                _availableRoutes = [ "102", "103", "115", "117", "119", "125", "566", "712", "715",
                                  "718", "720", "760", "761", "762", "763", "764", "765", "778",
                                  "800", "801", "803", "807", "809", "819", "820", "821", "822",
                                  "823", "824", "825", "826", "953", "954", "956", "957", "958",
                                  "959", "959B", "961", "962", "963", "964", "965", "965B", "975",
                                  "983", "998" ];
            }

            return Page();
        }
    }
}