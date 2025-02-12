using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusInfo.Models.Accounts;
using System.Collections.Generic;

namespace BusInfo.Controllers
{
    [ApiController]
    [Route("api/accounts")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class AccountController(ApplicationDbContext context, IUserService userService, IApiKeyGenerator apiKeyGenerator) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IUserService _userService = userService;
        private readonly IApiKeyGenerator _apiKeyGenerator = apiKeyGenerator;

        // Account CRUD Operations
        [HttpGet]
        public async Task<IActionResult> GetAccountAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportAccountAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            byte[] data = await _userService.ExportUserDataAsync(userId);
            return File(data, "application/json", "user-data.json");
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAccountAsync([FromBody] DeleteAccountModel model)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            bool result = await _userService.DeleteAccountAsync(userId, model.Password);
            if (!result) return BadRequest(new { message = "Invalid password" });

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Account deleted successfully" });
        }

        [HttpPost("logout")]
        public async Task<ActionResult> LogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

        // Preferences CRUD Operations
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferencesAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            UserPreferences? preferences = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserPreferences
                {
                    PreferredRoutes = u.PreferredRoutes ?? new(),
                    ShowPreferredRoutesFirst = u.ShowPreferredRoutesFirst,
                    EnableEmailNotifications = u.EnableEmailNotifications
                })
                .FirstOrDefaultAsync();

            return preferences == null ? NotFound() : Ok(preferences);
        }

        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferencesAsync([FromBody] UserPreferences preferences)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            user.PreferredRoutes = preferences.PreferredRoutes;
            user.ShowPreferredRoutesFirst = preferences.ShowPreferredRoutesFirst;
            user.EnableEmailNotifications = preferences.EnableEmailNotifications;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Preferences updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating preferences", error = ex.Message });
            }
        }

        // API Key Operations
        [HttpPost("api-keys")]
        public async Task<IActionResult> RequestApiKeyAsync([FromBody] ApiKeyRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            ApiKeyRequest? existingRequest = await _context.ApiKeyRequests
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending");

            if (existingRequest != null)
                return BadRequest(new { message = "You already have a pending API key request." });

            ApiKey? existingActiveKey = await _context.ApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.IsActive);

            if (existingActiveKey != null)
                return BadRequest(new { message = "You already have an active API key." });

            try
            {
                _context.ApiKeyRequests.Add(new ApiKeyRequest
                {
                    UserId = userId,
                    Reason = request.Reason,
                    IntendedUse = request.IntendedUse,
                    Status = "Pending",
                    RequestedAt = DateTime.UtcNow
                });
                user.HasRequestedApiAccess = true;
                await _context.SaveChangesAsync();

                Uri locationUri = new($"{Request.Scheme}://{Request.Host}/api/accounts/api-keys/{userId}");
                return Created(locationUri, new { message = "API key request submitted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error submitting request", error = ex.Message });
            }
        }

        [HttpPut("api-keys")]
        public async Task<IActionResult> RegenerateApiKeyAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApiKey? existingKey = await _context.ApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.IsActive);

            if (existingKey == null)
                return BadRequest(new { message = "No active API key found to regenerate." });

            try
            {
                existingKey.IsActive = false;
                ApiKey newKey = new()
                {
                    Key = await _apiKeyGenerator.GenerateApiKeyAsync(userId),
                    UserId = userId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ApiKeys.Add(newKey);
                await _context.SaveChangesAsync();

                return Ok(new { key = newKey.Key, message = "API key regenerated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error regenerating API key", error = ex.Message });
            }
        }

        [HttpGet("api-keys")]
        public async Task<IActionResult> GetApiKeyStatusAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApiKey? activeKey = await _context.ApiKeys
                .FirstOrDefaultAsync(k => k.UserId == userId && k.IsActive);

            bool hasPendingRequest = await _context.ApiKeyRequests
                .AnyAsync(r => r.UserId == userId && r.Status == "Pending");

            return Ok(new
            {
                hasApiKey = activeKey != null,
                key = activeKey?.Key,
                pendingRequest = hasPendingRequest
            });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfileAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var user = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.CreatedAt,
                    LastLogin = u.LastLoginAt,
                    u.EnableEmailNotifications
                })
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user == null ? NotFound() : Ok(user);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileModel model)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            user.EnableEmailNotifications = model.EnableEmailNotifications;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Profile updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating profile", error = ex.Message });
            }
        }

        [HttpDelete("feedback")]
        public async Task<IActionResult> SubmitDeletionFeedbackAsync([FromBody] DeletionFeedbackModel model)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            user.DeletionReason = model.Reason;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Feedback submitted successfully" });
        }

        // Password Operations
        [HttpPut("password")]
        public async Task<IActionResult> UpdatePasswordAsync([FromBody] ChangePasswordModel model)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            bool result = await _userService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
            if (!result)
                return BadRequest(new { message = "Invalid current password" });

            return Ok(new { message = "Password updated successfully" });
        }

        [HttpGet("routes")]
        public async Task<IActionResult> GetAvailableRoutesAsync()
        {
            try
            {
                List<string> routes = await _context.BusArrivals
                    .AsNoTracking()
                    .Select(b => b.Service)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync();

                // Fallback to default routes if none found in database
                if (routes.Count == 0)
                {
                    routes = ["102", "103", "115", "117", "119", "125", "566", "712", "715",
                             "718", "720", "760", "761", "762", "763", "764", "765", "778",
                             "800", "801", "803", "807", "809", "819", "820", "821", "822",
                             "823", "824", "825", "826", "953", "954", "956", "957", "958",
                             "959", "959B", "961", "962", "963", "964", "965", "965B", "975",
                             "983", "998"];
                }

                return Ok(new { routes });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving routes" });
            }
        }

        private string? GetCurrentUserId()
        {
            string? email = User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value;

            return email == null ? null : (_context.Users.FirstOrDefault(u => u.Email == email)?.Id);
        }
    }
}