using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusInfo.Models.Admin;
using BusInfo.Services;
using BusInfo.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace BusInfo.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminIndexModel : PageModel
    {
        private readonly IRequestTrackingService _requestTracking;
        private readonly ApplicationDbContext _context;

        public AdminMetrics Metrics { get; private set; } = new();
        public SystemHealth Health { get; private set; } = new();
        public List<AdminActivity> RecentActivities { get; private set; } = [];
        public int TodayNewUsers { get; private set; }

        public AdminIndexModel(IRequestTrackingService requestTracking, ApplicationDbContext context)
        {
            _requestTracking = requestTracking;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            // Get metrics
            var requestMetrics = await _requestTracking.GetMetricsAsync();
            var userCount = await _context.Users.CountAsync();
            var activeApiKeys = await _context.ApiKeys.CountAsync(k => k.IsActive);
            var pendingRequests = await _context.ApiKeyRequests.CountAsync(r => r.Status == "Pending");

            Metrics = new AdminMetrics
            {
                TotalUsers = userCount,
                ActiveApiKeys = activeApiKeys,
                PendingApiRequests = pendingRequests,
                TotalApiRequests = requestMetrics.TotalRequests,
                ApiRequests24Hours = requestMetrics.Requests24Hours,
                AverageResponseTime = requestMetrics.AverageResponseTime,
                ErrorCount = requestMetrics.ErrorCount
            };

            // Get today's new users
            TodayNewUsers = await _context.Users
                .CountAsync(u => u.CreatedAt.Date == DateTime.UtcNow.Date);

            // Get system health
            Health = new SystemHealth
            {
                Services = new Dictionary<string, ServiceStatus>
                {
                    ["Database"] = await CheckDatabaseHealthAsync(),
                    ["Redis"] = await CheckRedisServiceAsync(),
                    ["BusInfoService"] = await CheckBusInfoServiceAsync()
                }
            };

            // Get recent activities
            RecentActivities = await _context.AdminActivities
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .ToListAsync();
        }

        private async Task<ServiceStatus> CheckDatabaseHealthAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                return ServiceStatus.Healthy;
            }
            catch
            {
                return ServiceStatus.Unhealthy;
            }
        }

        private async Task<ServiceStatus> CheckRedisServiceAsync()
        {
            try
            {
                var redis = HttpContext.RequestServices.GetRequiredService<IRedisService>();
                await redis.KeyExistsAsync("health_check");
                return ServiceStatus.Healthy;
            }
            catch
            {
                return ServiceStatus.Unhealthy;
            }
        }

        private async Task<ServiceStatus> CheckBusInfoServiceAsync()
        {
            try
            {
                var busInfoService = HttpContext.RequestServices.GetRequiredService<IBusInfoService>();
                await busInfoService.GetBusInfoAsync();
                return ServiceStatus.Healthy;
            }
            catch
            {
                return ServiceStatus.Unhealthy;
            }
        }
    }
}