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
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using static Microsoft.Extensions.Logging.LoggerMessage;

namespace BusInfo.Controllers
{
    /// <summary>
    /// API controller for user account management, including authentication, preferences,
    /// API key management, and profile operations.
    /// </summary>
    /// <param name="context">Database context for accessing user data.</param>
    /// <param name="userService">Service for user-related operations.</param>
    /// <param name="apiKeyGenerator">Service for generating API keys.</param>
    /// <param name="logger">Logger for logging operations.</param>
    [ApiController]
    [Route("api/accounts")]
    [Authorize(AuthenticationSchemes = "Cookies")]
    public class AccountController(ApplicationDbContext context, IUserService userService, IApiKeyGenerator apiKeyGenerator, ILogger<AccountController> logger) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IUserService _userService = userService;
        private readonly IApiKeyGenerator _apiKeyGenerator = apiKeyGenerator;
        private readonly ILogger<AccountController> _logger = logger;

        #region Logger Message Definitions

        private static readonly Action<ILogger, string?, Exception?> _logLoginAttempt =
            Define<string?>(LogLevel.Information, 1000, "Login attempt for email: {Email}");

        private static readonly Action<ILogger, Exception?> _logLoginModelNull =
            Define(LogLevel.Warning, 1001, "Login failed: Model is null");

        private static readonly Action<ILogger, object, Exception?> _logLoginValidationFailed =
            Define<object>(LogLevel.Warning, 1002, "Login validation failed: {@ValidationErrors}");

        private static readonly Action<ILogger, string, Exception?> _logAuthenticationFailed =
            Define<string>(LogLevel.Warning, 1003, "Authentication failed for email: {Email}");

        private static readonly Action<ILogger, string, Exception?> _logLoginSuccessful =
            Define<string>(LogLevel.Information, 1004, "User {Email} logged in successfully");

        private static readonly Action<ILogger, string, Exception?> _logLoginFailedInvalidOperation =
            Define<string>(LogLevel.Warning, 1005, "Login failed due to invalid operation: {Message}");

        private static readonly Action<ILogger, string, Exception?> _logUnexpectedLoginError =
            Define<string>(LogLevel.Error, 1006, "Unexpected error during login for email: {Email}");

        #endregion Logger Message Definitions

        /// <summary>
        /// Default bus routes used when no routes are found in database
        /// </summary>
        private static readonly string[] DefaultRoutes =
        [
            "102", "103", "115", "117", "119", "125", "566", "712", "715",
            "718", "720", "760", "761", "762", "763", "764", "765", "778",
            "800", "801", "803", "807", "809", "819", "820", "821", "822",
            "823", "824", "825", "826", "953", "954", "956", "957", "958",
            "959", "959B", "961", "962", "963", "964", "965", "965B", "975",
            "983", "998"
        ];

        #region Account Management

