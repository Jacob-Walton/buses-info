using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusInfo.Models;
using BusInfo.Models.Notifications;
using BusInfo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Logging.LoggerMessage;

namespace BusInfo.Services.BackgroundServices
{
    /// <summary>
    /// Background service that monitors bus arrivals and sends notifications to users.
    /// </summary>
    /// <param name="scopeFactory">Factory to create service scopes.</param>
    /// <param name="logger">Logger for recording service activities.</param>
    public class NotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationBackgroundService> logger) : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        private readonly ILogger<NotificationBackgroundService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly Dictionary<string, string> _lastKnownBusLocations = [];
        private Timer? _busArrivalTimer;

        #region Logger Message Definitions

        // Service lifecycle logs (8000-8009)
        private static readonly Action<ILogger, Exception?> _logServiceStarting =
            Define(LogLevel.Information, 8000, "Notification Background Service is starting");

        private static readonly Action<ILogger, Exception?> _logServiceStopping =
            Define(LogLevel.Information, 8001, "Notification Background Service is stopping");

        // Bus monitoring logs (8010-8019)
        private static readonly Action<ILogger, Exception?> _logCheckingForBusArrivals =
            Define(LogLevel.Information, 8010, "Checking for bus arrivals to notify users about");

        private static readonly Action<ILogger, Exception?> _logNoBusInformation =
            Define(LogLevel.Information, 8011, "No bus information available");

        private static readonly Action<ILogger, string, string, Exception?> _logFirstBusStatus =
            Define<string, string>(LogLevel.Debug, 8012, "First status for bus {BusNumber}: {Status}");

        private static readonly Action<ILogger, string, string?, Exception?> _logBusArrived =
            Define<string, string?>(LogLevel.Information, 8013, "Bus {BusNumber} has arrived at bay {Bay}, sending notification");

        private static readonly Action<ILogger, string, string, string, Exception?> _logBusDeparted =
            Define<string, string, string>(LogLevel.Information, 8014, "Bus {BusNumber} has departed (was: {PreviousStatus}, now: {CurrentStatus})");

        // Notification logs (8020-8029)
        private static readonly Action<ILogger, string, int, Exception?> _logNotificationSent =
            Define<string, int>(LogLevel.Information, 8020, "Sent arrival notification for bus {BusNumber} to {Count} devices");

        // Error logs (8090-8099)
        private static readonly Action<ILogger, Exception> _logBusCheckError =
            Define(LogLevel.Error, 8090, "Error in bus arrival notification check");

        #endregion Logger Message Definitions

        /// <summary>
        /// Executes the background service logic.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to stop the service.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logServiceStarting(_logger, null);

            // Set up a timer to check for bus arrivals and schedule notifications
            _busArrivalTimer = new Timer(CheckBusArrivals, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks for bus arrivals and sends notifications.
        /// </summary>
        /// <param name="state">State object (not used).</param>
        private async void CheckBusArrivals(object? state)
        {
            try
            {
                _logCheckingForBusArrivals(_logger, null);

                using IServiceScope scope = _scopeFactory.CreateScope();
                IBusInfoService busInfoService = scope.ServiceProvider.GetRequiredService<IBusInfoService>();
                IPushNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

                // Get current bus information
                BusInfoResponse busInfo = await busInfoService.GetBusInfoAsync();

                if (busInfo == null || busInfo.BusData == null || busInfo.BusData.Count == 0)
                {
                    _logNoBusInformation(_logger, null);
                    return;
                }

                // Check for status changes that indicate arrivals
                foreach (KeyValuePair<string, BusStatus> busEntry in busInfo.BusData)
                {
                    string busNumber = busEntry.Key;
                    BusStatus busStatus = busEntry.Value;

                    // Normalize the status for comparison
                    string currentStatus = busStatus.Status?.Trim() ?? string.Empty;

                    // If we have no previous status for this bus, just store it and continue
                    if (!_lastKnownBusLocations.TryGetValue(busNumber, out string? previousStatus))
                    {
                        _lastKnownBusLocations[busNumber] = currentStatus;
                        _logFirstBusStatus(_logger, busNumber, currentStatus, null);
                        continue;
                    }

                    // If the status is "Arrived" but previously wasn't
                    bool wasArrived = previousStatus.Contains("Arrived", StringComparison.OrdinalIgnoreCase);
                    bool isNowArrived = currentStatus.Contains("Arrived", StringComparison.OrdinalIgnoreCase);

                    if (!wasArrived && isNowArrived)
                    {
                        // This bus has just arrived - send notification
                        _logBusArrived(_logger, busNumber, busStatus.Bay, null);

                        PushNotification notification = new()
                        {
                            Title = $"Bus {busNumber} Has Arrived",
                            Body = $"Bus {busNumber} has arrived at bay {busStatus.Bay ?? "unknown"}",
                            Type = NotificationType.BusArrival,
                            Sound = "default",
                            Data = new Dictionary<string, string>
                            {
                                { "busNumber", busNumber },
                                { "bayNumber", busStatus.Bay ?? "unknown" }
                            }
                        };

                        // Send to all users who are subscribed to this bus
                        int sentCount = await notificationService.SendNotificationToAllAsync(notification);

                        _logNotificationSent(_logger, busNumber, sentCount, null);
                    }
                    else if (wasArrived && !isNowArrived)
                    {
                        _logBusDeparted(_logger, busNumber, previousStatus, currentStatus, null);
                    }

                    // Update our dictionary of last known statuses
                    _lastKnownBusLocations[busNumber] = currentStatus;
                }
            }
            catch (Exception ex)
            {
                _logBusCheckError(_logger, ex);
            }
        }

        /// <summary>
        /// Stops the background service.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logServiceStopping(_logger, null);

            _busArrivalTimer?.Change(Timeout.Infinite, 0);

            return base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// Disposes resources used by the background service.
        /// </summary>
        public override void Dispose()
        {
            _busArrivalTimer?.Dispose();
            base.Dispose();
        }
    }
}
