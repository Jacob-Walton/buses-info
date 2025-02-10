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
            string? cachedData = await _cache.GetStringAsync(BUS_INFO_CACHE_KEY);
            return cachedData != null
                ? JsonSerializer.Deserialize<BusInfoResponse>(cachedData)
                    ?? await FetchAndCacheBusInfoAsync()
                : await FetchAndCacheBusInfoAsync();
        }

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

        private async Task<BusInfoResponse> FetchBusInfoAsync()
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
                                Bay = string.IsNullOrWhiteSpace(bay) ? default : bay
                            };
                        }
                    }
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
        public async Task<BusPredictionResponse> GetBusPredictionsAsync()
        {
            string cacheKey = $"{PREDICTION_CACHE_KEY}_{DateTime.UtcNow:yyyyMMddHHmm}";
            string? cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<BusPredictionResponse>(cached);

            BusInfoResponse currentInfo = await GetBusInfoAsync();
            List<string> candidateServices = [.. currentInfo.BusData.Select(kv => kv.Key)];

            BusPredictionResponse response = await ComputeEnhancedPredictionsAsync(candidateServices);
            response.LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(45)
            });

            return response;
        }

        private async Task<BusPredictionResponse> ComputeEnhancedPredictionsAsync(List<string> services)
        {
            BusPredictionResponse response = new();
            if (services.Count == 0 || IsWeekend())
                return HandleSpecialCases(services, response);

            DateTime now = DateTime.UtcNow;

            // First try with strict conditions
            List<BusArrival> historicalData = await _dbContext.BusArrivals
                .Where(ba => services.Contains(ba.Service) &&
                             ba.ArrivalTime >= now.AddDays(-28) &&
                             !string.IsNullOrEmpty(ba.Bay))
                .OrderByDescending(ba => ba.ArrivalTime)
                .ToListAsync();

            if (historicalData.Count == 0)
            {
                // Try again with more relaxed conditions
                historicalData = await _dbContext.BusArrivals
                    .Where(ba => services.Contains(ba.Service) &&
                                !string.IsNullOrEmpty(ba.Bay))
                    .OrderByDescending(ba => ba.ArrivalTime)
                    .Take(1000) // Limit the results
                    .ToListAsync();
            }

            if (historicalData.Count == 0)
            {
                return HandleSpecialCases(services, response);
            }

            // Group by service and analyze patterns with simpler logic first
            var servicePatterns = historicalData
                .GroupBy(h => h.Service)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var bayGroups = g.GroupBy(x => x.Bay)
                            .OrderByDescending(x => x.Count())
                            .Take(3)
                            .Select(bg => new
                            {
                                Bay = bg.Key,
                                Count = bg.Count(),
                            });

                        int totalArrivals = g.Count();
                        return bayGroups.Select(bg => new
                        {
                            bg.Bay,
                            Score = (double)bg.Count / totalArrivals
                        });
                    });

            foreach (string service in services)
            {
                response.Predictions[service] = servicePatterns.TryGetValue(service, out var patterns)
                    ? new PredictionInfo
                    {
                        Predictions = [.. patterns
                            .Select(p => new BayPrediction
                            {
                                Bay = p.Bay,
                                Probability = (int)Math.Round(p.Score * 100)
                            })]
                    }
                    : new PredictionInfo
                    {
                        Predictions = [new BayPrediction { Bay = "No data for service", Probability = 0 }]
                    };
            }

            return response;
        }

        private static BusPredictionResponse HandleSpecialCases(List<string> services, BusPredictionResponse response)
        {
            foreach (string service in services)
            {
                response.Predictions[service] = new PredictionInfo
                {
                    Predictions = [new BayPrediction
                    {
                        Bay = IsWeekend() ? "No weekend service" : "No historical data",
                        Probability = 0
                    }]
                };
            }
            return response;
        }

        private static bool IsWeekend() =>
            DateTime.UtcNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

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

    internal record ArrivalRecord(string Service, DateTime ArrivalTime, DateTime Date);

    internal class ServiceArrivalProfile(string name)
    {
        public string Name { get; } = name;
        public int TotalAppearances { get; set; }
        public Dictionary<string, int> BeforeCounts { get; } = [];
        public Dictionary<string, double> ArrivalProbabilities { get; } = [];
    }
}