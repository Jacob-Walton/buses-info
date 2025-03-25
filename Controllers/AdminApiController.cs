using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BusInfo.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class AdminApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IApiKeyGenerator _apiKeyGenerator;

        public AdminApiController(
            ApplicationDbContext context, 
            IUserService userService,
            IApiKeyGenerator apiKeyGenerator)
        {
            _context = context;
            _userService = userService;
            _apiKeyGenerator = apiKeyGenerator;
        }

        #region Dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var userCount = await _context.Users.CountAsync();
                var pendingApiKeyRequests = await _context.ApiKeyRequests
                    .Where(r => r.Status == "Pending")
                    .CountAsync();
                var activeApiKeys = await _context.ApiKeys
                    .Where(k => k.IsActive)
                    .CountAsync();

                return Ok(new
                {
                    userCount,
                    pendingApiKeyRequests,
                    activeApiKeys,
                    lastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        #endregion

        #region API Key Management
        [HttpGet("api-key-requests")]
        public async Task<IActionResult> GetApiKeyRequests(string status = "all")
        {
            try
            {
                var query = _context.ApiKeyRequests
                    .Include(r => r.User)
                    .AsQueryable();

                if (status != "all")
                {
                    query = query.Where(r => r.Status == status);
                }

                var requests = await query
                    .OrderByDescending(r => r.RequestedAt)
                    .Select(r => new
                    {
                        r.Id,
                        r.UserId,
                        UserEmail = r.User.Email,
                        r.Reason,
                        r.IntendedUse,
                        r.Status,
                        r.RequestedAt,
                        r.UpdatedAt,
                        r.ReviewedBy,
                        r.ReviewedAt,
                        r.ReviewNotes,
                        r.RejectionReason
                    })
                    .ToListAsync();

                return Ok(requests);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("api-key-requests/{id}/review")]
        public async Task<IActionResult> ReviewApiKeyRequest(int id, [FromBody] ApiKeyRequestReview review)
        {
            try
            {
                var request = await _context.ApiKeyRequests.FindAsync(id);
                if (request == null)
                {
                    return NotFound(new { error = "API key request not found" });
                }

                request.Status = review.Status;
                request.UpdatedAt = DateTime.UtcNow;
                request.ReviewedBy = User.Identity?.Name;
                request.ReviewedAt = DateTime.UtcNow;
                request.ReviewNotes = review.Notes;
                
                if (review.Status == "Rejected")
                {
                    request.RejectionReason = review.RejectionReason;
                }
                else if (review.Status == "Approved")
                {
                    // Generate a new API key for the user using the key generator service
                    string apiKeyValue = await _apiKeyGenerator.GenerateApiKeyAsync(request.UserId);
                    
                    // The key is already saved to the database by the generator service
                    // so we don't need to create and save a new ApiKey entity
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = $"API key request {review.Status.ToLower()}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("api-keys")]
        public async Task<IActionResult> GetApiKeys()
        {
            try
            {
                var apiKeys = await _context.ApiKeys
                    .Include(k => k.User)
                    .OrderByDescending(k => k.CreatedAt)
                    .Select(k => new
                    {
                        k.Key,
                        k.UserId,
                        UserEmail = k.User.Email,
                        k.CreatedAt,
                        k.IsActive,
                        k.LastUsed
                    })
                    .ToListAsync();

                return Ok(apiKeys);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("api-keys/{key}/toggle")]
        public async Task<IActionResult> ToggleApiKey(string key)
        {
            try
            {
                var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(k => k.Key == key);
                if (apiKey == null)
                {
                    return NotFound(new { error = "API key not found" });
                }

                apiKey.IsActive = !apiKey.IsActive;
                await _context.SaveChangesAsync();

                return Ok(new { isActive = apiKey.IsActive });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("api-keys/{key}/metrics")]
        public async Task<IActionResult> GetApiKeyMetrics(string key)
        {
            try
            {
                // In a real implementation, fetch actual metrics from a database
                // This is a mock implementation
                var metrics = new ApiKeyMetrics
                {
                    TotalRequests = new Random().Next(10, 10000),
                    RequestsToday = new Random().Next(0, 100),
                    StatusCodes = new Dictionary<int, int>
                    {
                        { 200, new Random().Next(50, 900) },
                        { 400, new Random().Next(0, 50) },
                        { 404, new Random().Next(0, 20) },
                        { 500, new Random().Next(0, 10) }
                    },
                    AverageResponseTime = Math.Round(new Random().NextDouble() * 500, 2),
                    RequestsTimeSeries = GenerateTimeSeries()
                };

                return Ok(metrics);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private List<TimeSeriesDataPoint> GenerateTimeSeries()
        {
            var result = new List<TimeSeriesDataPoint>();
            var rnd = new Random();
            
            for (int i = 0; i < 24; i++)
            {
                result.Add(new TimeSeriesDataPoint
                {
                    TimeLabel = $"{i}:00",
                    Value = rnd.Next(0, 50)
                });
            }
            
            return result;
        }
        #endregion

        #region User Management
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _context.Users
                    .OrderBy(u => u.Email)
                    .Select(u => new
                    {
                        u.Id,
                        u.Email,
                        u.LockoutEnd,
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("users/{id}/toggle-lock")]
        public async Task<IActionResult> ToggleUserLock(string id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                if (user == null)
                {
                    return NotFound(new { error = "User not found" });
                }

                if (user.LockoutEnd == null || user.LockoutEnd < DateTime.UtcNow)
                {
                    // Lock the user for 30 days
                    user.LockoutEnd = DateTime.UtcNow.AddDays(30);
                    await _context.SaveChangesAsync();
                    return Ok(new { isLocked = true });
                }
                else
                {
                    // Unlock the user
                    user.LockoutEnd = null;
                    await _context.SaveChangesAsync();
                    return Ok(new { isLocked = false });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        #endregion
    }

    public class ApiKeyRequestReview
    {
        public string Status { get; set; } = "Pending";
        public string? Notes { get; set; }
        public string? RejectionReason { get; set; }
    }
}
