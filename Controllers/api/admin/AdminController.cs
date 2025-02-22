using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Models.Admin;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace BusInfo.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IRequestTrackingService _requestTracking;
        private readonly IRedisService _redis;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            IRequestTrackingService requestTracking,
            IRedisService redis,
            ILogger<AdminController> logger)
        {
            _context = context;
            _requestTracking = requestTracking;
            _redis = redis;
            _logger = logger;
        }

        [HttpGet("metrics")]
        public async Task<ActionResult<AdminMetrics>> GetMetricsAsync()
        {
            var requestMetrics = await _requestTracking.GetMetricsAsync();
            var userCount = await _context.Users.CountAsync();
            var activeApiKeys = await _context.ApiKeys.CountAsync(k => k.IsActive);
            var pendingRequests = await _context.ApiKeyRequests.CountAsync(r => r.Status == "Pending");

            return new AdminMetrics
            {
                TotalUsers = userCount,
                ActiveApiKeys = activeApiKeys,
                PendingApiRequests = pendingRequests,
                TotalApiRequests = requestMetrics.TotalRequests,
                ApiRequests24Hours = requestMetrics.Requests24Hours,
                AverageResponseTime = requestMetrics.AverageResponseTime,
                ErrorCount = requestMetrics.ErrorCount
            };
        }

        [HttpGet("settings")]
        public async Task<ActionResult<AdminSettings>> GetSettingsAsync()
        {
            var settings = await _context.AdminSettings
                .OrderByDescending(s => s.LastModified)
                .FirstOrDefaultAsync();

            return settings ?? new AdminSettings();
        }

        [HttpPut("settings")]
        public async Task<IActionResult> UpdateSettingsAsync(AdminSettings settings)
        {
            settings.LastModified = DateTime.UtcNow;
            settings.ModifiedBy = User.FindFirst(ClaimTypes.Email)?.Value ?? "unknown";

            _context.AdminSettings.Add(settings);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin settings updated by {AdminUser}", settings.ModifiedBy);
            return Ok();
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<AdminUserInfo>>> GetUsersAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var users = await _context.Users
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserInfo
                {
                    Id = u.Id,
                    Email = u.Email,
                    CreatedAt = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                    IsAdmin = u.IsAdmin,
                    HasApiAccess = u.ActiveApiKey != null,
                    IsPendingDeletion = u.IsPendingDeletion,
                    PreferredRoutesCount = u.PreferredRoutes.Count,
                    FailedLoginAttempts = u.FailedLoginAttempts,
                    IsLocked = u.IsLocked
                })
                .ToListAsync();

            Response.Headers["X-Total-Count"] = (await _context.Users.CountAsync()).ToString();
            return users;
        }

        [HttpGet("api/requests")]
        public async Task<ActionResult<IEnumerable<ApiKeyRequest>>> GetApiRequestsAsync([FromQuery] string status = "Pending")
        {
            return await _context.ApiKeyRequests
                .Where(r => r.Status == status)
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestedAt)
                .Take(100)
                .ToListAsync();
        }

        [HttpPost("api/requests/{id}/approve")]
        public async Task<IActionResult> ApproveApiRequestAsync(int id, [FromBody] ApiRequestDecision decision)
        {
            var request = await _context.ApiKeyRequests.FindAsync(id);
            if (request == null) return NotFound();

            request.Status = "Approved";
            request.ReviewedBy = User.FindFirst(ClaimTypes.Email)?.Value;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = decision.Notes;

            // Generate new API key
            var apiKeyGenerator = HttpContext.RequestServices.GetRequiredService<IApiKeyGenerator>();
            var newKey = await apiKeyGenerator.GenerateApiKeyAsync(request.UserId);

            _logger.LogInformation("API request {RequestId} approved by {AdminUser}", id, request.ReviewedBy);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Request approved", apiKey = newKey });
        }

        [HttpPost("api/requests/{id}/reject")]
        public async Task<IActionResult> RejectApiRequestAsync(int id, [FromBody] ApiRequestDecision decision)
        {
            var request = await _context.ApiKeyRequests.FindAsync(id);
            if (request == null) return NotFound();

            request.Status = "Rejected";
            request.ReviewedBy = User.FindFirst(ClaimTypes.Email)?.Value;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = decision.Notes;

            _logger.LogInformation("API request {RequestId} rejected by {AdminUser}", id, request.ReviewedBy);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("api/keys")]
        public async Task<ActionResult<IEnumerable<ApiKeyInfo>>> GetApiKeysAsync([FromQuery] bool activeOnly = true)
        {
            var query = _context.ApiKeys.AsQueryable();
            if (activeOnly) query = query.Where(k => k.IsActive);

            return await query
                .Include(k => k.User)
                .OrderByDescending(k => k.CreatedAt)
                .Take(100)
                .Select(k => new ApiKeyInfo
                {
                    Key = k.Key,
                    UserId = k.UserId,
                    UserEmail = k.User.Email,
                    CreatedAt = k.CreatedAt,
                    ExpiresAt = k.CreatedAt.AddDays(365), // Get from settings
                    IsActive = k.IsActive,
                    LastUsed = k.LastUsed
                })
                .ToListAsync();
        }

        [HttpPost("api/keys/{key}/revoke")]
        public async Task<IActionResult> RevokeApiKeyAsync(string key)
        {
            var apiKey = await _context.ApiKeys.FindAsync(key);
            if (apiKey == null) return NotFound();

            apiKey.IsActive = false;
            _logger.LogInformation("API key revoked by {AdminUser}", User.FindFirst(ClaimTypes.Email)?.Value);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("health")]
        public async Task<ActionResult<SystemHealth>> GetSystemHealthAsync()
        {
            var health = new SystemHealth
            {
                Services = new Dictionary<string, ServiceStatus>
                {
                    ["Database"] = await CheckDatabaseHealthAsync(),
                    ["Redis"] = await CheckRedisHealthAsync(),
                    ["BusInfoService"] = await CheckBusInfoServiceHealthAsync()
                }
            };

            // Add performance metrics
            health.Performance = await GetPerformanceMetricsAsync();

            return health;
        }

        private async Task<ServiceStatus> CheckDatabaseHealthAsync()
        {
            try
            {
                await _context.Database.CanConnectAsync();
                return ServiceStatus.Healthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return ServiceStatus.Unhealthy;
            }
        }

        private async Task<ServiceStatus> CheckRedisHealthAsync()
        {
            try
            {
                await _redis.KeyExistsAsync("health_check");
                return ServiceStatus.Healthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return ServiceStatus.Unhealthy;
            }
        }

        private async Task<ServiceStatus> CheckBusInfoServiceHealthAsync()
        {
            try
            {
                var busInfoService = HttpContext.RequestServices.GetRequiredService<IBusInfoService>();
                await busInfoService.GetBusInfoAsync();
                return ServiceStatus.Healthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bus info service health check failed");
                return ServiceStatus.Unhealthy;
            }
        }

        private async Task<List<PerformanceMetric>> GetPerformanceMetricsAsync()
        {
            var metrics = new List<PerformanceMetric>();

            // Add CPU usage
            metrics.Add(new PerformanceMetric
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "CPU Usage",
                Value = await GetCpuUsageAsync(),
                Unit = "%",
                Type = MetricType.Gauge
            });

            // Add memory usage
            metrics.Add(new PerformanceMetric
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "Memory Usage",
                Value = await GetMemoryUsageAsync(),
                Unit = "MB",
                Type = MetricType.Gauge
            });

            return metrics;
        }

        private Task<double> GetCpuUsageAsync()
        {
            // Implement CPU usage check
            return Task.FromResult(Environment.ProcessorCount * 100.0);
        }

        private Task<double> GetMemoryUsageAsync()
        {
            // Implement memory usage check
            var process = Process.GetCurrentProcess();
            return Task.FromResult(process.WorkingSet64 / 1024.0 / 1024.0);
        }
    }

    public class ApiRequestDecision
    {
        public string? Notes { get; set; }
    }
}