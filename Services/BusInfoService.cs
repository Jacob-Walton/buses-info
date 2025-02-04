using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace BusInfo.Services
{
    public sealed class BusInfoService(
        IHttpClientFactory clientFactory,
        IDistributedCache cache,
        ApplicationDbContext dbContext) : IBusInfoService, IDisposable
    {
        private const string BUS_INFO_URL = "https://webservices.runshaw.ac.uk/bus/BusDepartures.aspx";
        private const string CACHE_KEY_LEGACY = "BusInfoCacheLegacy";
        private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);

        private readonly IHttpClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ApplicationDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private bool _disposed;

        private static string Escape(string input)
        {
            return input.Replace("&nbsp;", " ", StringComparison.InvariantCultureIgnoreCase);
        }

        public async Task<BusInfoLegacyResponse> GetLegacyBusInfoAsync()
        {
            string? cachedData = await _cache.GetStringAsync(CACHE_KEY_LEGACY);
            if (cachedData != null)
            {
                return JsonSerializer.Deserialize<BusInfoLegacyResponse>(cachedData)
                    ?? await FetchLegacyBusInfoAsync();
            }

            using CancellationTokenSource cts = new(REQUEST_TIMEOUT);
            BusInfoLegacyResponse response = await FetchLegacyBusInfoAsync();

            DistributedCacheEntryOptions cacheOptions = new()
            {
                AbsoluteExpirationRelativeToNow = CACHE_EXPIRATION
            };

            string serializedData = JsonSerializer.Serialize(response);
            await _cache.SetStringAsync(CACHE_KEY_LEGACY, serializedData, cacheOptions, cts.Token);

            return response;
        }

        private async Task<BusInfoLegacyResponse> FetchLegacyBusInfoAsync()
        {
            using HttpClient client = _clientFactory.CreateClient();
            using CancellationTokenSource cts = new(REQUEST_TIMEOUT);
            client.DefaultRequestHeaders.ConnectionClose = true;  // Force connection to close

            try
            {
                using HttpResponseMessage response = await client.GetAsync(new Uri(BUS_INFO_URL), cts.Token);
                response.EnsureSuccessStatusCode();

                // Read content as string and dispose response immediately
                string content = await response.Content.ReadAsStringAsync(cts.Token);

                HtmlDocument doc = new();
                doc.LoadHtml(content);

                Dictionary<string, string> busData = [];

                HtmlNodeCollection rows = doc.DocumentNode.SelectNodes("//table[@id='grdAll']//tr");
                if (rows != null)
                {
                    foreach (HtmlNode? row in rows)
                    {
                        HtmlNodeCollection cells = row.SelectNodes("td");
                        if (cells != null && cells.Count >= 3)
                        {
                            string service = cells[0].InnerText.Trim();
                            string status = Escape(cells[2].InnerText.Trim()).Trim();

                            if (string.IsNullOrWhiteSpace(status))
                            {
                                status = "Not arrived";
                            }
                            busData[service] = status;
                        }
                    }
                }

                return new BusInfoLegacyResponse
                {
                    BusData = busData,
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                };
            }
            catch (OperationCanceledException ex)
            {
                throw new InvalidOperationException("Request timed out", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse bus info", ex);
            }
        }

        public async Task<BusInfoResponse> GetBusInfoAsync()
        {
            using HttpClient client = _clientFactory.CreateClient();
            client.Timeout = REQUEST_TIMEOUT;
            client.DefaultRequestHeaders.ConnectionClose = true;  // Force connection to close

            using HttpResponseMessage response = await client.GetAsync(new Uri(BUS_INFO_URL));
            response.EnsureSuccessStatusCode();

            // Read content fully and dispose response immediately
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            response.Content.Dispose();

            try
            {
                HtmlDocument doc = new();
                doc.LoadHtml(content);

                Dictionary<string, BusStatus> busData = [];

                HtmlNodeCollection rows = doc.DocumentNode.SelectNodes("//table[@id='grdAll']//tr");
                if (rows != null)
                {
                    foreach (HtmlNode row in rows)
                    {
                        HtmlNodeCollection cells = row.SelectNodes("td");
                        if (cells != null && cells.Count >= 3)
                        {
                            string service = cells[0].InnerText.Trim();
                            string bay = Escape(cells[2].InnerText.Trim()).Trim();
                            string status = !string.IsNullOrWhiteSpace(bay) ? "Arrived" : "Not arrived";

                            busData[service] = new BusStatus
                            {
                                Status = status,
                                Bay = bay
                            };
                        }
                    }
                }

                foreach ((string service, BusStatus status) in busData)
                {
                    status.PredictedBays = [.. await PredictBayForServiceAsync(service, DateTime.UtcNow)];
                    status.PredictionConfidence = status.PredictedBays.Count != 0 ? (int)status.PredictedBays.Average(p => p.Probability) : 0;
                }

                return new BusInfoResponse
                {
                    BusData = busData,
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                };
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Failed to fetch bus info", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException("Request timed out", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse bus info", ex);
            }
        }

        private async Task<List<BayPrediction>> PredictBayForServiceAsync(string service, DateTime targetTime)
        {
            // Handle weekends immediately
            if (targetTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                return [new BayPrediction { Bay = "No weekend service", Probability = 0 }];
            }

            try
            {
                // Get current week of year
                Calendar cal = CultureInfo.InvariantCulture.Calendar;
                int currentWeek = cal.GetWeekOfYear(targetTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

                // Get all historical data for this service on weekdays, removing strict bay format check
                List<BusArrival> historicalData = await _dbContext.BusArrivals
                    .Where(x => x.Service == service &&
                               x.DayOfWeek >= 1 && x.DayOfWeek <= 5 &&
                               x.Bay != string.Empty)
                    .OrderByDescending(x => x.ArrivalTime)
                    .Take(2000)
                    .ToListAsync();

                // Additional filtering in memory if needed
                historicalData = [.. historicalData.Where(x => !string.IsNullOrWhiteSpace(x.Bay) &&
                                                         x.Bay.Length >= 2 &&
                                                         !x.Bay.Equals("No historical data", StringComparison.OrdinalIgnoreCase))];

                if (historicalData.Count == 0)
                {
                    return [new BayPrediction { Bay = "No historical data", Probability = 0 }];
                }

                // Analysis windows
                TimeSpan targetTimeOfDay = targetTime.TimeOfDay;
                TimeSpan timeWindow = TimeSpan.FromMinutes(15);
                int weekWindow = 4; // Consider data within 4 weeks before/after target week

                BusArrival latestArrival = historicalData[0];

                List<BayPrediction> bayStats = [.. historicalData
                    .GroupBy(a => a.Bay)
                    .Select(g => new
                    {
                        Bay = g.Key,
                        TotalOccurrences = g.Count(),
                        TimeMatches = g.Count(a => Math.Abs((a.ArrivalTime.TimeOfDay - targetTimeOfDay).TotalMinutes) <= timeWindow.TotalMinutes),
                        RecentMatches = g.Count(a => (targetTime - a.ArrivalTime).TotalDays <= 30),
                        WeatherMatches = g.Count(a => a.Weather == latestArrival.Weather),
                        SeasonalMatches = g.Count(a =>
                        {
                            int arrivalWeek = cal.GetWeekOfYear(a.ArrivalTime, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
                            int weekDiff = Math.Abs(arrivalWeek - currentWeek);
                            if (weekDiff > 26) weekDiff = 52 - weekDiff; // Handle year wraparound
                            return weekDiff <= weekWindow;
                        }),
                        SchoolTermMatches = g.Count(a => a.IsSchoolTerm == latestArrival.IsSchoolTerm)
                    })
                    .Select(x => new BayPrediction
                    {
                        Bay = x.Bay,
                        Probability = CalculateWeightedProbability(
                            timeMatches: x.TimeMatches,
                            recentMatches: x.RecentMatches,
                            weatherMatches: x.WeatherMatches,
                            seasonalMatches: x.SeasonalMatches,
                            schoolTermMatches: x.SchoolTermMatches,
                            totalOccurrences: x.TotalOccurrences,
                            totalRecords: historicalData.Count)
                    })
                    .OrderByDescending(x => x.Probability)
                    .Where(x => x.Probability > 15)
                    .Take(3)];

                return bayStats.Count > 0 ? bayStats :
                    [new BayPrediction { Bay = "No predictions", Probability = 0 }];
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to process prediction data", ex);
            }
        }

        private static int CalculateWeightedProbability(
            int timeMatches,
            int recentMatches,
            int weatherMatches,
            int seasonalMatches,
            int schoolTermMatches,
            int totalOccurrences,
            int totalRecords)
        {
            if (totalRecords == 0 || totalOccurrences == 0) return 0;

            // Weighted factors
            const double TimeWeight = 0.35;
            const double RecencyWeight = 0.25;
            const double WeatherWeight = 0.15;
            const double SeasonalWeight = 0.15;
            const double SchoolTermWeight = 0.10;

            double baseProbability = (double)totalOccurrences / totalRecords;

            double timeFactor = (double)timeMatches / totalOccurrences;
            double recencyFactor = (double)recentMatches / totalRecords;
            double weatherFactor = (double)weatherMatches / totalOccurrences;
            double seasonalFactor = (double)seasonalMatches / totalOccurrences;
            double schoolTermFactor = (double)schoolTermMatches / totalOccurrences;

            // Combined weighted probability
            double probability =
                (TimeWeight * timeFactor) +
                (RecencyWeight * recencyFactor) +
                (WeatherWeight * weatherFactor) +
                (SeasonalWeight * seasonalFactor) +
                (SchoolTermWeight * schoolTermFactor) +
                (0.1 * baseProbability); // Add base probability as prior

            return (int)Math.Round(Math.Clamp(probability * 100, 0, 100));
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_dbContext is IDisposable && !_dbContext.GetType().Name.Contains("Proxy", StringComparison.OrdinalIgnoreCase))
                    {
                        _dbContext.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}