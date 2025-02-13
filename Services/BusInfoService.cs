using System;
using System.Collections.Concurrent;
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
using DnsClient.Internal;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace BusInfo.Services
{
    /// <summary>
    /// Provides functionality to fetch and manage bus information and arrival predictions.
    /// </summary>
    public sealed class BusInfoService(
        IHttpClientFactory clientFactory,
        IDistributedCache cache,
        ApplicationDbContext dbContext) : IBusInfoService, IDisposable
    {
        private const string BUS_INFO_URL = "https://webservices.runshaw.ac.uk/bus/BusDepartures.aspx";
        private const string CACHE_KEY_LEGACY = "BusInfoCacheLegacy";
        private const string CACHE_KEY = "BusInfoCache";
        private const string PREDICTION_CACHE_KEY = "BusPrediction_";
        private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);

        private readonly IHttpClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ApplicationDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private bool _disposed;

        private static string Escape(string input)
        {
            return input.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<BusInfoResponse> GetBusInfoAsync()
        {
            string? cachedData = await _cache.GetStringAsync(CACHE_KEY);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonSerializer.Deserialize<BusInfoResponse>(cachedData) ?? await FetchBusInfoAsync();
            }

            BusInfoResponse busInfo = await FetchBusInfoAsync();

            await _cache.SetStringAsync(
                CACHE_KEY,
                JsonSerializer.Serialize(busInfo),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CACHE_EXPIRATION
                });

            return busInfo;
        }

        private async Task<BusInfoResponse> FetchBusInfoAsync()
        {
            using HttpClient client = _clientFactory.CreateClient();
            using CancellationTokenSource cts = new(REQUEST_TIMEOUT);

            if (IsWeekend())
            {
                return new BusInfoResponse
                {
                    BusData = [],
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    Status = "No weekend service"
                };
            }

            try
            {
                string response = await client.GetStringAsync(new Uri(BUS_INFO_URL), cts.Token);

                HtmlDocument doc = new();
                doc.LoadHtml(response);

                Dictionary<string, BusStatus> busData = [];

                HtmlNodeCollection rows = doc.DocumentNode.SelectNodes("//table[@id='grdAll']//tr");
                if (rows != null)
                {
                    foreach (HtmlNode? row in rows)
                    {
                        HtmlNodeCollection cells = row.SelectNodes("td");
                        if (cells != null && cells.Count >= 3)
                        {
                            string service = cells[0].InnerText.Trim();
                            string bay = Escape(cells[2].InnerText.Trim()).Trim();

                            busData[service] = new BusStatus
                            {
                                Status = string.IsNullOrWhiteSpace(bay) ? "Not arrived" : "Arrived",
                                Bay = string.IsNullOrWhiteSpace(bay) ? null : bay
                            };
                        }
                    }
                }

                return new BusInfoResponse
                {
                    BusData = busData,
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                    Status = "OK"
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to fetch bus info", ex);
            }
        }

        public async Task<BusInfoLegacyResponse> GetLegacyBusInfoAsync()
        {
            string? cachedData = await _cache.GetStringAsync(CACHE_KEY_LEGACY);
            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonSerializer.Deserialize<BusInfoLegacyResponse>(cachedData) ?? await FetchLegacyBusInfoAsync();
            }

            BusInfoLegacyResponse busInfo = await FetchLegacyBusInfoAsync();

            await _cache.SetStringAsync(
                CACHE_KEY_LEGACY,
                JsonSerializer.Serialize(busInfo),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CACHE_EXPIRATION
                });

            return busInfo;
        }

        private async Task<BusInfoLegacyResponse> FetchLegacyBusInfoAsync()
        {
            using HttpClient client = _clientFactory.CreateClient();
            using CancellationTokenSource cts = new(REQUEST_TIMEOUT);

            try
            {
                string response = await client.GetStringAsync(new Uri(BUS_INFO_URL), cts.Token);

                HtmlDocument doc = new();
                doc.LoadHtml(response);

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
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to fetch legacy bus info", ex);
            }
        }

        /// <summary>
        /// Gets predictions for bus bay assignments based on historical data.
        /// </summary>
        /// <returns>A task containing predictions for each active bus service.</returns>
        /// <remarks>
        /// Predictions are cached for 45 seconds to avoid excessive database queries.
        /// No predictions are made for weekend services.
        /// </remarks>
        public async Task<BusPredictionResponse> GetBusPredictionsAsync()
        {
            string cacheKey = $"{PREDICTION_CACHE_KEY}_{DateTime.UtcNow:yyyyMMddHHmm}";
            string? cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<BusPredictionResponse>(cached) ?? new BusPredictionResponse();

            BusInfoResponse currentInfo = await GetBusInfoAsync();
            List<string> candidateServices = [.. currentInfo.BusData.Select(kv => kv.Key)];

            BusPredictionResponse response = await GetBusPredictionDataAsync(candidateServices);
            response.LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45)
            });

            return response;
        }

        /// <summary>
        /// Computes predictions using historical data.
        /// </summary>
        /// <param name="services">List of bus services to generate predictions for.</param>
        private async Task<BusPredictionResponse> GetBusPredictionDataAsync(List<string> services)
        {
            BusPredictionResponse response = new()
            {
                Predictions = []
            };

            if (services.Count == 0 || IsWeekend())
                return HandleSpecialCases(services, response);

            DateTime now = DateTime.UtcNow;
            int currentDayOfWeek = (int)now.DayOfWeek;

            var historicalData = await _dbContext.BusArrivals
                .AsNoTracking()
                .Where(ba => services.Contains(ba.Service) &&
                            ba.ArrivalTime >= now.AddDays(-28) &&
                            ba.DayOfWeek == currentDayOfWeek &&
                            !string.IsNullOrEmpty(ba.Bay))
                .Select(ba => new { ba.Service, ba.Bay, ba.ArrivalTime })
                .ToListAsync();

            if (historicalData.Count == 0)
            {
                historicalData = await _dbContext.BusArrivals
                    .AsNoTracking()
                    .Where(ba => services.Contains(ba.Service) &&
                                !string.IsNullOrEmpty(ba.Bay))
                    .Select(ba => new { ba.Service, ba.Bay, ba.ArrivalTime })
                    .Take(1000)
                    .ToListAsync();
            }

            if (historicalData.Count == 0)
                return HandleSpecialCases(services, response);

            // Process services sequentially
            foreach (string service in services)
            {
                if (string.IsNullOrEmpty(service)) continue;

                var serviceData = historicalData.Where(h => h.Service == service).ToList();
                if (serviceData.Count == 0)
                {
                    response.Predictions[service] = new PredictionInfo
                    {
                        Predictions = [new() { Bay = "No historical data", Probability = 0 }]
                    };
                    continue;
                }

                // Group and calculate probabilities
                List<BayPrediction> bayGroups = [.. serviceData
                    .GroupBy(x => x.Bay)
                    .Select(g =>
                    {
                        int totalCount = serviceData.Count;
                        int bayCount = g.Count();
                        int recentCount = g.Count(x => x.ArrivalTime >= now.AddDays(-7));
                        double score = (((double)bayCount / totalCount) + ((double)recentCount / Math.Max(1, g.Count()))) / 2;
                        return new BayPrediction
                        {
                            Bay = g.Key ?? "Unknown",
                            Probability = (int)Math.Round(score * 100)
                        };
                    })
                    .OrderByDescending(x => x.Probability)
                    .Take(3)];

                response.Predictions[service] = new PredictionInfo
                {
                    Predictions = bayGroups,
                    OverallConfidence = CalculateOverallConfidence(bayGroups)
                };
            }

            return response;
        }

        private static BusPredictionResponse HandleSpecialCases(List<string> services, BusPredictionResponse response)
        {
            foreach (string service in services.Where(s => !string.IsNullOrEmpty(s)))
            {
                response.Predictions[service] = new PredictionInfo
                {
                    Predictions =
                    [
                        new()
                        {
                            Bay = IsWeekend() ? "No weekend service" : "No historical data",
                            Probability = 0
                        }
                    ]
                };
            }
            return response;
        }

        /// <summary>
        /// Checks if the current day is a weekend.
        /// </summary>
        private static bool IsWeekend() =>
            DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _dbContext is IDisposable && !_dbContext.GetType().Name.Contains("Proxy", StringComparison.OrdinalIgnoreCase))
                {
                    _dbContext.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Releases resources used by the service.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private static int CalculateOverallConfidence(List<BayPrediction> predictions)
        {
            if (predictions.Count == 0) return 0;
            if (predictions.Count == 1) return predictions[0].Probability;

            List<BayPrediction> orderedPredictions = [.. predictions.OrderByDescending(p => p.Probability)];
            int highestProb = orderedPredictions[0].Probability;
            int secondProb = orderedPredictions[1].Probability;

            // Both probabilities are high (>70%)
            if (highestProb >= 70 && secondProb >= 70)
            {
                return Math.Max(70, highestProb - 10); // Keep high confidence but slightly reduced
            }

            // Equal probabilities
            if (highestProb == secondProb)
            {
                return (int)(highestProb * 0.8); // 80% of highest prob when equal
            }

            // Calculate confidence based on difference
            int difference = highestProb - secondProb;
            double ratio = difference / (double)highestProb;

            // More gradual confidence reduction
            return (int)(highestProb * (0.7 + (ratio * 0.3)));
        }
    }
}