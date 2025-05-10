using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using BusInfo.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusInfo.Services.BackgroundServices
{
    public class BusInfoBackgroundService(
        ILogger<BusInfoBackgroundService> logger,
        IServiceScopeFactory scopeFactory) : BackgroundService
    {
        private readonly ILogger<BusInfoBackgroundService> _logger = logger;
        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly Dictionary<string, string> _previousBusData = [];
        private DateTime _lastCheckTime = DateTime.MinValue;
        private const int CHECK_INTERVAL_SECONDS = 30;
        private static bool IsValidTimeToCheck()
        {
            DateTime now = DateTime.Now;
            return now.DayOfWeek != DayOfWeek.Saturday
                && now.DayOfWeek != DayOfWeek.Sunday
                && now.Hour >= 14
                && now.Hour < 19;
        }

        private static readonly Action<ILogger, DateTime, Exception?> LogRequestReceived =
            LoggerMessage.Define<DateTime>(
                LogLevel.Information,
                new EventId(1, "CheckingBuses"),
                "Checking buses at {Timestamp}");

        private static readonly Action<ILogger, DateTime, Exception?> LogRequestSkipped =
            LoggerMessage.Define<DateTime>(
                LogLevel.Information,
                new EventId(1, "CheckingBuses"),
                "Skipping bus check at {Timestamp}");

        private static readonly Action<ILogger, string, string, Exception?> LogNewArrival =
            LoggerMessage.Define<string, string>(
                LogLevel.Information,
                new EventId(2, "NewArrival"),
                "New arrival recorded for service {Service} at bay {Bay}");

        private static readonly Action<ILogger, Exception> LogBusCheckError =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(3, "BusCheckError"),
                "Error occurred while checking buses");

        private static readonly Action<ILogger, Exception?> LogDataReset =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(4, "DataReset"),
                "Reset previous bus data");

        private static readonly Action<ILogger, string, string, Exception?> LogDuplicateArrival =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(5, "DuplicateArrival"),
                "Duplicate arrival detected for service {Service} at bay {Bay}, skipping");

        private static readonly Action<ILogger, string, Exception?> LogNoUsersForBus =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(6, "NoUsersForBus"),
                "No users have bus {Service} in their preferred routes");

        private static readonly Action<ILogger, string, string, int, int, Exception?> LogNotificationsSent =
            LoggerMessage.Define<string, string, int, int>(
                LogLevel.Information,
                new EventId(7, "NotificationsSent"),
                "Sent bus arrival notification for {Service} at bay {Bay} to {SuccessCount}/{TotalCount} users");

        private static readonly Action<ILogger, string, string, Exception?> LogNotificationError =
            LoggerMessage.Define<string, string>(
                LogLevel.Error,
                new EventId(8, "NotificationError"),
                "Failed to send notification to user {UserId} for bus {Service}");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (IsValidTimeToCheck())
                    {
                        await CheckBusesAsync(stoppingToken);
                    }
                    else if (DateTime.UtcNow - _lastCheckTime > TimeSpan.FromHours(1))
                    {
                        ResetPreviousBusData();
                    }
                    else
                    {
                        LogRequestSkipped(_logger, DateTime.UtcNow, null);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    LogBusCheckError(_logger, ex);
                    await Task.Delay(TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS), stoppingToken);
                }
            }
        }

        private void ResetPreviousBusData()
        {
            _previousBusData.Clear();
            _lastCheckTime = DateTime.UtcNow;
            LogDataReset(_logger, null);
        }

        private async Task CheckBusesAsync(CancellationToken stoppingToken)
        {
            LogRequestReceived(_logger, DateTime.UtcNow, null);

            using IServiceScope scope = _scopeFactory.CreateScope();
            IBusInfoService busInfoService = scope.ServiceProvider.GetRequiredService<IBusInfoService>();
            IWeatherService weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                BusInfoResponse response = await busInfoService.GetBusInfoAsync();
                if (response.BusData != null)
                {
                    foreach ((string service, BusStatus status) in response.BusData)
                    {
                        string currentBay = status.Bay ?? string.Empty;
                        _previousBusData.TryGetValue(service, out string? previousBay);

                        if (currentBay != previousBay && !string.IsNullOrEmpty(currentBay))
                        {
                            bool hasArrivedToday = await HasArrivedTodayAsync(dbContext, service, stoppingToken);

                            if (!hasArrivedToday)
                            {
                                try
                                {
                                    await SaveArrivalDataAsync(dbContext, weatherService, service, currentBay, stoppingToken);
                                    LogNewArrival(_logger, service, currentBay, null);
                                    await CheckAndSendNotificationsAsync(service, currentBay, stoppingToken);
                                }
                                catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key", StringComparison.InvariantCulture) == true)
                                {
                                    // Someone else might have inserted the same record between our check and save
                                    LogDuplicateArrival(_logger, service, currentBay, null);
                                }
                            }
                        }

                        _previousBusData[service] = currentBay;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or DbUpdateException)
            {
                LogBusCheckError(_logger, ex);
                throw;
            }
        }

        private async Task CheckAndSendNotificationsAsync(
            string service,
            string bay,
            CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IPushNotificationService pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find users who have this bus in their preferred routes
            List<ApplicationUser> usersToNotify = await dbContext.Users!
                .Where(u => u.PreferredRoutes.Contains(service) && u.DeletedAt == null)
                .ToListAsync(cancellationToken);

            if (usersToNotify.Count == 0)
            {
                LogNoUsersForBus(_logger, service, null);
                return;
            }

            // Create the notification
            PushNotification notification = new()
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Bus {service} has arrived",
                Body = $"Your bus {service} has arrived at bay {bay}",
                Type = NotificationType.BusArrival,
                Sound = "default",
                Data = new Dictionary<string, string>
                {
                    ["busNumber"] = service,
                    ["bay"] = bay
                }
            };

            // Track notification metrics
            int successCount = 0;
            List<string> notifiedUserIds = [];

            // Send notification to each user
            foreach (ApplicationUser? user in usersToNotify)
            {
                try
                {
                    bool success = await pushService.SendNotificationAsync(user.Id, notification);
                    if (success)
                    {
                        successCount++;
                        notifiedUserIds.Add(user.Id);
                    }
                }
                catch (Exception ex)
                {
                    LogNotificationError(_logger, user.Id, service, ex);
                }
            }

            // Log notification outcome
            LogNotificationsSent(_logger, service, bay, successCount, usersToNotify.Count, null);

            // Store notification history if any notifications were sent
            if (successCount > 0)
            {
                try
                {
                    NotificationHistory history = new()

                    {
                        Title = notification.Title,
                        Body = notification.Body,
                        NotificationType = notification.Type.ToString(),
                        Recipients = string.Join(",", notifiedUserIds),
                        DevicesReached = successCount,
                        SentAt = DateTime.UtcNow,
                        SentBy = "System"
                    };

                    dbContext.NotificationHistory.Add(history);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save notification history for bus {Service}", service);
                }
            }
        }

        private static Task<bool> HasArrivedTodayAsync(
            ApplicationDbContext dbContext,
            string service,
            CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;
            Calendar cal = CultureInfo.InvariantCulture.Calendar;
            int weekOfYear = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            int dayOfWeek = (int)now.DayOfWeek;

            // Check if this service has already arrived with same service, day of week, and week of year
            return dbContext!.BusArrivals!.AnyAsync(
                x => x.Service == service &&
                     x.DayOfWeek == dayOfWeek &&
                     x.WeekOfYear == weekOfYear,
                cancellationToken);
        }

        private static async Task SaveArrivalDataAsync(
            ApplicationDbContext dbContext,
            IWeatherService weatherService,
            string service,
            string bay,
            CancellationToken cancellationToken)
        {
            DateTime now = DateTime.UtcNow;
            WeatherInfo weather = await weatherService.GetWeatherAsync("Leyland,UK");
            Calendar cal = CultureInfo.InvariantCulture.Calendar;
            int weekOfYear = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            int dayOfWeek = (int)now.DayOfWeek;

            BusArrival arrival = new()
            {
                Service = service,
                Bay = bay,
                Status = $"Arrived at {now:HH:mm}",
                ArrivalTime = now,
                DayOfWeek = dayOfWeek,
                Temperature = weather.Temperature,
                Weather = weather.Weather,
                WeekOfYear = weekOfYear,
                IsSchoolTerm = IsSchoolTerm(now)
            };

            await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Check again inside transaction to reduce race condition possibility
                    bool exists = await dbContext.BusArrivals!
                        .AnyAsync(x =>
                            x.Service == service &&
                            x.DayOfWeek == dayOfWeek &&
                            x.WeekOfYear == weekOfYear, cancellationToken);

                    if (!exists)
                    {
                        dbContext.BusArrivals?.Add(arrival);
                        await dbContext.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                    else
                    {
                        // Silent rollback if already exists
                        await transaction.RollbackAsync(cancellationToken);
                    }
                }
                catch (DbUpdateException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    // Only rethrow if it's not a duplicate key error
                    if (!ex.InnerException?.Message.Contains("duplicate key", StringComparison.InvariantCulture) ?? true)
                    {
                        throw;
                    }
                }
            });
        }

        private static bool IsSchoolTerm(DateTime date)
        {
            return date >= new DateTime(2024, 9, 1) && date <= new DateTime(2025, 7, 4);
        }
    }
}
