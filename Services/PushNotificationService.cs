using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Logging.LoggerMessage;

namespace BusInfo.Services
{
    public interface IPushNotificationService
    {
        Task<bool> RegisterDeviceAsync(DeviceRegistrationRequest request);
        Task<bool> SendNotificationAsync(string userId, PushNotification notification);
        Task<int> SendNotificationToAllAsync(PushNotification notification);
    }

    public class PushNotificationService : IPushNotificationService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<PushNotificationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppleNotificationSettings _appleSettings;
        // Store both the token and its expiry time
        private readonly Dictionary<string, (string token, DateTime expiry)> _tokenCache = [];

        #region Logger Message Definitions

        // Service initialization (6000-6009)
        private static readonly Action<ILogger, string, string, string, Exception?> _logServiceInitialized =
            Define<string, string, string>(LogLevel.Information, 6000,
                "PushNotificationService initialized with Apple settings: TeamId={TeamId}, KeyId={KeyId}, AppBundleId={AppBundleId}");

        // Device registration (6010-6029)
        private static readonly Action<ILogger, string, string, Exception?> _logDeviceRegistration =
            Define<string, string>(LogLevel.Information, 6010,
                "Registering device for user {UserId}, token: {TokenPrefix}...");

        private static readonly Action<ILogger, string, Exception?> _logDeviceUpdated =
            Define<string>(LogLevel.Information, 6011,
                "Updated existing device registration for user {UserId}");

        private static readonly Action<ILogger, string, Exception?> _logNewDeviceRegistered =
            Define<string>(LogLevel.Information, 6012,
                "Created new device registration for user {UserId}");

        private static readonly Action<ILogger, string, Exception> _logDeviceRegistrationError =
            Define<string>(LogLevel.Error, 6013,
                "Error registering device for user {UserId}");

        // New logger definition for multiple devices
        private static readonly Action<ILogger, string, int, Exception?> _logMultipleDevicesForUser =
            Define<string, int>(LogLevel.Information, 6014,
                "User {UserId} has {DeviceCount} registered devices");

        // Notification sending (6030-6049)
        private static readonly Action<ILogger, string, string, Exception?> _logSendingNotification =
            Define<string, string>(LogLevel.Information, 6030,
                "Sending notification to user {UserId}: {Title}");

        private static readonly Action<ILogger, string, Exception?> _logNoActiveDevices =
            Define<string>(LogLevel.Warning, 6031,
                "No active devices found for user {UserId}");

        private static readonly Action<ILogger, int, Exception?> _logNotificationSkipped =
            Define<int>(LogLevel.Information, 6032,
                "Skipping notification for device {DeviceId} due to user preferences");

        private static readonly Action<ILogger, int, Exception?> _logSendNotificationFailed =
            Define<int>(LogLevel.Warning, 6033,
                "Failed to send notification to device {DeviceId}");

        private static readonly Action<ILogger, string, Exception> _logNotificationError =
            Define<string>(LogLevel.Error, 6034,
                "Error sending notification to user {UserId}");

        // Broadcast notifications (6050-6069)
        private static readonly Action<ILogger, string, Exception?> _logBroadcastNotification =
            Define<string>(LogLevel.Information, 6050,
                "Broadcasting notification to all users: {Title}");

        private static readonly Action<ILogger, Exception?> _logNoBroadcastDevices =
            Define(LogLevel.Warning, 6051,
                "No active devices found for broadcast");

        private static readonly Action<ILogger, int, Exception?> _logBroadcastFailed =
            Define<int>(LogLevel.Warning, 6052,
                "Failed to send broadcast notification to device {DeviceId}");

        private static readonly Action<ILogger, Exception> _logBroadcastError =
            Define(LogLevel.Error, 6053,
                "Error broadcasting notification");

        // Notification filtering (6070-6079)
        private static readonly Action<ILogger, int, Exception?> _logSkipBusArrival =
            Define<int>(LogLevel.Debug, 6070,
                "Skipping bus arrival notification for device {DeviceId} - notifications disabled");

        private static readonly Action<ILogger, int, Exception?> _logSkipServiceUpdate =
            Define<int>(LogLevel.Debug, 6071,
                "Skipping service update notification for device {DeviceId} - notifications disabled");

