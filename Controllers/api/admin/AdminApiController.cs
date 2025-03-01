using System;
using System.Collections.Generic;
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
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IApiKeyGenerator _apiKeyGenerator;
        private readonly IRequestTrackingService _requestTracking;
        private readonly ILogger<AdminApiController> _logger;
        private readonly IEmailService _emailService;
        
        public AdminApiController(
            ApplicationDbContext context,
            IApiKeyGenerator apiKeyGenerator,
            IRequestTrackingService requestTracking,
            ILogger<AdminApiController> logger,
            IEmailService emailService)
        {
            _context = context;
            _apiKeyGenerator = apiKeyGenerator;
            _requestTracking = requestTracking;
            _logger = logger;
            _emailService = emailService;
        }
        
        #region Dashboard Metrics
        
        [HttpGet("metrics")]
        public async Task<ActionResult<AdminMetrics>> GetMetrics()
        {
            try
            {
                var requestMetrics = await _requestTracking.GetMetricsAsync();
                var userCount = await _context.Users.CountAsync();
                var activeApiKeys = await _context.ApiKeys.CountAsync(k => k.IsActive);
                var pendingRequests = await _context.ApiKeyRequests.CountAsync(r => r.Status == "Pending");

                var metrics = new AdminMetrics
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
        public async Task<ActionResult<SystemHealth>> GetSystemHealth()
        {
            try
            {
                var health = new SystemHealth
                {
                    CpuUsage = GetCpuUsage(),
                    MemoryUsage = GetMemoryUsage(),
                    DiskUsage = GetDiskUsage(),
                    ActiveConnections = GetActiveConnections(),
                    Services = new Dictionary<string, ServiceStatus>
                    {
                        ["Database"] = await CheckDatabaseHealthAsync(),
                        ["Redis"] = await CheckRedisHealthAsync(),
                        ["BusInfoService"] = await CheckBusInfoServiceAsync()
                    },
                    Performance = await GetPerformanceMetricsAsync()
                };

                return Ok(health);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system health");
                return StatusCode(500, new { message = "Error retrieving system health" });
            }
        }
        
        private double GetCpuUsage()
        {
            // Simplified implementation - would use a real system monitoring library in production
            return new Random().Next(10, 80);
        }
        
        private double GetMemoryUsage()
        {
            // Simplified implementation
            return new Random().Next(30, 75);
        }
        
        private double GetDiskUsage()
        {
            // Simplified implementation
            return new Random().Next(20, 90);
        }
        
        private int GetActiveConnections()
        {
            // Simplified implementation
            return new Random().Next(1, 50);
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
        
        private async Task<ServiceStatus> CheckRedisHealthAsync()
        {
            try
            {
                // Use request tracking service which uses Redis
                await _requestTracking.GetMetricsAsync();
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
        
        private async Task<List<PerformanceMetric>> GetPerformanceMetricsAsync()
        {
            // In a real implementation, would retrieve actual system metrics
            return new List<PerformanceMetric>
            {
                new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "API Response Time",
                    Value = (await _requestTracking.GetMetricsAsync()).AverageResponseTime,
                    Unit = "ms",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "service", "api" } }
                },
                new PerformanceMetric
                {
                    Timestamp = DateTime.UtcNow,
                    MetricName = "Active Database Connections",
                    Value = new Random().Next(1, 10),
                    Unit = "connections",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "service", "database" } }
                }
            };
        }
        
        #endregion
        
        #region API Key Management
        
        // Get all API keys
        [HttpGet("api-keys")]
        public async Task<ActionResult<IEnumerable<ApiKeyInfo>>> GetApiKeys()
        {
            try
            {
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
                            ExpiresAt = key.CreatedAt.AddYears(1), // Assuming 1 year expiration
                            IsActive = key.IsActive,
                            LastUsed = key.LastUsed,
                            RequestsToday = 0, // Will be populated from metrics
                            TotalRequests = 0  // Will be populated from metrics
                        })
                    .ToListAsync();
                    
                // Enhance with metrics data
                foreach (var key in keys)
                {
                    var metrics = await _requestTracking.GetApiKeyMetricsAsync(key.Key);
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
        
        // Get API key usage details
        [HttpGet("api-keys/{key}/usage")]
        public async Task<ActionResult<ApiKeyMetrics>> GetApiKeyMetrics(string key)
        {
            try
            {
                var apiKey = await _context.ApiKeys.FindAsync(key);
                if (apiKey == null)
                    return NotFound(new { message = "API key not found" });
                    
                var metrics = await _requestTracking.GetApiKeyMetricsAsync(key);
                
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API key metrics for {Key}", key);
                return StatusCode(500, new { message = "Error retrieving API key metrics" });
            }
        }
        
        // Revoke an API key
        [HttpPost("api-keys/{key}/revoke")]
        public async Task<IActionResult> RevokeApiKey(string key)
        {
            try
            {
                var apiKey = await _context.ApiKeys.FindAsync(key);
                if (apiKey == null)
                    return NotFound(new { message = "API key not found" });
                    
                // Deactivate the key
                apiKey.IsActive = false;
                
                await _context.SaveChangesAsync();
                
                // Log admin activity
                var activity = new AdminActivity
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
        
        // Regenerate API key for a user
        [HttpPost("api-keys/{userId}/regenerate")]
        public async Task<IActionResult> RegenerateApiKey(string userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                
                // Get existing API keys for this user
                var existingKeys = await _context.ApiKeys
                    .Where(k => k.UserId == userId && k.IsActive)
                    .ToListAsync();
                
                // Deactivate all existing keys
                foreach (var key in existingKeys)
                {
                    key.IsActive = false;
                }
                
                // Generate a new key
                var newKey = await _apiKeyGenerator.GenerateApiKeyAsync(userId);
                
                // Log admin activity
                var activity = new AdminActivity
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
                        ["oldKeyCount"] = existingKeys.Count.ToString()
                    }
                };
                
                _context.AdminActivities.Add(activity);
                await _context.SaveChangesAsync();
                
                return Ok(new { 
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
        
        #endregion
        
        #region API Request Management
        
        // Get all pending API key requests
        [HttpGet("api-requests")]
        public async Task<ActionResult<IEnumerable<ApiKeyRequest>>> GetPendingRequests()
        {
            try
            {
                var requests = await _context.ApiKeyRequests
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
        
        // Approve an API key request
        [HttpPost("api-requests/{id}/approve")]
        public async Task<IActionResult> ApproveRequest(int id)
        {
            try
            {
                var request = await _context.ApiKeyRequests.FindAsync(id);
                if (request == null)
                    return NotFound(new { message = "Request not found" });
                    
                if (request.Status != "Pending")
                    return BadRequest(new { message = "Request is not in pending state" });
                
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                    
                // Update request status
                request.Status = "Approved";
                request.ReviewedBy = User.Identity?.Name;
                request.ReviewedAt = DateTime.UtcNow;
                
                // Generate API key for the user
                string apiKey = await _apiKeyGenerator.GenerateApiKeyAsync(request.UserId);
                
                // Log admin activity
                var activity = new AdminActivity
                {
                    Description = $"Approved API key request for {user.Email}",
                    Timestamp = DateTime.UtcNow,
                    Type = "ApiKeyRequest",
                    Icon = "check-circle",
                    AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    UserId = request.UserId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["requestId"] = id.ToString(),
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
                
                return Ok(new { 
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
        
        // Reject an API key request
        [HttpPost("api-requests/{id}/reject")]
        public async Task<IActionResult> RejectRequest(int id, [FromBody] RejectRequestModel model)
        {
            try
            {
                var request = await _context.ApiKeyRequests.FindAsync(id);
                if (request == null)
                    return NotFound(new { message = "Request not found" });
                    
                if (request.Status != "Pending")
                    return BadRequest(new { message = "Request is not in pending state" });
                    
                if (string.IsNullOrWhiteSpace(model.Reason))
                    return BadRequest(new { message = "Rejection reason is required" });
                
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null)
                    return NotFound(new { message = "User not found" });
                    
                // Update request status
                request.Status = "Rejected";
                request.ReviewedBy = User.Identity?.Name;
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewNotes = model.Reason;
                
                // Log admin activity
                var activity = new AdminActivity
                {
                    Description = $"Rejected API key request for {user.Email}",
                    Timestamp = DateTime.UtcNow,
                    Type = "ApiKeyRequest",
                    Icon = "times-circle",
                    AdminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    UserId = request.UserId,
                    Metadata = new Dictionary<string, string>
                    {
                        ["requestId"] = id.ToString(),
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
        
        #endregion
        
        #region API Usage Statistics
        
        // Get API usage statistics
        [HttpGet("api-stats/usage")]
        public async Task<ActionResult<ApiUsageStats>> GetApiUsageStats()
        {
            try
            {
                var metrics = await _requestTracking.GetMetricsAsync();
                
                var activeKeysCount = await _context.ApiKeys.CountAsync(k => k.IsActive);
                
                // Calculate keys expiring soon (within 30 days)
                var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
                var keysExpiringSoon = await _context.ApiKeys
                    .CountAsync(k => k.IsActive && k.CreatedAt.AddYears(1) < thirtyDaysFromNow);
                
                // Get top endpoint
                var topEndpoints = await _requestTracking.GetTopEndpointsAsync(1);
                string topEndpoint = topEndpoints.Count > 0 ? topEndpoints[0].Endpoint : "None";
                    
                var stats = new ApiUsageStats
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
        
        // Get status code distribution
        [HttpGet("api-stats/status-codes")]
        public async Task<ActionResult<Dictionary<int, int>>> GetStatusCodeDistribution()
        {
            try
            {
                var distribution = await _requestTracking.GetStatusCodeDistributionAsync();
                return Ok(distribution);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving status code distribution");
                return StatusCode(500, new { message = "Error retrieving status code distribution" });
            }
        }
        
        // Get top endpoints
        [HttpGet("api-stats/top-endpoints")]
        public async Task<ActionResult<List<EndpointMetrics>>> GetTopEndpoints([FromQuery] int count = 5)
        {
            try
            {
                var endpoints = await _requestTracking.GetTopEndpointsAsync(count);
                return Ok(endpoints);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving top endpoints");
                return StatusCode(500, new { message = "Error retrieving top endpoints" });
            }
        }
        
        // Get hourly request breakdown for today
        [HttpGet("api-stats/hourly")]
        public async Task<ActionResult<Dictionary<string, int>>> GetHourlyRequests([FromQuery] string date = null)
        {
            try
            {
                var hourlyData = await _requestTracking.GetHourlyRequestsAsync(date);
                return Ok(hourlyData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hourly requests");
                return StatusCode(500, new { message = "Error retrieving hourly requests" });
            }
        }
        
        // Get comprehensive API dashboard data
        [HttpGet("api-stats/dashboard")]
        public async Task<ActionResult<ApiDashboardData>> GetDashboardData()
        {
            try
            {
                var metrics = await _requestTracking.GetMetricsAsync();
                var statusCodes = await _requestTracking.GetStatusCodeDistributionAsync();
                var topEndpoints = await _requestTracking.GetTopEndpointsAsync(5);
                var hourlyData = await _requestTracking.GetHourlyRequestsAsync();
                
                // Count active API keys
                var activeKeysCount = await _context.ApiKeys.CountAsync(k => k.IsActive);
                
                // Calculate keys expiring soon (within 30 days)
                var thirtyDaysFromNow = DateTime.UtcNow.AddDays(30);
                var keysExpiringSoon = await _context.ApiKeys
                    .CountAsync(k => k.IsActive && k.CreatedAt.AddYears(1) < thirtyDaysFromNow);
                
                var dashboardData = new ApiDashboardData
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
        
        #endregion
    }
    
    public class RejectRequestModel
    {
        public string Reason { get; set; } = string.Empty;
    }
}