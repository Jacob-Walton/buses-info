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
        private const string BUS_INFO_CACHE_KEY = "CurrentBusInfo";
        private const string PREDICTION_CACHE_KEY = "BusPrediction_";
        private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan REQUEST_TIMEOUT = TimeSpan.FromSeconds(10);
        private readonly IHttpClientFactory _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ApplicationDbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private bool _disposed;

        private static string Escape(string input)
        {
            return input.AsSpan().Trim().ToString().Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets bus information in the legacy format from the web service.
        /// </summary>
        /// <returns>A task containing bus information in the legacy response format.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the request times out or parsing fails.</exception>
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

        /// <summary>
        /// Fetches bus information in legacy format directly from the web service.
        /// </summary>
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

                HtmlNodeCollection rows = doc.DocumentNode.SelectNodes("//table[@id='grdAll']//tr"); // Get table rows
                if (rows != null) // Check if table rows exist
                {
                    foreach (HtmlNode? row in rows) // Iterate over table rows
                    {
                        HtmlNodeCollection cells = row.SelectNodes("td"); // Get table cells
                        if (cells != null && cells.Count >= 3)
                        {
                            string service = cells[0].InnerText.Trim(); // Service number
                            string status = Escape(cells[2].InnerText.Trim()).Trim(); // Bay information

                            if (string.IsNullOrWhiteSpace(status)) // Set status to "Not arrived" if bay is empty
                            {
                                status = "Not arrived";
                            }
                            busData[service] = status; // Add to dictionary
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
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new InvalidOperationException("Failed to parse bus info", ex);
            }
        }

        /// <summary>
        /// Gets current bus information from the web service.
        /// </summary>
        /// <returns>A task containing the current bus information.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the request fails after retries.</exception>
        public async Task<BusInfoResponse> GetBusInfoAsync()
        {
            string? cachedData = await _cache.GetStringAsync(BUS_INFO_CACHE_KEY);
            return cachedData != null
                ? JsonSerializer.Deserialize<BusInfoResponse>(cachedData)
                    ?? await FetchAndCacheBusInfoAsync()
                : await FetchAndCacheBusInfoAsync();
        }

        /// <summary>
        /// Fetches and caches current bus information.
        /// </summary>
        private async Task<BusInfoResponse> FetchAndCacheBusInfoAsync()
        {
            BusInfoResponse busInfo = await FetchBusInfoAsync();

            DistributedCacheEntryOptions cacheOptions = new()
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                SlidingExpiration = TimeSpan.FromSeconds(10)
            };

            await _cache.SetStringAsync(
                BUS_INFO_CACHE_KEY,
                JsonSerializer.Serialize(busInfo),
                cacheOptions);

            return busInfo;
        }

        /// <summary>
        /// Fetches current bus information from the web service with retry logic.
        /// </summary>
        private async Task<BusInfoResponse> FetchBusInfoAsync()
        {
            using HttpClient client = _clientFactory.CreateClient();
            client.Timeout = REQUEST_TIMEOUT;

            int retryCount = 0;
            const int maxRetries = 3;
            Exception? lastException = null;

            while (retryCount < maxRetries)
            {
                using CancellationTokenSource cts = new(REQUEST_TIMEOUT);
                try
                {
                    using HttpResponseMessage response = await client.GetAsync(new Uri(BUS_INFO_URL), cts.Token);
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync(cts.Token);

                    HtmlDocument doc = new();
                    doc.LoadHtml(content);

                    ConcurrentDictionary<string, BusStatus> busData = new();
                    HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//table[@id='grdAll']//tr[td]");

                    if (nodes != null)
                    {
                        foreach (HtmlNode row in nodes)
                        {
                            HtmlNodeCollection cells = row.SelectNodes("td");
                            if (cells != null && cells.Count >= 3)
                            {
                                string service = cells[0].InnerText.Trim();
                                string bay = Escape(cells[2].InnerText).Trim();

                                busData.TryAdd(service, new BusStatus
                                {
                                    Status = string.IsNullOrWhiteSpace(bay) ? "Not arrived" : "Arrived",
                                    Bay = string.IsNullOrWhiteSpace(bay) ? default : bay
                                });
                            }
                        }
                    }

                    return new BusInfoResponse
                    {
                        BusData = new Dictionary<string, BusStatus>(busData),
                        LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    };
                }
                catch (Exception ex)
                {
                    retryCount++;
                    lastException = ex;
                    if (retryCount == maxRetries)
                        throw new InvalidOperationException($"Failed to fetch bus info after {maxRetries} attempts", ex);

                    await Task.Delay(1000 * retryCount);
                }
            }

            throw new InvalidOperationException("Failed to fetch bus info", lastException);
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
                Predictions = [] // Initialize dictionary
            };

            if (services.Count == 0 || IsWeekend())
                return HandleSpecialCases(services, response);

            DateTime now = DateTime.UtcNow;
            int currentDayOfWeek = (int)now.DayOfWeek;

            // Get historical data
            var historicalData = await _dbContext.BusArrivals
                .Where(ba => services.Contains(ba.Service) &&
                            ba.ArrivalTime >= now.AddDays(-28) &&
                            ba.DayOfWeek == currentDayOfWeek &&
                            !string.IsNullOrEmpty(ba.Bay))
                .Select(ba => new { ba.Service, ba.Bay, ba.ArrivalTime })
                .ToListAsync();

            // If no recent data, get any data for these services
            if (historicalData.Count == 0)
            {
                historicalData = await _dbContext.BusArrivals
                    .Where(ba => services.Contains(ba.Service) &&
                                !string.IsNullOrEmpty(ba.Bay))
                    .Select(ba => new { ba.Service, ba.Bay, ba.ArrivalTime })
                    .Take(1000)
                    .ToListAsync();
            }

            if (historicalData.Count == 0)
                return HandleSpecialCases(services, response);

            foreach (string service in services)
            {
                if (string.IsNullOrEmpty(service)) continue; // Skip null/empty services

                var serviceData = historicalData.Where(h => h.Service == service).ToList();
                if (serviceData.Count == 0)
                {
                    response.Predictions[service] = new PredictionInfo
                    {
                        Predictions =
                        [
                            new() { Bay = "No historical data", Probability = 0 }
                        ]
                    };
                    continue;
                }

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

                // Calculate overall confidence based on probability distribution
                int overallConfidence = CalculateOverallConfidence(bayGroups);

                response.Predictions[service] = new PredictionInfo
                {
                    Predictions = bayGroups,
                    OverallConfidence = overallConfidence
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

    /// <summary>
    /// Represents a record of bus arrivals for analysis.
    /// </summary>
    /// <param name="Service">The bus service identifier.</param>
    /// <param name="ArrivalTime">The time the bus arrived.</param>
    /// <param name="Date">The date of the arrival.</param>
    internal record ArrivalRecord(string Service, DateTime ArrivalTime, DateTime Date);

    /// <summary>
    /// Maintains arrival statistics for a specific bus service.
    /// </summary>
    /// <param name="name">The name of the bus service.</param>
    internal class ServiceArrivalProfile(string name)
    {
        public string Name { get; } = name;
        public int TotalAppearances { get; set; }
        public Dictionary<string, int> BeforeCounts { get; } = [];
        public Dictionary<string, double> ArrivalProbabilities { get; } = [];
    }
}