        private static readonly Action<ILogger, string, int, Exception?> _logSkipBusNotInPreferred =
            Define<string, int>(LogLevel.Debug, 6072,
                "Skipping bus {BusNumber} notification for device {DeviceId} - not in preferred buses list");

        // Push notification sending (6080-6099)
        private static readonly Action<ILogger, Exception?> _logEmptyToken =
            Define(LogLevel.Warning, 6080,
                "Attempted to send notification with empty device token");

        private static readonly Action<ILogger, string, string, Exception?> _logApnsRequest =
            Define<string, string>(LogLevel.Debug, 6081,
                "Sending APNs request to {Url} with payload: {Payload}");

        private static readonly Action<ILogger, string, Exception?> _logPushSuccess =
            Define<string>(LogLevel.Information, 6082,
                "Successfully sent notification to device {DeviceToken}");

        private static readonly Action<ILogger, int, string, Exception?> _logPushFailure =
            Define<int, string>(LogLevel.Error, 6083,
                "Failed to send notification. Status: {StatusCode}, Response: {Response}");

        private static readonly Action<ILogger, string, Exception> _logPushError =
            Define<string>(LogLevel.Error, 6084,
                "Error sending push notification to device {DeviceToken}");

        // Authentication token management (6100-6119)
        private static readonly Action<ILogger, DateTime, Exception?> _logCachedToken =
            Define<DateTime>(LogLevel.Debug, 6100,
                "Using cached APNs token, valid until {Expiry}");

        private static readonly Action<ILogger, Exception?> _logGeneratingToken =
            Define(LogLevel.Information, 6101,
                "Generating new APNs authentication token");

        private static readonly Action<ILogger, DateTime, Exception?> _logNewToken =
            Define<DateTime>(LogLevel.Debug, 6102,
                "Generated new APNs token, valid until {Expiry}");

        private static readonly Action<ILogger, Exception> _logTokenError =
            Define(LogLevel.Error, 6103,
                "Error generating APNs authentication token");

        private static readonly Action<ILogger, Exception> _logSigningError =
            Define(LogLevel.Error, 6104,
                "Error signing APNs token");

        // Device management (6120-6129)
        private static readonly Action<ILogger, string, Exception?> _logDeviceInactive =
            Define<string>(LogLevel.Information, 6120,
                "Marked device {DeviceToken} as inactive");

        private static readonly Action<ILogger, string, Exception> _logInactiveError =
            Define<string>(LogLevel.Error, 6121,
                "Error marking device as inactive: {DeviceToken}");

        // Add new logger definition for bad tokens in the Logger Message Definitions region
        private static readonly Action<ILogger, string, Exception?> _logBadDeviceToken =
            Define<string>(LogLevel.Warning, 6085,
                "Detected bad device token: {TokenPrefix}");

        private static readonly Action<ILogger, string, int, Exception?> _logDeviceProcessingStatus =
            Define<string, int>(LogLevel.Debug, 6086,
                "For user {UserId}: processed {DeviceCount} devices");

        #endregion Logger Message Definitions

        public PushNotificationService(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ILogger<PushNotificationService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

            // Load Apple notification settings from configuration
            _appleSettings = new AppleNotificationSettings
            {
                TeamId = configuration["PushNotifications:Apple:TeamId"] ?? throw new ArgumentNullException("Apple TeamId not configured"),
                KeyId = configuration["PushNotifications:Apple:KeyId"] ?? throw new ArgumentNullException("Apple KeyId not configured"),
                AppBundleId = configuration["PushNotifications:Apple:AppBundleId"] ?? throw new ArgumentNullException("Apple AppBundleId not configured"),
                AuthKey = configuration["PushNotifications:Apple:AuthKey"] ?? throw new ArgumentNullException("Apple AuthKey not configured")
            };

            _logServiceInitialized(_logger, _appleSettings.TeamId, _appleSettings.KeyId, _appleSettings.AppBundleId, null);
        }

