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

            using CancellationTokenSource cts = new(REQUEST_TIMEOUT);
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    // Fetch and parse the HTML document
                    using HttpResponseMessage response = await client.GetAsync(new Uri(BUS_INFO_URL), cts.Token);
                    response.EnsureSuccessStatusCode();
                    string content = await response.Content.ReadAsStringAsync(cts.Token);
                    HtmlDocument doc = new();
                    doc.LoadHtml(content);

                    // Extract bus data from the table
                    Dictionary<string, BusStatus> busData = doc.DocumentNode
                        .SelectNodes("//table[@id='grdAll']//tr[td]")
                        ?.AsParallel()
                        .Select(static row =>
                        {
                            // Extract service number and bay information from each row
                            HtmlNodeCollection cells = row.SelectNodes("td");
                            return cells?.Count >= 3
                                ? (new
                                {
                                    Service = cells[0].InnerText.Trim(), // Service number
                                    Bay = Escape(cells[2].InnerText).Trim() // Bay information
                                })
                                : null; // Skip invalid rows
                        })
                        ?.Where(x => x != null)
                        ?.ToDictionary(
                            x => x!.Service,
                            // Convert bay information into status - "Arrived" if bay is set, "Not arrived" if empty
                            x => new BusStatus
                            {
                                Status = string.IsNullOrWhiteSpace(x!.Bay) ? "Not arrived" : "Arrived", // Set status to "Not arrived" if bay is empty
                                Bay = string.IsNullOrWhiteSpace(x.Bay) ? default : x.Bay // Set bay to null if empty (will be omitted in JSON)
                            }) ?? []; // Default to empty dictionary if no valid rows are found

                    return new BusInfoResponse
                    {
                        BusData = busData,
                        LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                    };
                }
                catch (Exception ex)
                {
                    // Retry after a delay if the request fails
                    retryCount++;
                    if (retryCount == maxRetries)
                        throw new InvalidOperationException($"Failed to fetch bus info after {maxRetries} attempts", ex);

                    await Task.Delay(1000 * retryCount, cts.Token);
                }
            }

            throw new InvalidOperationException("Failed to fetch bus info");
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
            BusPredictionResponse response = new();
            if (services.Count == 0 || IsWeekend())
                return HandleSpecialCases(services, response);

            DateTime now = DateTime.UtcNow;

            // First attempt: Get recent data from the last 28 days
            List<BusArrival> historicalData = await _dbContext.BusArrivals
                .Where(ba => services.Contains(ba.Service) &&
                             ba.ArrivalTime >= now.AddDays(-28) &&
                             !string.IsNullOrEmpty(ba.Bay))
                .OrderByDescending(ba => ba.ArrivalTime)
                .ToListAsync();

            // Fallback: If no recent data, get the last 1000 records regardless of date
            if (historicalData.Count == 0)
            {
                historicalData = await _dbContext.BusArrivals
                    .Where(ba => services.Contains(ba.Service) &&
                                !string.IsNullOrEmpty(ba.Bay))
                    .OrderByDescending(ba => ba.ArrivalTime)
                    .Take(1000)
                    .ToListAsync();
            }

            if (historicalData.Count == 0)
            {
                return HandleSpecialCases(services, response);
            }

            // Calculate bay assignment patterns and probabilities
            Dictionary<string, IEnumerable<(string Bay, double Score)>> servicePatterns = historicalData
                .GroupBy(h => h.Service)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // Get top 3 most common bays for each service
                        IEnumerable<(string Bay, int Count)> bayGroups = g.GroupBy(x => x.Bay)
                            .OrderByDescending(x => x.Count())
                            .Take(3)
                            .Select(bg => (
                                Bay: bg.Key,
                                Count: bg.Count()
                            ));

                        // Calculate probability scores based on historical frequency
                        int totalArrivals = g.Count();
                        return bayGroups.Select(bg => (bg.Bay,
                            Score: (double)bg.Count / totalArrivals
                        ));
                    });

            // Build prediction response for each service
            foreach (string service in services)
            {
                response.Predictions[service] = servicePatterns.TryGetValue(service, out IEnumerable<(string Bay, double Score)>? patterns)
                    ? new PredictionInfo
                    {
                        // Convert probability scores to percentages
                        Predictions = new ReadOnlyCollection<BayPrediction>([.. patterns
                            .Select(p => new BayPrediction
                            {
                                Bay = p.Bay,
                                Probability = (int)Math.Round(p.Score * 100)
                            })])
                    }
                    : new PredictionInfo
                    {
                        Predictions = new ReadOnlyCollection<BayPrediction>(
                        [
                            new() { Bay = "No data for service", Probability = 0 } // No data available for this service
                        ])
                    };
            }

            return response;
        }

        /// <summary>
        /// Handles special cases when predictions cannot be made.
        /// </summary>
        /// <param name="services">List of bus services.</param>
        /// <param name="response">Response object to populate.</param>
        private static BusPredictionResponse HandleSpecialCases(List<string> services, BusPredictionResponse response)
        {
            foreach (string service in services)
            {
                response.Predictions[service] = new PredictionInfo
                {
                    Predictions = new ReadOnlyCollection<BayPrediction>(
                    [
                        new()
                        {
                            Bay = IsWeekend() ? "No weekend service" : "No historical data",
                            Probability = 0
                        }
                    ])
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