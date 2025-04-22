using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace BusInfo.Pages
{
    [Authorize]
    public class BusRankingsModel : PageModel
    {
        private readonly ILogger<BusRankingsModel> _logger;

        public BusRankingsModel(ILogger<BusRankingsModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            _logger.LogInformation("Bus Rankings page accessed at {Time}", DateTime.UtcNow);
        }
    }
}
