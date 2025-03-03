using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Models.Admin;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusInfo.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminApiController(
        ApplicationDbContext context,
        IApiKeyGenerator apiKeyGenerator,
        IRequestTrackingService requestTracking,
        ILogger<AdminApiController> logger,
        IHealthCheckService healthCheckService) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IApiKeyGenerator _apiKeyGenerator = apiKeyGenerator;
        private readonly IRequestTrackingService _requestTracking = requestTracking;
        private readonly ILogger<AdminApiController> _logger = logger;
        private readonly IHealthCheckService _healthCheckService = healthCheckService;

        #region Dashboard Metrics

        [HttpGet("metrics")]
        public async Task<ActionResult<AdminMetrics>> GetMetricsAsync()
        {
            try
            {
                RequestMetrics requestMetrics = await _requestTracking.GetMetricsAsync();
                int userCount = await (_context.Users ?? throw new InvalidOperationException("Users DbSet is null")).CountAsync();
                int activeApiKeys = await (_context.ApiKeys ?? throw new InvalidOperationException("ApiKeys DbSet is null")).CountAsync(k => k.IsActive);
                int pendingRequests = await (_context.ApiKeyRequests ?? throw new InvalidOperationException("ApiKeyRequests DbSet is null")).CountAsync(r => r.Status == "Pending");

                AdminMetrics metrics = new()
                {
                    TotalUsers = userCount,
                    ActiveApiKeys = activeApiKeys,
                    PendingApiRequests = pendingRequests,
                    TotalApiRequests = requestMetrics.TotalRequests,
                    ApiRequests24Hours = requestMetrics.Requests24Hours,
                    AverageResponseTime = requestMetrics.AverageResponseTime,
                    ErrorCount = requestMetrics.ErrorCount
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving admin metrics");
                return StatusCode(500, new { message = "Error retrieving metrics" });
            }
        }

        [HttpGet("health")]
        public async Task<ActionResult<SystemHealth>> GetSystemHealthAsync()
        {
            return await _healthCheckService.GetSystemHealthAsync();
        }
        #endregion Dashboard Metrics

        #region API Key Management

        [HttpGet("api-keys")]
        public async Task<ActionResult<IEnumerable<ApiKeyInfo>>> GetApiKeysAsync()
        {
            try
            {
                List<ApiKeyInfo> keys = await _context.ApiKeys
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
                            ExpiresAt = key.CreatedAt.AddYears(1), // Assuming 1 year expiration
                            IsActive = key.IsActive,
                            LastUsed = key.LastUsed,
                            RequestsToday = 0, // Will be populated from metrics
                            TotalRequests = 0  // Will be populated from metrics
                        })
                    .ToListAsync();

                // Enhance with metrics data
                foreach (ApiKeyInfo? key in keys)
                {
                    ApiKeyMetrics metrics = await _requestTracking.GetApiKeyMetricsAsync(key.Key);
                    key.RequestsToday = metrics.RequestsToday;
                    key.TotalRequests = metrics.TotalRequests;
                }

                return Ok(keys);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API keys");
                return StatusCode(500, new { message = "Error retrieving API keys" });
            }
        }

        [HttpGet("api-keys/{key}/usage")]
        public async Task<ActionResult<ApiKeyMetrics>> GetApiKeyMetricsAsync(string key)
        {
            try
            {
                ApiKey? apiKey = await _context.ApiKeys.FindAsync(key);
                if (apiKey == null)
                    return NotFound(new { message = "API key not found" });

                ApiKeyMetrics metrics = await _requestTracking.GetApiKeyMetricsAsync(apiKey.Key);

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API key metrics for {Key}", key);
                return StatusCode(500, new { message = "Error retrieving API key metrics" });
            }
        }

        [HttpPost("api-keys/{key}/revoke")]
        public async Task<IActionResult> RevokeApiKeyAsync(string key)
        {
            try
            {
                ApiKey? apiKey = await _context.ApiKeys.FindAsync(key);
                if (apiKey == null)
                    return NotFound(new { message = "API key not found" });

                // Deactivate the key
                apiKey.IsActive = false;

                await _context.SaveChangesAsync();

                // Log admin activity
                AdminActivity activity = new()
                {
                    Description = $"Revoked API key for user {apiKey.UserId}",
                    Timestamp = DateTime.UtcNow,
                    Type = "ApiKey",
                    Icon = "key",
                    AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    UserId = apiKey.UserId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["keyId"] = key,
                        ["action"] = "revoke"
                    }
                };

                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();

                return Ok(new { message = "API key revoked successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking API key {Key}", key);
                return StatusCode(500, new { message = "Error revoking API key" });
            }
        }

        [HttpPost("api-keys/{userId}/regenerate")]
        public async Task<IActionResult> RegenerateApiKeyAsync(string userId)
        {
            try
            {
                ApplicationUser? user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Get existing API keys for this user
                List<ApiKey> existingKeys = await _context.ApiKeys
                    .Where(k => k.UserId == userId && k.IsActive)
                    .ToListAsync();

                // Deactivate all existing keys
                foreach (ApiKey? key in existingKeys)
                {
                    key.IsActive = false;
                }

                // Generate a new key
                string newKey = await _apiKeyGenerator.GenerateApiKeyAsync(userId);

                // Log admin activity
                AdminActivity activity = new()
                {
                    Description = $"Regenerated API key for user {user.Email}",
                    Timestamp = DateTime.UtcNow,
                    Type = "ApiKey",
                    Icon = "sync",
                    AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    UserId = userId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["action"] = "regenerate",
                        ["oldKeyCount"] = existingKeys.Count.ToString(CultureInfo.InvariantCulture)
                    }
                };

                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "API key regenerated successfully",
                    key = newKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerating API key for user {UserId}", userId);
                return StatusCode(500, new { message = "Error regenerating API key" });
            }
        }

        #endregion API Key Management

        #region API Request Management

        [HttpGet("api-requests")]
        public async Task<ActionResult<IEnumerable<ApiKeyRequest>>> GetPendingRequestsAsync()
        {
            try
            {
                List<ApiKeyRequest> requests = await _context.ApiKeyRequests
                    .Where(r => r.Status == "Pending")
                    .Include(r => r.User)
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API key requests");
                return StatusCode(500, new { message = "Error retrieving API key requests" });
            }
        }

        [HttpPost("api-requests/{id}/approve")]
        public async Task<IActionResult> ApproveRequestAsync(int id)
        {
            try
            {
                ApiKeyRequest? request = await _context.ApiKeyRequests.FindAsync(id);
                if (request == null)
                    return NotFound(new { message = "Request not found" });

                if (request.Status != "Pending")
                    return BadRequest(new { message = "Request is not in pending state" });

                ApplicationUser? user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Update request status
                request.Status = "Approved";
                request.ReviewedBy = User.Identity?.Name;
                request.ReviewedAt = DateTime.UtcNow;

                // Generate API key for the user
                string apiKey = await _apiKeyGenerator.GenerateApiKeyAsync(request.UserId);

                // Log admin activity
                AdminActivity activity = new()
                {
                    Description = $"Approved API key request for {user.Email}",
                    Timestamp = DateTime.UtcNow,
                    Type = "ApiKeyRequest",
                    Icon = "check-circle",
                    AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    UserId = request.UserId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["requestId"] = id.ToString(CultureInfo.InvariantCulture),
                        ["action"] = "approve"
                    }
                };

                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();

                // Send email notification
                if (user.EnableEmailNotifications)
                {
                    // In a real implementation, send an email to notify the user
                    // _emailService.SendApiKeyApprovalEmail(user.Email, apiKey);
                }

                return Ok(new
                {
                    message = "API key request approved successfully",
                    apiKey
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving API key request {RequestId}", id);
                return StatusCode(500, new { message = "Error approving API key request" });
            }
        }

        [HttpPost("api-requests/{id}/reject")]
        public async Task<IActionResult> RejectRequestAsync(int id, [FromBody] RejectRequestModel model)
        {
            try
            {
                ApiKeyRequest? request = await _context.ApiKeyRequests.FindAsync(id);
                if (request == null)
                    return NotFound(new { message = "Request not found" });

                if (request.Status != "Pending")
                    return BadRequest(new { message = "Request is not in pending state" });

                if (string.IsNullOrWhiteSpace(model.Reason))
                    return BadRequest(new { message = "Rejection reason is required" });

                ApplicationUser? user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Update request status
                request.Status = "Rejected";
                request.ReviewedBy = User.Identity?.Name;
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewNotes = model.Reason;

                // Log admin activity
                AdminActivity activity = new()
                {
                    Description = $"Rejected API key request for {user.Email}",
                    Timestamp = DateTime.UtcNow,
                    Type = "ApiKeyRequest",
                    Icon = "times-circle",
                    AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    UserId = request.UserId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["requestId"] = id.ToString(CultureInfo.InvariantCulture),
                        ["action"] = "reject",
                        ["reason"] = model.Reason
                    }
                };

                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();

                // Send email notification
                if (user.EnableEmailNotifications)
                {
                    // In a real implementation, send an email to notify the user
                    // _emailService.SendApiKeyRejectionEmail(user.Email, model.Reason);
                }

                return Ok(new { message = "API key request rejected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting API key request {RequestId}", id);
                return StatusCode(500, new { message = "Error rejecting API key request" });
            }
        }

        #endregion API Request Management

        #region API Usage Statistics

        [HttpGet("api-stats/usage")]
        public async Task<ActionResult<ApiUsageStats>> GetApiUsageStatsAsync()
        {
            try
            {
                RequestMetrics metrics = await _requestTracking.GetMetricsAsync();

                int activeKeysCount = await _context.ApiKeys.CountAsync(k => k.IsActive);

                // Calculate keys expiring soon (within 30 days)
                DateTime thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
                int keysExpiringSoon = await _context.ApiKeys
                    .CountAsync(k => k.IsActive && k.CreatedAt.AddYears(1) < thirtyDaysFromNow);

                // Get top endpoint
                List<EndpointMetrics> topEndpoints = await _requestTracking.GetTopEndpointsAsync(1);
                string topEndpoint = topEndpoints.Count > 0 ? topEndpoints[0].Endpoint : "None";

                ApiUsageStats stats = new()
                {
                    ActiveKeys = activeKeysCount,
                    KeysExpiringSoon = keysExpiringSoon,
                    TotalRequests = (int)metrics.TotalRequests,
                    Requests24Hours = (int)metrics.Requests24Hours,
                    AverageResponseTime = metrics.AverageResponseTime,
                    ErrorCount = metrics.ErrorCount,
                    TopEndpoint = topEndpoint
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API usage statistics");
                return StatusCode(500, new { message = "Error retrieving API usage statistics" });
            }
        }

        [HttpGet("api-stats/status-codes")]
        public async Task<ActionResult<Dictionary<int, int>>> GetStatusCodeDistributionAsync()
        {
            try
            {
                Dictionary<int, int> distribution = await _requestTracking.GetStatusCodeDistributionAsync();
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving status code distribution");
                return StatusCode(500, new { message = "Error retrieving status code distribution" });
            }
        }

        [HttpGet("api-stats/top-endpoints")]
        public async Task<ActionResult<List<EndpointMetrics>>> GetTopEndpointsAsync([FromQuery] int count = 5)
        {
            try
            {
                List<EndpointMetrics> endpoints = await _requestTracking.GetTopEndpointsAsync(count);
                return Ok(endpoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top endpoints");
                return StatusCode(500, new { message = "Error retrieving top endpoints" });
            }
        }

        [HttpGet("api-stats/hourly")]
        public async Task<ActionResult<Dictionary<string, int>>> GetHourlyRequestsAsync([FromQuery] string? date = null)
        {
            try
            {
                Dictionary<string, int> hourlyData = await _requestTracking.GetHourlyRequestsAsync(date);
                return Ok(hourlyData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hourly requests");
                return StatusCode(500, new { message = "Error retrieving hourly requests" });
            }
        }

        [HttpGet("api-stats/dashboard")]
        public async Task<ActionResult<ApiDashboardData>> GetDashboardDataAsync()
        {
            try
            {
                RequestMetrics metrics = await _requestTracking.GetMetricsAsync();
                Dictionary<int, int> statusCodes = await _requestTracking.GetStatusCodeDistributionAsync();
                List<EndpointMetrics> topEndpoints = await _requestTracking.GetTopEndpointsAsync(5);
                Dictionary<string, int> hourlyData = await _requestTracking.GetHourlyRequestsAsync();

                // Count active API keys
                int activeKeysCount = await _context.ApiKeys.CountAsync(k => k.IsActive);

                // Calculate keys expiring soon (within 30 days)
                DateTime thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
                int keysExpiringSoon = await _context.ApiKeys
                    .CountAsync(k => k.IsActive && k.CreatedAt.AddYears(1) < thirtyDaysFromNow);

                ApiDashboardData dashboardData = new()
                {
                    ActiveKeys = activeKeysCount,
                    KeysExpiringSoon = keysExpiringSoon,
                    TotalRequests = metrics.TotalRequests,
                    Requests24Hours = metrics.Requests24Hours,
                    AverageResponseTime = metrics.AverageResponseTime,
                    ErrorCount = metrics.ErrorCount,
                    StatusCodeDistribution = statusCodes,
                    TopEndpoints = topEndpoints,
                    HourlyRequests = hourlyData
                };

                return Ok(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard data");
                return StatusCode(500, new { message = "Error retrieving dashboard data" });
            }
        }

        #endregion API Usage Statistics
    }

    public class RejectRequestModel
    {
        public string Reason { get; set; } = string.Empty;
    }
}