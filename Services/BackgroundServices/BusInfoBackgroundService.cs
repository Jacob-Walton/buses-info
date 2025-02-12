using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using Microsoft.EntityFrameworkCore;
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
                            bool hasArrivedToday = await HasArrivedTodayAsync(dbContext, service, currentBay, stoppingToken);

                            if (!hasArrivedToday)
                            {
                                await SaveArrivalDataAsync(dbContext, weatherService, service, currentBay, stoppingToken);
                                LogNewArrival(_logger, service, currentBay, null);
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

        private static Task<bool> HasArrivedTodayAsync(
            ApplicationDbContext dbContext,
            string service,
            string bay,
            CancellationToken cancellationToken)
        {
            DateTime today = DateTime.UtcNow.Date;
            return dbContext.BusArrivals.AnyAsync(
                x => x.Service == service &&
                     x.Bay == bay &&
                     x.ArrivalTime.Date == today,
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

            BusArrival arrival = new()
            {
                Service = service,
                Bay = bay,
                Status = $"Arrived at {now:HH:mm}",
                ArrivalTime = now,
                DayOfWeek = (int)now.DayOfWeek,
                Temperature = weather.Temperature,
                Weather = weather.Weather,
                WeekOfYear = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday),
                IsSchoolTerm = IsSchoolTerm(now)
            };

            dbContext.BusArrivals.Add(arrival);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static bool IsSchoolTerm(DateTime date)
        {
            return date >= new DateTime(2024, 9, 1) && date <= new DateTime(2025, 7, 4);
        }
    }
}