        /// <summary>
        /// Retrieves the current user's account information.
        /// </summary>
        /// <returns>
        /// 200 OK with user details if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// </returns>
        [HttpGet]
        public async Task<IActionResult> GetAccountAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            return user == null ? NotFound() : Ok(user);
        }

        /// <summary>
        /// Exports all user data for the current user in compliance with data protection regulations.
        /// </summary>
        /// <returns>
        /// User data as a downloadable JSON file if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// </returns>
        [HttpGet("export")]
        public async Task<IActionResult> ExportAccountAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            byte[] data = await _userService.ExportUserDataAsync(userId);
            return File(data, "application/json", "user-data.json");
        }

        /// <summary>
        /// Initiates the account deletion process for the current user.
        /// </summary>
        /// <param name="model">Contains password verification and optional reason for deletion</param>
        /// <returns>
        /// 200 OK with confirmation message if deletion process started.
        /// 401 Unauthorized if user is not authenticated.
        /// 400 Bad Request if password is invalid.
        /// </returns>
        [HttpDelete]
        public async Task<IActionResult> DeleteAccountAsync([FromBody] DeleteAccountModel model)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            bool result = await _userService.InitiateAccountDeletionAsync(userId, model.Password, model.Reason);
            if (!result) return BadRequest(new { message = "Invalid password" });

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Account deletion initiated. You have 30 days to reactivate your account." });
        }

        /// <summary>
        /// Logs the current user out of the application.
        /// </summary>
        /// <returns>Redirect to home page after logout</returns>
        [HttpPost("logout")]
        public async Task<ActionResult> LogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        #endregion Account Management

        #region User Preferences

        /// <summary>
        /// Gets the current user's preferences including preferred routes and notification settings.
        /// </summary>
        /// <returns>
        /// 200 OK with user preferences if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// </returns>
        [HttpGet("preferences")]
        public async Task<IActionResult> GetPreferencesAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            UserPreferences? preferences = await _context.Users!
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

        /// <summary>
        /// Updates the current user's preferences.
        /// </summary>
        /// <param name="preferences">The updated user preferences</param>
        /// <returns>
        /// 200 OK with confirmation message if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// 500 Internal Server Error if database operation fails.
        /// </returns>
        [HttpPut("preferences")]
        public async Task<IActionResult> UpdatePreferencesAsync([FromBody] UserPreferences preferences)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            user.PreferredRoutes = [.. preferences.PreferredRoutes];
            user.ShowPreferredRoutesFirst = preferences.ShowPreferredRoutesFirst;
            user.EnableEmailNotifications = preferences.EnableEmailNotifications;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "Preferences updated successfully" });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { message = "Database error while updating preferences", error = "Database operation failed" });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new { message = "Invalid operation while updating preferences", error = "Operation invalid" });
            }
        }

        #endregion User Preferences

        #region API Key Management

        /// <summary>
        /// Submits a request for a new API key.
        /// </summary>
        /// <param name="requestDto">The API key request details</param>
        /// <returns>
        /// 201 Created with location header if request submitted successfully.
        /// 400 Bad Request if model is invalid or user already has a pending request/active key.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// 500 Internal Server Error if database operation fails.
        /// </returns>
        [HttpPost("api-keys")]
        public async Task<IActionResult> RequestApiKeyAsync([FromBody] ApiKeyRequestDto requestDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound();

            // Check for existing pending requests
            ApiKeyRequest? existingRequest = await _context.ApiKeyRequests!
                .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == "Pending");

            if (existingRequest != null)
                return BadRequest(new { message = "You already have a pending API key request." });

            // Check for existing active API key
            ApiKey? existingActiveKey = await _context.ApiKeys!
                .FirstOrDefaultAsync(k => k.UserId == userId && k.IsActive);

            if (existingActiveKey != null)
                return BadRequest(new { message = "You already have an active API key." });

            try
            {
                // Create new API key request
                _context.ApiKeyRequests!.Add(new ApiKeyRequest
                {
                    UserId = userId,
                    Reason = requestDto.Reason,
                    IntendedUse = requestDto.IntendedUse,
                    Status = "Pending",
                    RequestedAt = DateTime.UtcNow
                });
                user.HasRequestedApiAccess = true;
                await _context.SaveChangesAsync();

                Uri locationUri = new($"{Request.Scheme}://{Request.Host}/api/accounts/api-keys/{userId}");
                return Created(locationUri, new { message = "API key request submitted successfully." });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { message = "Database error while submitting request", error = "Database operation failed" });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new { message = "Invalid operation while submitting request", error = "Operation invalid" });
            }
        }

        /// <summary>
        /// Regenerates the API key for the current user.
        /// </summary>
        /// <returns>
        /// 200 OK with new API key if successful.
        /// 400 Bad Request if user doesn't have an active API key.
        /// 401 Unauthorized if user is not authenticated.
        /// 500 Internal Server Error if database operation fails.
        /// </returns>
        [HttpPut("api-keys")]
        public async Task<IActionResult> RegenerateApiKeyAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            ApiKey? existingKey = await _context.ApiKeys!
                .FirstOrDefaultAsync(k => k.UserId == userId && k.IsActive);

            if (existingKey == null)
                return BadRequest(new { message = "No active API key found to regenerate." });

            try
            {
                // Deactivate existing key and create new one
                existingKey.IsActive = false;
                ApiKey newKey = new()
                {
                    Key = await _apiKeyGenerator.GenerateApiKeyAsync(userId),
                    UserId = userId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ApiKeys!.Add(newKey);
                await _context.SaveChangesAsync();

                return Ok(new { key = newKey.Key, message = "API key regenerated successfully" });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { message = "Database error while regenerating API key", error = "Database operation failed" });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new { message = "Invalid operation while regenerating API key", error = "Operation invalid" });
            }
        }

        /// <summary>
        /// Gets the current user's API key status including active keys and pending requests.
        /// </summary>
        /// <returns>
        /// 200 OK with API key status if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 500 Internal Server Error if operation fails.
        /// </returns>
        [HttpGet("api-keys")]
        public async Task<IActionResult> GetApiKeyStatusAsync()
        {
            string? userId = GetCurrentUserId();

            // Return a proper error response instead of redirecting
            if (userId == null)
                return Unauthorized(new { message = "User is not authenticated or not found" });

            try
            {
                // Check for active API key
                ApiKey? activeKey = await _context.ApiKeys!
                    .FirstOrDefaultAsync(k => k.UserId == userId && k.IsActive);

                // Check for pending requests
                bool hasPendingRequest = await _context.ApiKeyRequests!
                    .AnyAsync(r => r.UserId == userId && r.Status == "Pending");

                // Check for rejected requests that haven't been dismissed
                ApiKeyRequest? rejectedRequest = await _context.ApiKeyRequests!
                    .Where(r => r.UserId == userId && r.Status == "Rejected" && !r.DismissedByUser)
                    .OrderByDescending(r => r.UpdatedAt)
                    .FirstOrDefaultAsync();

                // Use either RejectionReason or ReviewNotes for the rejection reason
                string? rejectionReason = rejectedRequest?.RejectionReason ?? rejectedRequest?.ReviewNotes;

                return Ok(new
                {
                    hasApiKey = activeKey != null,
                    key = activeKey?.Key,
                    pendingRequest = hasPendingRequest,
                    rejectedRequest = rejectedRequest != null,
                    rejectionReason
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving API key status", error = ex.Message });
            }
        }

        /// <summary>
        /// Dismisses a rejected API key request notification.
        /// </summary>
        /// <returns>
        /// 200 OK with confirmation message if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 500 Internal Server Error if operation fails.
        /// </returns>
        [HttpPost("api-keys/dismiss-rejection")]
        public async Task<IActionResult> DismissRejectionAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            try
            {
                List<ApiKeyRequest> rejectedRequests = await _context.ApiKeyRequests!
                    .Where(r => r.UserId == userId && r.Status == "Rejected" && !r.DismissedByUser)
                    .ToListAsync();

                if (rejectedRequests.Count != 0)
                {
                    foreach (ApiKeyRequest? request in rejectedRequests)
                    {
                        request.DismissedByUser = true;
                        request.DismissedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Rejection notification dismissed successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error dismissing rejection notification", error = ex.Message });
            }
        }

        #endregion API Key Management

        #region User Profile

        /// <summary>
        /// Gets the current user's profile information.
        /// </summary>
        /// <returns>
        /// 200 OK with user profile if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// </returns>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfileAsync()
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            var user = await _context!.Users
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

        /// <summary>
        /// Updates the current user's profile information.
        /// </summary>
        /// <param name="model">The updated profile data</param>
        /// <returns>
        /// 200 OK with confirmation message if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// 500 Internal Server Error if database operation fails.
        /// </returns>
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
            catch (DbUpdateException)
            {
                return StatusCode(500, new { message = "Database error while updating profile", error = "Database operation failed" });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new { message = "Invalid operation while updating profile", error = "Operation invalid" });
            }
        }

        /// <summary>
        /// Submits feedback from a user who is deleting their account.
        /// </summary>
        /// <param name="model">Contains the reason for account deletion</param>
        /// <returns>
        /// 200 OK with confirmation message if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// </returns>
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

        #endregion User Profile

        #region Password Management

        /// <summary>
        /// Updates the current user's password.
        /// </summary>
        /// <param name="model">Contains current and new password</param>
        /// <returns>
        /// 200 OK with confirmation message if successful.
        /// 400 Bad Request if current password is invalid.
        /// 401 Unauthorized if user is not authenticated.
        /// </returns>
        [HttpPut("password")]
        public async Task<IActionResult> UpdatePasswordAsync([FromBody] ChangePasswordModel model)
        {
            string? userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();

            bool result = await _userService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
            return !result ? BadRequest(new { message = "Invalid current password" }) : Ok(new { message = "Password updated successfully" });
        }

        #endregion Password Management

        #region Routes Information

        /// <summary>
        /// Gets all available bus routes for the user to select from.
        /// </summary>
        /// <returns>
        /// 200 OK with list of routes if successful.
        /// 500 Internal Server Error if database operation fails.
        /// </returns>
        [HttpGet("routes")]
        public async Task<IActionResult> GetAvailableRoutesAsync()
        {
            try
            {
                List<string> routes = await _context.BusArrivals!
                    .AsNoTracking()
                    .Select(b => b.Service)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .OrderBy(s => s)
                    .ToListAsync();

                // Fallback to default routes if none found in database
                if (routes.Count == 0)
                {
                    routes = [.. DefaultRoutes];
                }

                return Ok(new { routes });
            }
            catch (DbException)
            {
                return StatusCode(500, new { message = "Database error while retrieving routes" });
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new { message = "Error processing routes data" });
            }
        }

        #endregion Routes Information

        #region Authentication

        /// <summary>
        /// Authenticates a user using email and password credentials.
        /// </summary>
        /// <param name="model">The login credentials</param>
        /// <returns>
        /// 200 OK with authentication token if successful.
        /// 400 Bad Request if request is invalid.
        /// 401 Unauthorized if credentials are invalid.
        /// 500 Internal Server Error if operation fails.
        /// </returns>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginAsync([FromBody] LoginModel model)
        {
            _logLoginAttempt(_logger, model?.Email ?? "null", null);

            if (model == null)
            {
                _logLoginModelNull(_logger, null);
                return BadRequest(new { message = "Request body cannot be empty", details = "A valid login request must contain email and password fields" });
            }

            if (!ModelState.IsValid)
            {
                Dictionary<string, string[]?> errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                _logLoginValidationFailed(_logger, errors, null);
                return BadRequest(new { message = "Validation failed", errors });
            }

            try
            {
                ApplicationUser? user = await _userService.AuthenticateAsync(model.Email, model.Password);
                if (user == null)
                {
                    _logAuthenticationFailed(_logger, model.Email, null);
                    return Unauthorized(new { message = "Invalid email or password" });
                }

                // Generate JWT token using the same method as social logins
                string token = _userService.GenerateJwtToken(user);
                string refreshToken = _userService.GenerateRefreshToken();
                DateTime expiryTime = DateTime.UtcNow.AddDays(30);

                // Save refresh token
                await _userService.SaveRefreshTokenAsync(user, refreshToken, expiryTime);

                _logLoginSuccessful(_logger, user.Email, null);

                // Create the same response format as social logins
                return Ok(new LoginResponse
                {
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(1).ToString("o"),
                    User = new User
                    {
                        Id = user.Id,
                        Email = user.Email,
                        Name = _userService.GetDisplayName(user),
                        Role = user.IsAdmin ? UserRole.Admin : UserRole.Student
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                _logLoginFailedInvalidOperation(_logger, ex.Message, ex);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logUnexpectedLoginError(_logger, model.Email, ex);
                return StatusCode(500, new { message = "An error occurred while processing your request" });
            }
        }

        /// <summary>
        /// Gets authentication details for the current user.
        /// </summary>
        /// <returns>
        /// 200 OK with authentication details if successful.
        /// 401 Unauthorized if user is not authenticated.
        /// 404 Not Found if user doesn't exist.
        /// </returns>
        [HttpGet("auth-details")]
        public async Task<ActionResult<AuthenticationDetails>> GetAuthenticationDetailsAsync()
        {
            string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            ApplicationUser? user = await _userService.GetUserByIdAsync(userId);
            return user == null
                ? (ActionResult<AuthenticationDetails>)NotFound()
                : (ActionResult<AuthenticationDetails>)new AuthenticationDetails
                {
                    Provider = user.AuthProvider,
                    ProviderName = user.AuthProvider.GetDisplayName(),
                    CanChangePassword = user.AuthProvider == AuthProvider.Local,
                    RequiresEmailVerification = user.AuthProvider == AuthProvider.Local,
                    IsEmailVerified = user.IsEmailVerified,
                    Email = user.Email,
                    HasExternalId = !string.IsNullOrEmpty(user.ExternalId)
                };
        }

        #endregion Authentication

        /// <summary>
        /// Helper method to get the current user's ID from claims.
        /// </summary>
        /// <returns>The user ID if found, otherwise null.</returns>
        private string? GetCurrentUserId()
        {
            string? email = User.FindFirst("preferred_username")?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value;

            if (email == null)
                return null;
                
            // Convert to lower case on both sides to ensure case-insensitive comparison
            string emailLower = email.ToLower();
            return _context.Users!
                .Where(u => u.Email.ToLower() == emailLower)
                .Select(u => u.Id)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Model for login credentials.
    /// </summary>
    public class LoginModel
    {
        /// <summary>
        /// User's email address used for authentication.
        /// </summary>
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please provide a valid email address")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User's password.
        /// </summary>
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model for account deletion request.
    /// </summary>
    public class DeleteAccountModel
    {
        /// <summary>
        /// User's current password for verification.
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Optional reason for deleting the account.
        /// </summary>
        public string? Reason { get; set; }
    }
}