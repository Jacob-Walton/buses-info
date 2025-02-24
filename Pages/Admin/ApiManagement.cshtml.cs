using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BusInfo.Models.Admin;
using BusInfo.Services;
using BusInfo.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class ApiManagementModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IRequestTrackingService _requestTracking;
        
        public List<ApiKeyRequest> PendingRequests { get; private set; } = [];
        public List<ApiKeyInfo> ActiveKeys { get; private set; } = [];
        public ApiUsageStats UsageStats { get; private set; } = new();

        public ApiManagementModel(ApplicationDbContext context, IRequestTrackingService requestTracking)
        {
            _context = context;
            _requestTracking = requestTracking;
        }

        public async Task OnGetAsync()
        {
            // Get pending requests
            PendingRequests = await _context.ApiKeyRequests
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            // Get active keys
            ActiveKeys = await GetActiveKeys();

            // Get API usage stats
            var requestMetrics = await _requestTracking.GetMetricsAsync();
            
            UsageStats = new ApiUsageStats
            {
                ActiveKeys = ActiveKeys.Count(k => k.IsActive),
                KeysExpiringSoon = ActiveKeys.Count(k => k.ExpiresAt < DateTime.UtcNow.AddDays(30) && k.IsActive),
                TotalRequests = requestMetrics.TotalRequests,
                Requests24Hours = requestMetrics.Requests24Hours,
                AverageResponseTime = requestMetrics.AverageResponseTime,
                ErrorCount = requestMetrics.ErrorCount,
                TopEndpoint = GetTopEndpoint()
            };
        }

        private async Task<List<ApiKeyInfo>> GetActiveKeys()
        {
            return await _context.ApiKeys
                .Include(k => k.User)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new ApiKeyInfo
                {
                    Key = k.Key,
                    UserId = k.UserId,
                    UserEmail = k.User.Email,
                    CreatedAt = k.CreatedAt,
                    ExpiresAt = k.CreatedAt.AddDays(365), // This should come from settings
                    IsActive = k.IsActive,
                    LastUsed = k.LastUsed
                })
                .ToListAsync();
        }

        private string GetTopEndpoint()
        {
            // In a real implementation, this would query your endpoint usage data
            // For now returning a placeholder
            return "GET /api/businfo";
        }
    }
}