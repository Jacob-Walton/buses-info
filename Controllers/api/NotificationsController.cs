using System;
using System.Threading.Tasks;
using BusInfo.Models;
using BusInfo.Models.Notifications;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Logging.LoggerMessage;
using System.Linq;

namespace BusInfo.Controllers.Api
{
    /// <summary>
    /// Controller for managing device registration and sending notifications
    /// </summary>
    /// <param name="notificationService">Service for handling push notifications</param>
    /// <param name="logger">Logger for logging operations</param>
    [ApiController]
    [Route("api/notifications")]
    public class NotificationsController(
        IPushNotificationService notificationService,
        ILogger<NotificationsController> logger) : ControllerBase
    {
        private readonly IPushNotificationService _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        private readonly ILogger<NotificationsController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        #region Logger Message Definitions

        private static readonly Action<ILogger, string, string, Exception?> _logDeviceRegistration =
            Define<string, string>(LogLevel.Information, 7000, "Device registration attempt for user {UserId} with device token starting with {TokenPrefix}");

        private static readonly Action<ILogger, string, Exception?> _logRegistrationSuccess =
            Define<string>(LogLevel.Information, 7001, "Device registration successful for user {UserId}");

        private static readonly Action<ILogger, string, Exception?> _logRegistrationFailure =
            Define<string>(LogLevel.Warning, 7002, "Device registration failed for user {UserId}");

        private static readonly Action<ILogger, string, Exception?> _logUserIdMismatch =
            Define<string>(LogLevel.Warning, 7003, "User ID mismatch during device registration, authenticated as {UserId}");

        private static readonly Action<ILogger, string, Exception?> _logTestNotification =
            Define<string>(LogLevel.Information, 7010, "Test notification requested by user {UserId}");

        private static readonly Action<ILogger, string, bool, Exception?> _logTestNotificationResult =
            Define<string, bool>(LogLevel.Information, 7011, "Test notification for user {UserId} - Success: {Success}");

        private static readonly Action<ILogger, string, string, Exception?> _logBroadcastNotification =
            Define<string, string>(LogLevel.Information, 7020, "Broadcast notification requested by admin {UserId} with title '{Title}'");

        private static readonly Action<ILogger, int, Exception?> _logBroadcastResult =
            Define<int>(LogLevel.Information, 7021, "Broadcast notification sent to {Count} devices");

        private static readonly Action<ILogger, Exception> _logServiceError =
            Define(LogLevel.Error, 7099, "Notification service error");

        #endregion Logger Message Definitions

        /// <summary>
        /// Registers a device for push notifications
        /// </summary>
        /// <param name="request">Device registration details</param>
        /// <returns>Success or error response</returns>
        [HttpPost("register")]
        [Authorize(AuthenticationSchemes = "Cookies,ApiKey,Bearer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RegisterDeviceAsync([FromBody] DeviceRegistrationRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Ensure request is for the authenticated user
            string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            string tokenPrefix = request.DeviceToken.Length > 10
                ? request.DeviceToken[..10]
                : request.DeviceToken;

            _logDeviceRegistration(_logger, userId, tokenPrefix, null);

            if (request.UserId != userId)
            {
                _logUserIdMismatch(_logger, userId, null);
                return BadRequest("User ID mismatch");
            }

            try
            {
                bool success = await _notificationService.RegisterDeviceAsync(request);

                if (success)
                {
                    _logRegistrationSuccess(_logger, userId, null);
                    return Ok(new { success = true, message = "Device registered successfully" });
                }
                _logRegistrationFailure(_logger, userId, null);

                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "Failed to register device" });
            }
            catch (Exception ex)
            {
                _logServiceError(_logger, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "An error occurred while registering the device" });
            }
        }

        /// <summary>
        /// Sends a test notification to the current user
        /// </summary>
        /// <returns>Result of the notification attempt</returns>
        [HttpPost("test")]
        [Authorize(AuthenticationSchemes = "Cookies,ApiKey,Bearer")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> SendTestNotificationAsync()
        {
            string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            _logTestNotification(_logger, userId, null);

            PushNotification notification = new()
            {
                Title = "Test Notification",
                Body = "This is a test notification from BusInfo",
                Type = NotificationType.General,
                Sound = "default"
            };

            try
            {
                bool success = await _notificationService.SendNotificationAsync(userId, notification);

                _logTestNotificationResult(_logger, userId, success, null);

                return Ok(new
                {
                    success,
                    message = success ? "Test notification sent" : "Failed to send test notification"
                });
            }
            catch (Exception ex)
            {
                _logServiceError(_logger, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "An error occurred while sending the notification" });
            }
        }

        /// <summary>
        /// Admin-only endpoint to send notification to all users
        /// </summary>
        /// <param name="notification">Notification to send</param>
        /// <returns>Count of devices the notification was sent to</returns>
        [HttpPost("broadcast")]
        [Authorize(AuthenticationSchemes = "Cookies,ApiKey,Bearer", Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> SendBroadcastNotificationAsync([FromBody] PushNotification notification)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            string userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            _logBroadcastNotification(_logger, userId, notification.Title, null);

            try
            {
                int count = await _notificationService.SendNotificationToAllAsync(notification);

                _logBroadcastResult(_logger, count, null);

                return Ok(new
                {
                    success = count > 0,
                    count,
                    message = $"Notification sent to {count} devices"
                });
            }
            catch (Exception ex)
            {
                _logServiceError(_logger, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { success = false, message = "An error occurred while broadcasting the notification" });
            }
        }
    }
}
