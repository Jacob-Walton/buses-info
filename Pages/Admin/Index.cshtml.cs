using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Models.Admin;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BusInfo.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel(
        ApplicationDbContext context,
        IRequestTrackingService requestTrackingService,
        IUserService userService) : PageModel
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IRequestTrackingService _requestTrackingService = requestTrackingService;

        public AdminMetrics Metrics { get; private set; } = new();
        public SystemHealth SystemHealth { get; private set; } = new();
        public List<AdminActivityViewModel> RecentActivity { get; private set; } = [];
        public List<PendingRequestViewModel> PendingRequests { get; private set; } = [];

        public async Task<IActionResult> OnGetAsync()
        {
            // Load admin metrics
            await LoadMetricsAsync();

            // Load system health
            await LoadSystemHealthAsync();

            // Load recent activity
            await LoadRecentActivityAsync();

            // Load pending API requests
            await LoadPendingRequestsAsync();

            return Page();
        }

        private async Task LoadMetricsAsync()
        {
            // Get user count
            int totalUsers = await _context!.Users
                .AsNoTracking()
                .CountAsync(u => !u.DeletedAt.HasValue);

            // Get active API keys count
            int activeApiKeys = await _context!.ApiKeys
                .AsNoTracking()
                .CountAsync(k => k.IsActive);

            // Get API metrics
            RequestMetrics requestMetrics = await _requestTrackingService.GetMetricsAsync();

            Metrics = new AdminMetrics
            {
                TotalUsers = totalUsers,
                ActiveApiKeys = activeApiKeys,
                ApiRequests24Hours = requestMetrics.Requests24Hours,
                TotalApiRequests = requestMetrics.TotalRequests,
                AverageResponseTime = requestMetrics.AverageResponseTime,
                ErrorCount = requestMetrics.ErrorCount,
                PendingApiRequests = await _context!.ApiKeyRequests
                    .CountAsync(r => r.Status == "Pending")
            };
        }

        private Task LoadSystemHealthAsync()
        {
            // This would typically come from a health check service
            // For now, we'll create a simple mock

            SystemHealth = new SystemHealth
            {
                CpuUsage = 35.2,
                MemoryUsage = 42.8,
                DiskUsage = 65.3,
                ActiveConnections = 24,
                Services = new Dictionary<string, ServiceStatus>
                {
                    { "Database", ServiceStatus.Healthy },
                    { "API", ServiceStatus.Healthy },
                    { "Cache", ServiceStatus.Healthy },
                    { "Email", ServiceStatus.Degraded }
                }
            };

            return Task.CompletedTask;
        }

        private async Task LoadRecentActivityAsync()
        {
            List<AdminActivity> activities = await _context!.AdminActivities
                .AsNoTracking()
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .ToListAsync();

            RecentActivity = [.. activities.Select(a => new AdminActivityViewModel
            {
                Id = a.Id,
                Description = a.Description,
                Icon = a.Icon,
                Type = a.Type,
                AdminId = a.AdminId,
                AdminEmail = GetAdminEmail(a!.AdminId),
                TimeAgo = GetTimeAgo(a.Timestamp)
            })];
        }

        private async Task LoadPendingRequestsAsync()
        {
            List<ApiKeyRequest> requests = await _context!.ApiKeyRequests
                .AsNoTracking()
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.RequestedAt)
                .Take(5)
                .ToListAsync();

            PendingRequests = [.. requests.Select(r => new PendingRequestViewModel
            {
                Id = r.Id,
                UserId = r.UserId,
                UserEmail = GetUserEmail(r.UserId),
                RequestedAt = r.RequestedAt,
                RequestedTimeAgo = GetTimeAgo(r.RequestedAt)
            })];
        }

        private string GetUserEmail(string userId)
        {
            return _context!.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefault() ?? "Unknown";
        }

        private string GetAdminEmail(string adminId)
        {
            return string.IsNullOrEmpty(adminId)
                ? string.Empty
                : _context!.Users
                .AsNoTracking()
                .Where(u => u.Id == adminId)
                .Select(u => u.Email)
                .FirstOrDefault() ?? "Unknown Admin";
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            TimeSpan timeSpan = DateTime.UtcNow - dateTime;

            if (timeSpan.TotalDays > 1)
            {
                return $"{(int)timeSpan.TotalDays} days ago";
            }

            return timeSpan.TotalHours > 1
                ? $"{(int)timeSpan.TotalHours} hours ago"
                : timeSpan.TotalMinutes > 1 ? $"{(int)timeSpan.TotalMinutes} minutes ago" : "just now";
        }

        public class AdminActivityViewModel
        {
            public int Id { get; set; }
            public string Description { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string? AdminId { get; set; }
            public string AdminEmail { get; set; } = string.Empty;
            public string TimeAgo { get; set; } = string.Empty;
        }

        public class PendingRequestViewModel
        {
            public int Id { get; set; }
            public string UserId { get; set; } = string.Empty;
            public string UserEmail { get; set; } = string.Empty;
            public DateTime RequestedAt { get; set; }
            public string RequestedTimeAgo { get; set; } = string.Empty;
        }
    }
}