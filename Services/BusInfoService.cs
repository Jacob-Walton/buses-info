using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BusInfo.Data;
using BusInfo.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;

namespace BusInfo.Services
{
    public class BusInfoService : IBusInfoService
    {
        private const string BUS_INFO_URL = "https://webservices.runshaw.ac.uk/bus/BusDepartures.aspx";
        private const string CACHE_KEY_LEGACY = "BusInfoCacheLegacy";
        private static readonly TimeSpan CACHE_EXPIRATION = TimeSpan.FromSeconds(30);

        private readonly IHttpClientFactory _clientFactory;
        private readonly IDistributedCache _cache;
        private readonly IMongoCollection<BusStatusMongo> _busStatusCollection;

        public BusInfoService(
            IHttpClientFactory clientFactory,
            IDistributedCache cache,
            IConfiguration configuration)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));

            ArgumentNullException.ThrowIfNull(configuration);

            try
            {
                using MongoClient mongoClient = new(configuration.GetConnectionString("MongoDb"));

                _busStatusCollection = mongoClient.GetDatabase("bus-bot").GetCollection<BusStatusMongo>("busarrivals");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialise MongoDB", ex);
            }
        }

        private static string Escape(string input)
        {
            return input.Replace("&nbsp;", " ", StringComparison.InvariantCultureIgnoreCase);
        }

        public async Task<BusInfoLegacyResponse> GetLegacyBusInfoAsync()
        {
            string cachedData = await _cache.GetStringAsync(CACHE_KEY_LEGACY);
            if (cachedData != null)
            {
                return JsonSerializer.Deserialize<BusInfoLegacyResponse>(cachedData);
            }

            BusInfoLegacyResponse response = await FetchLegacyBusInfoAsync();

            DistributedCacheEntryOptions cacheOptions = new DistributedCacheEntryOptions()
                .SetAbsoluteExpiration(CACHE_EXPIRATION);

            string serializedData = JsonSerializer.Serialize(response);
            await _cache.SetStringAsync(CACHE_KEY_LEGACY, serializedData, cacheOptions);

            return response;
        }

        private async Task<BusInfoLegacyResponse> FetchLegacyBusInfoAsync()
        {
            try
            {
                using HttpClient client = _clientFactory.CreateClient();
                string response = await client.GetStringAsync(new Uri(BUS_INFO_URL));

                HtmlDocument doc = new();
                doc.LoadHtml(response);

                Dictionary<string, string> busData = [];

                HtmlNodeCollection rows = doc.DocumentNode.SelectNodes("//table[@id='grdAll']//tr");
                if (rows != null)
                {
                    foreach (HtmlNode row in rows)
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
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Failed to fetch bus info", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse bus info", ex);
            }
        }

        public async Task<BusInfoResponse> GetBusInfoAsync()
        {
            try
            {
                using HttpClient client = _clientFactory.CreateClient();
                string response = await client.GetStringAsync(new Uri(BUS_INFO_URL));

                HtmlDocument doc = new();
                doc.LoadHtml(response);

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
                // Get all historical data for this service on weekdays
                FilterDefinition<BusStatusMongo> filter = Builders<BusStatusMongo>.Filter.And(
                    Builders<BusStatusMongo>.Filter.Eq(x => x.Service, service),
                    Builders<BusStatusMongo>.Filter.In(x => x.DayOfWeek, [1, 2, 3, 4, 5]),
                    Builders<BusStatusMongo>.Filter.Ne(x => x.Bay, string.Empty),
                    Builders<BusStatusMongo>.Filter.Regex(x => x.Bay, new BsonRegularExpression("^[ABCT][0-9]{1,2}$"))
                );

                List<BusStatusMongo> historicalData = await _busStatusCollection
                    .Find(filter)
                    .SortByDescending(a => a.ArrivalTime)
                    .Limit(1000)
                    .ToListAsync();

                if (historicalData.Count == 0)
                {
                    return [new BayPrediction { Bay = "No historical data", Probability = 0 }];
                }

                // Temporal analysis windows
                TimeSpan targetTimeOfDay = targetTime.TimeOfDay;
                TimeSpan timeWindow = TimeSpan.FromMinutes(15);

                // Bayesian probability calculation
                List<BayPrediction> bayStats = [.. historicalData
                    .GroupBy(a => a.Bay)
                    .Select(g => new
                    {
                        Bay = g.Key,
                        TotalOccurrences = g.Count(),
                        TimeMatches = g.Count(a => Math.Abs((a.ArrivalTime.TimeOfDay - targetTimeOfDay).TotalMinutes) <= timeWindow.TotalMinutes),
                        RecentMatches = g.Count(a => (DateTime.UtcNow - a.ArrivalTime).TotalDays <= 30)
                    })
                    .Select(x => new BayPrediction
                    {
                        Bay = x.Bay,
                        Probability = CalculateBayesianProbability(
                            timeMatches: x.TimeMatches,
                            recentMatches: x.RecentMatches,
                            totalOccurrences: x.TotalOccurrences,
                            totalRecords: historicalData.Count)
                    })
                    .OrderByDescending(x => x.Probability)
                    .Take(3)];

                return bayStats.Count > 0 ? bayStats :
                    [new BayPrediction { Bay = "No predictions", Probability = 0 }];
            }
            catch (MongoException ex)
            {
                throw new InvalidOperationException("Failed to fetch historical bus data", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to process prediction data", ex);
            }
        }

        private static int CalculateBayesianProbability(int timeMatches, int recentMatches, int totalOccurrences, int totalRecords)
        {
            // Bayesian probability with temporal weighting
            double timeWeight = 0.7;  // Weight for time window matches
            double recencyWeight = 0.3; // Weight for recent matches

            // Avoid division by zero
            if (totalRecords == 0) return 0;

            double baseProbability = (double)totalOccurrences / totalRecords;
            double timeFactor = (double)timeMatches / totalOccurrences;
            double recencyFactor = (double)recentMatches / totalRecords;

            // Combined probability calculation
            double probability = (timeWeight * timeFactor) +
                               (recencyWeight * recencyFactor) +
                               (0.2 * baseProbability); // Add base probability as prior

            // Normalize to 0-100 scale
            return (int)Math.Round(Math.Clamp(probability * 100, 0, 100));
        }
    }
}