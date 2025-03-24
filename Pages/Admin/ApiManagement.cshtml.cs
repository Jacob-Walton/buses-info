using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusInfo.Models;
using BusInfo.Models.Admin;
using BusInfo.Data;
using BusInfo.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace BusInfo.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class ApiManagementModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IRequestTrackingService _requestTracking;

        public List<ApiKeyInfo> ApiKeys { get; private set; } = [];
        public List<ApiRequestViewModel> PendingRequests { get; private set; } = [];

        public ApiManagementModel(ApplicationDbContext context, IRequestTrackingService requestTracking)
        {
            _context = context;
            _requestTracking = requestTracking;
        }

        public async Task OnGetAsync()
        {
            // Load API keys with usage data
            var keys = await _context.ApiKeys
                .Join(
                    _context.Users,
                    key => key.UserId,
                    user => user.Id,
                    (key, user) => new ApiKeyInfo
                    {
                        Key = key.Key,
                        UserId = user.Id,
                        UserEmail = user.Email,
                        CreatedAt = key.CreatedAt,
                        ExpiresAt = key.CreatedAt.AddYears(1),
                        IsActive = key.IsActive,
                        LastUsed = key.LastUsed,
                        RequestsToday = 0,
                        TotalRequests = 0
                    })
                .ToListAsync();

            // Enhance with metrics data
            foreach (var key in keys)
            {
                var metrics = await _requestTracking.GetApiKeyMetricsAsync(key.Key);
                key.RequestsToday = metrics.RequestsToday;
                key.TotalRequests = metrics.TotalRequests;
            }

            ApiKeys = keys;

            // Load pending requests
            var pendingRequests = await _context.ApiKeyRequests
                .Where(r => r.Status == "Pending")
                .Join(
                    _context.Users,
                    req => req.UserId,
                    user => user.Id,
                    (req, user) => new ApiRequestViewModel
                    {
                        Id = req.Id,
                        UserId = user.Id,
                        UserEmail = user.Email,
                        RequestedAt = req.RequestedAt,
                        RequestedTimeAgo = GetTimeAgo(req.RequestedAt)
                    })
                .ToListAsync();

            PendingRequests = pendingRequests;
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalDays > 1)
                return $"{(int)timeSpan.TotalDays} days ago";

            if (timeSpan.TotalHours > 1)
                return $"{(int)timeSpan.TotalHours} hours ago";

            if (timeSpan.TotalMinutes > 1)
                return $"{(int)timeSpan.TotalMinutes} minutes ago";

            return "just now";
        }

        public class ApiRequestViewModel
        {
            public int Id { get; set; }
            public string UserId { get; set; } = string.Empty;
            public string UserEmail { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }
            public string RequestedTimeAgo { get; set; } = string.Empty;
        }
    }
}