        /// <summary>
        /// Registers or updates a device for push notifications
        /// </summary>
        /// <param name="request">Device registration request containing user ID, device token, and notification settings</param>
        public async Task<bool> RegisterDeviceAsync(DeviceRegistrationRequest request)
        {
            try
            {
                string tokenPrefix = request.DeviceToken[..Math.Min(request.DeviceToken.Length, 10)];
                _logDeviceRegistration(_logger, request.UserId, tokenPrefix, null);

                // Check if the device is already registered
                DeviceRegistration? existingDevice = await _dbContext.DeviceRegistrations
                    .FirstOrDefaultAsync(d => d.DeviceToken == request.DeviceToken);

                if (existingDevice != null)
                {
                    // Update existing registration
                    existingDevice.UserId = request.UserId;
                    existingDevice.AppVersion = request.AppVersion;
                    existingDevice.OsVersion = request.OsVersion;
                    existingDevice.UpdatedAt = DateTime.UtcNow;
                    existingDevice.IsActive = true;
                    existingDevice.BusArrivalNotifications = request.NotificationSettings.BusArrivalNotifications;
                    existingDevice.ServiceUpdateNotifications = request.NotificationSettings.ServiceUpdateNotifications;

                    // Convert specific buses array to comma-separated string for storage
                    existingDevice.SpecificBuses = request.NotificationSettings.SpecificBuses?.Length > 0
                        ? string.Join(",", request.NotificationSettings.SpecificBuses)
                        : string.Empty;

                    _dbContext.Update(existingDevice);
                    _logDeviceUpdated(_logger, request.UserId, null);
                }
                else
                {
                    // Create new registration
                    DeviceRegistration newDevice = new()
                    {
                        UserId = request.UserId,
                        DeviceToken = request.DeviceToken,
                        DeviceType = request.DeviceType,
                        AppVersion = request.AppVersion,
                        OsVersion = request.OsVersion,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true,
                        BusArrivalNotifications = request.NotificationSettings.BusArrivalNotifications,
                        ServiceUpdateNotifications = request.NotificationSettings.ServiceUpdateNotifications,
                        SpecificBuses = request.NotificationSettings.SpecificBuses?.Length > 0
                            ? string.Join(",", request.NotificationSettings.SpecificBuses)
                            : string.Empty
                    };

                    _dbContext.DeviceRegistrations.Add(newDevice);
                    _logNewDeviceRegistered(_logger, request.UserId, null);

                    // Log the number of devices this user now has (including the new one)
                    int deviceCount = await _dbContext.DeviceRegistrations
                        .CountAsync(d => d.UserId == request.UserId && d.IsActive) + 1;

                    if (deviceCount > 1)
                    {
                        _logMultipleDevicesForUser(_logger, request.UserId, deviceCount, null);
                    }
                }

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logDeviceRegistrationError(_logger, request.UserId, ex);
                return false;
            }
        }

        /// <summary>
        /// Sends a notification to all devices for a specific user
        /// </summary>
        /// <param name="userId">User ID to send the notification to</param>
        /// <param name="notification">Notification details</param>
        public async Task<bool> SendNotificationAsync(string userId, PushNotification notification)
        {
            try
            {
                _logSendingNotification(_logger, userId, notification.Title, null);

                // Get all active device registrations for this user
                List<DeviceRegistration> devices = await _dbContext.DeviceRegistrations
                    .Where(d => d.UserId == userId && d.IsActive)
                    .ToListAsync();

                if (devices.Count == 0)
                {
                    _logNoActiveDevices(_logger, userId, null);
                    return false;
                }

                // Log when we find multiple devices
                if (devices.Count > 1)
                {
                    _logMultipleDevicesForUser(_logger, userId, devices.Count, null);
                }

                bool anySuccess = false;
                List<Exception> exceptions = new();

                // Send to each device
                foreach (DeviceRegistration device in devices)
                {
                    try
                    {
                        // Check if notification type matches user preferences
                        if (!ShouldSendNotification(device, notification))
                        {
                            _logNotificationSkipped(_logger, device.Id, null);
                            continue;
                        }

                        bool success = await SendPushNotificationAsync(device.DeviceToken, notification);

                        if (success)
                        {
                            anySuccess = true;
                        }
                        else
                        {
                            _logSendNotificationFailed(_logger, device.Id, null);

                            // Explicitly check for bad tokens
                            if (IsLikelyBadToken(device.DeviceToken))
                            {
                                string tokenPrefix = device.DeviceToken[..Math.Min(device.DeviceToken.Length, 10)];
                                _logBadDeviceToken(_logger, tokenPrefix, null);
                                await MarkDeviceAsInactiveAsync(device.DeviceToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        _logger.LogError(ex, "Error sending notification to device {DeviceId}", device.Id);
                    }
                }

                // Log summary of device processing
                _logDeviceProcessingStatus(_logger, userId, devices.Count, null);

                // If we have exceptions but some notifications were sent successfully, we still return success
                if (exceptions.Count > 0 && !anySuccess)
                {
                    throw new AggregateException("Failed to send notifications to any device", exceptions);
                }

                return anySuccess;
            }
            catch (Exception ex)
            {
                _logNotificationError(_logger, userId, ex);
                return false;
            }
        }

        /// <summary>
        /// Sends a notification to all active devices
        /// </summary>
        /// <param name="notification">Notification details</param>
        public async Task<int> SendNotificationToAllAsync(PushNotification notification)
        {
            try
            {
                _logBroadcastNotification(_logger, notification.Title, null);

                // Get all active device registrations
                List<DeviceRegistration> devices = await _dbContext.DeviceRegistrations
                    .Where(d => d.IsActive)
                    .ToListAsync();

                if (devices.Count == 0)
                {
                    _logNoBroadcastDevices(_logger, null);
                    return 0;
                }

                int successCount = 0;
                List<Exception> exceptions = new();

                // Send to each device
                foreach (DeviceRegistration device in devices)
                {
                    try
                    {
                        // Check if notification type matches user preferences
                        if (!ShouldSendNotification(device, notification))
                        {
                            continue;
                        }

                        bool success = await SendPushNotificationAsync(device.DeviceToken, notification);

                        if (success)
                        {
                            successCount++;
                        }
                        else
                        {
                            _logBroadcastFailed(_logger, device.Id, null);

                            // Explicitly check for bad tokens
                            if (IsLikelyBadToken(device.DeviceToken))
                            {
                                string tokenPrefix = device.DeviceToken[..Math.Min(device.DeviceToken.Length, 10)];
                                _logBadDeviceToken(_logger, tokenPrefix, null);
                                await MarkDeviceAsInactiveAsync(device.DeviceToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        _logger.LogError(ex, "Error sending broadcast notification to device {DeviceId}", device.Id);
                    }
                }

                // Group devices by user for reporting
                var devicesByUser = devices.GroupBy(d => d.UserId);
                foreach (var userDevices in devicesByUser)
                {
                    _logDeviceProcessingStatus(_logger, userDevices.Key, userDevices.Count(), null);
                }

                // Log summary if we had exceptions
                if (exceptions.Count > 0)
                {
                    _logger.LogWarning("Encountered {ErrorCount} errors while sending broadcast notifications", exceptions.Count);
                }

                return successCount;
            }
            catch (Exception ex)
            {
                _logBroadcastError(_logger, ex);
                return 0;
            }
        }

        /// <summary>
        /// Determines if a notification should be sent based on user preferences
        /// </summary>
        /// <param name="device">Device registration</param>
        /// <param name="notification">Notification details</param>
        private bool ShouldSendNotification(DeviceRegistration device, PushNotification notification)
        {
            // For test notifications, always send
            if (notification.Type == NotificationType.General)
            {
                return true;
            }

            // Check notification type against user preferences
            if (notification.Type == NotificationType.BusArrival && !device.BusArrivalNotifications)
            {
                _logSkipBusArrival(_logger, device.Id, null);
                return false;
            }

            if (notification.Type == NotificationType.ServiceUpdate && !device.ServiceUpdateNotifications)
            {
                _logSkipServiceUpdate(_logger, device.Id, null);
                return false;
            }

            // If specific buses are defined and this is a bus arrival notification
            if (notification.Type == NotificationType.BusArrival &&
                !string.IsNullOrEmpty(device.SpecificBuses) &&
                notification.Data != null &&
                notification.Data.TryGetValue("busNumber", out string? busNumber))
            {
                // Check if the user wants notifications for all buses (empty list) or this specific bus
                string[] specificBuses = device.SpecificBuses.Split(',', StringSplitOptions.RemoveEmptyEntries);

                // If user has specified buses but this bus isn't in the list
                if (specificBuses.Length > 0 && !specificBuses.Contains(busNumber))
                {
                    _logSkipBusNotInPreferred(_logger, busNumber, device.Id, null);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if a device token is likely to be bad/invalid
        /// </summary>
        private bool IsLikelyBadToken(string deviceToken)
        {
            // Basic validation of token format
            if (string.IsNullOrWhiteSpace(deviceToken))
                return true;

            // Apple tokens should be 64 hexadecimal characters
            // This is a simple check - you might need to adjust based on actual token format
            if (deviceToken.Length != 64)
                return true;

            // Check if token contains only valid hex characters
            foreach (char c in deviceToken)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a push notification to a specific device token
        /// </summary>
        /// <param name="deviceToken">Device token to send the notification to</param>
        /// <param name="notification">Notification details</param>
        private async Task<bool> SendPushNotificationAsync(string deviceToken, PushNotification notification)
        {
            if (string.IsNullOrEmpty(deviceToken))
            {
                _logEmptyToken(_logger, null);
                return false;
            }

            // Do basic token validation before attempting to send
            if (IsLikelyBadToken(deviceToken))
            {
                string tokenPrefix = deviceToken[..Math.Min(deviceToken.Length, 10)];
                _logBadDeviceToken(_logger, tokenPrefix, null);
                await MarkDeviceAsInactiveAsync(deviceToken);
                return false;
            }

            try
            {
                // Prepare the HTTP client
                using HttpClient httpClient = _httpClientFactory.CreateClient();
                // Ensure HTTP/2 is preferred (default in modern .NET)
                httpClient.DefaultRequestVersion = new Version(2, 0);

                bool useDevelopmentServer =
                    string.Equals(_appleSettings.AppBundleId, "com.example.development", StringComparison.OrdinalIgnoreCase);
                string baseUrl = useDevelopmentServer
                    ? "https://api.development.push.apple.com/3/device/"
                    : "https://api.push.apple.com/3/device/";

                string requestUrl = baseUrl + deviceToken;

                // Set up authentication token for APNs
                string authToken = await GenerateApnsAuthTokenAsync();

                Dictionary<string, object> payload = new()
                {
                    ["aps"] = new Dictionary<string, object>
                    {
                        ["alert"] = new Dictionary<string, object>
                        {
                            ["title"] = notification.Title,
                            // ["subtitle"] = "Optional Subtitle",
                            ["body"] = notification.Body
                        },
                        ["badge"] = 1,
                        ["sound"] = notification.Sound ?? "default"
                    },
                    ["id"] = notification.Id ?? Guid.NewGuid().ToString(),
                    ["notificationType"] = notification.Type.ToString(), // Use a consistent key name like "notificationType"
                    ["data"] = notification.Data ?? []
                };

                // Create the HTTP request
                HttpRequestMessage request = new(HttpMethod.Post, requestUrl)
                {
                    Version = new Version(2, 0) // Explicitly set HTTP/2
                };

                // Set headers matching the curl command
                request.Headers.Add("authorization", $"bearer {authToken}");
                request.Headers.Add("apns-topic", _appleSettings.AppBundleId);
                request.Headers.Add("apns-push-type", "alert");
                request.Headers.Add("apns-priority", "10");
                request.Headers.Add("apns-expiration", "0");

                // Add the JSON payload
                string jsonContent = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Ensure consistency if needed, though dictionary keys are explicit
                    IgnoreNullValues = true
                });
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logApnsRequest(_logger, requestUrl, jsonContent, null);

                // Send the request
                HttpResponseMessage response = await httpClient.SendAsync(request);

                // Check for success
                bool isSuccess = response.IsSuccessStatusCode;

                if (isSuccess)
                {
                    string tokenPrefix = deviceToken[..Math.Min(deviceToken.Length, 10)];
                    _logPushSuccess(_logger, tokenPrefix, null);
                }
                else
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _logPushFailure(_logger, (int)response.StatusCode, responseContent, null);

                    // Check for specific errors like invalid token
                    if (response.StatusCode == System.Net.HttpStatusCode.BadRequest ||
                        response.StatusCode == System.Net.HttpStatusCode.Gone ||
                        (int)response.StatusCode == 410) // Explicitly check for Gone status code
                    {
                        // Token may be invalid or expired
                        await MarkDeviceAsInactiveAsync(deviceToken);
                    }
                }

                return isSuccess;
            }
            catch (Exception ex)
            {
                string tokenPrefix = deviceToken.Substring(0, Math.Min(deviceToken.Length, 10));
                _logPushError(_logger, tokenPrefix, ex);

                // Mark device as inactive on exceptions too
                await MarkDeviceAsInactiveAsync(deviceToken);
                return false;
            }
        }

        /// <summary>
        /// Generates an authentication token for Apple Push Notification service
        /// </summary>
        private async Task<string> GenerateApnsAuthTokenAsync()
        {
            string cacheKey = $"apns_token_{_appleSettings.TeamId}_{_appleSettings.KeyId}";

            // Check if we have a cached token that's still valid
            if (_tokenCache.TryGetValue(cacheKey, out (string token, DateTime expiry) tokenInfo) && tokenInfo.expiry > DateTime.UtcNow)
            {
                _logCachedToken(_logger, tokenInfo.expiry, null);
                return tokenInfo.token;
            }

            try
            {
                _logGeneratingToken(_logger, null);

                // Token has format: {header}.{payload}.{signature}
                var header = new { alg = "ES256", kid = _appleSettings.KeyId };
                var payload = new { iss = _appleSettings.TeamId, iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };

                // Serialize to base64url strings
                string headerJson = JsonSerializer.Serialize(header);
                string payloadJson = JsonSerializer.Serialize(payload);

                string headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
                string payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

                // Join header and payload with a period
                string unsignedToken = $"{headerBase64}.{payloadBase64}";

                // Sign the token with the private key
                string signature = SignWithES256(unsignedToken, _appleSettings.AuthKey);

                // Join all parts to form the complete token
                string token = $"{unsignedToken}.{signature}";

                // Cache the token for 50 minutes (Apple tokens are valid for 60 minutes)
                DateTime expiry = DateTime.UtcNow.AddMinutes(50);
                _tokenCache[cacheKey] = (token, expiry);

                _logNewToken(_logger, expiry, null);
                return token;
            }
            catch (Exception ex)
            {
                _logTokenError(_logger, ex);
                throw;
            }
        }

        /// <summary>
        /// Signs the token using ES256 (ECDSA with P-256 curve and SHA-256)
        /// </summary>
        /// <param name="token">The token to sign</param>
        /// <param name="privateKey">The private key in PEM format</param>
        private string SignWithES256(string token, string privateKey)
        {
            try
            {
                // Clean up the private key - remove headers, footers, and newlines
                string cleanedKey = privateKey
                    .Replace("-----BEGIN PRIVATE KEY-----", "", StringComparison.Ordinal)
                    .Replace("-----END PRIVATE KEY-----", "", StringComparison.Ordinal)
                    .Replace("\n", "", StringComparison.Ordinal)
                    .Replace("\r", "", StringComparison.Ordinal);

                // Convert from base64 to byte array
                byte[] keyData = Convert.FromBase64String(cleanedKey);

                // Create the ECDsa object with the private key
                using ECDsa ecdsa = ECDsa.Create();
                ecdsa.ImportPkcs8PrivateKey(keyData, out _);

                // Sign the token
                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                byte[] signature = ecdsa.SignData(tokenBytes, HashAlgorithmName.SHA256);

                // Return base64url encoded signature
                return Base64UrlEncode(signature);
            }
            catch (Exception ex)
            {
                _logSigningError(_logger, ex);
                throw;
            }
        }

        /// <summary>
        /// Encodes data in Base64URL format (base64 safe for URLs)
        /// </summary>
        /// <param name="data">The byte array to encode</param>
        private static string Base64UrlEncode(byte[] data)
        {
            string base64 = Convert.ToBase64String(data);
            return base64
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Mark a device as inactive when push notification fails
        /// </summary>
        /// <param name="deviceToken">The device token to mark as inactive</param>
        private async Task MarkDeviceAsInactiveAsync(string deviceToken)
        {
            try
            {
                DeviceRegistration? device = await _dbContext.DeviceRegistrations
                    .FirstOrDefaultAsync(d => d.DeviceToken == deviceToken);

                if (device != null)
                {
                    device.IsActive = false;
                    device.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.SaveChangesAsync();
                    string tokenPrefix = deviceToken[..Math.Min(deviceToken.Length, 10)];
                    _logDeviceInactive(_logger, tokenPrefix, null);
                }
            }
            catch (Exception ex)
            {
                string tokenPrefix = deviceToken.Substring(0, Math.Min(deviceToken.Length, 10));
                _logInactiveError(_logger, tokenPrefix, ex);
            }
        }
    }

    public class AppleNotificationSettings
    {
        public string TeamId { get; set; } = "";
        public string KeyId { get; set; } = "";
        public string AppBundleId { get; set; } = "";
        public string AuthKey { get; set; } = "";
    }
}
