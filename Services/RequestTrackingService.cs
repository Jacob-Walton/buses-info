// Services/RequestTrackingService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusInfo.Models;
using StackExchange.Redis;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BusInfo.Services
{
    public class RequestTrackingService : IRequestTrackingService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RequestTrackingService> _logger;
        
        // Key patterns for Redis
        private const string API_REQUESTS_KEY = "metrics:api:requests:total";
        private const string API_REQUESTS_24H_KEY = "metrics:api:requests:24h";
        private const string API_RESPONSE_TIMES_KEY = "metrics:api:response_times";
        private const string API_REQUESTS_COUNT_KEY = "metrics:api:requests_count";
        private const string API_ENDPOINT_KEY_PREFIX = "metrics:api:endpoint:";
        private const string API_STATUS_CODE_KEY_PREFIX = "metrics:api:status:";
        private const string API_USER_REQUEST_KEY_PREFIX = "metrics:api:user:";
        private const string API_KEY_REQUEST_KEY_PREFIX = "metrics:api:key:";
        private const string API_KEY_RESPONSE_TIME_KEY_PREFIX = "metrics:api:key:time:";
        private const string API_ERROR_COUNT_KEY = "metrics:api:errors";
        private const string API_HOURLY_STATS_KEY_PREFIX = "metrics:api:hourly:";

        public RequestTrackingService(IConnectionMultiplexer redis, ILogger<RequestTrackingService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task IncrementApiRequestCountAsync()
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                var now = DateTime.UtcNow;

                // Increment total requests
                await db.StringIncrementAsync(API_REQUESTS_KEY);

                // Add to sorted set with timestamp score for 24h tracking
                await db.SortedSetAddAsync(API_REQUESTS_24H_KEY, now.Ticks.ToString(), now.Ticks);

                // Remove old entries (older than 24h)
                var dayAgo = DateTime.UtcNow.AddHours(-24).Ticks;
                await db.SortedSetRemoveRangeByScoreAsync(API_REQUESTS_24H_KEY, 0, dayAgo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing API request count");
            }
        }

        public async Task RecordResponseTimeAsync(double milliseconds)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();

                ITransaction transaction = db.CreateTransaction();

                _ = transaction.StringIncrementAsync(API_REQUESTS_COUNT_KEY);
                _ = transaction.StringIncrementAsync(API_RESPONSE_TIMES_KEY, (long)milliseconds);

                bool success = await transaction.ExecuteAsync();
                if (!success)
                {
                    _logger.LogWarning("Failed to record response time");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording response time");
            }
        }

        public async Task<RequestMetrics> GetMetricsAsync()
        {
            try
            {
                IDatabase db = _redis.GetDatabase();

                // Get total counts
                RedisValue totalRequests = await db.StringGetAsync(API_REQUESTS_KEY);
                RedisValue totalResponseTime = await db.StringGetAsync(API_RESPONSE_TIMES_KEY);
                RedisValue responseCount = await db.StringGetAsync(API_REQUESTS_COUNT_KEY);
                RedisValue errorCount = await db.StringGetAsync(API_ERROR_COUNT_KEY);

                // Get 24h request count
                var requests24h = await db.SortedSetLengthAsync(API_REQUESTS_24H_KEY);

                double avgResponseTime = 0.0;
                if (responseCount.HasValue && (long)responseCount > 0)
                {
                    avgResponseTime = (double)totalResponseTime / (long)responseCount;
                }

                return new RequestMetrics
                {
                    TotalRequests = (long)(totalRequests.HasValue ? totalRequests : 0),
                    Requests24Hours = requests24h,
                    AverageResponseTime = Math.Round(avgResponseTime, 2),
                    ErrorCount = (int)(errorCount.HasValue ? errorCount : 0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting API metrics");
                return new RequestMetrics();
            }
        }

        public async Task TrackApiRequestAsync(ApiRequestInfo requestInfo)
        {
            try
            {
                _logger.LogInformation("Tracking API request: {Endpoint}, {StatusCode}, {ResponseTime}, {UserId}, {ApiKey}",
                    requestInfo.Endpoint, requestInfo.StatusCode, requestInfo.ResponseTimeMs, requestInfo.UserId, requestInfo.ApiKey);
                IDatabase db = _redis.GetDatabase();
                var now = DateTime.UtcNow;
                var hourKey = $"{now:yyyy-MM-dd-HH}";
                var dayKey = $"{now:yyyy-MM-dd}";

                // Start a transaction for atomic updates
                ITransaction transaction = db.CreateTransaction();

                // Track status code
                transaction.StringIncrementAsync($"{API_STATUS_CODE_KEY_PREFIX}{requestInfo.StatusCode}");
                transaction.StringIncrementAsync($"{API_STATUS_CODE_KEY_PREFIX}{requestInfo.StatusCode}:{dayKey}");

                // If error status code (4xx or 5xx)
                if (requestInfo.StatusCode >= 400)
                {
                    transaction.StringIncrementAsync(API_ERROR_COUNT_KEY);
                }

                // Track endpoint
                transaction.StringIncrementAsync($"{API_ENDPOINT_KEY_PREFIX}{requestInfo.Endpoint}");
                transaction.StringIncrementAsync($"{API_ENDPOINT_KEY_PREFIX}{requestInfo.Endpoint}:{dayKey}");

                // Track hourly stats for charts
                transaction.HashIncrementAsync($"{API_HOURLY_STATS_KEY_PREFIX}{dayKey}", hourKey);

                // If we have user ID, track user activity
                if (!string.IsNullOrEmpty(requestInfo.UserId))
                {
                    transaction.StringIncrementAsync($"{API_USER_REQUEST_KEY_PREFIX}{requestInfo.UserId}");
                    transaction.StringIncrementAsync($"{API_USER_REQUEST_KEY_PREFIX}{requestInfo.UserId}:{dayKey}");
                }

                // If we have API key, track key activity
                if (!string.IsNullOrEmpty(requestInfo.ApiKey))
                {
                    // Track total requests
                    transaction.StringIncrementAsync($"{API_KEY_REQUEST_KEY_PREFIX}{requestInfo.ApiKey}");
                    transaction.StringIncrementAsync($"{API_KEY_REQUEST_KEY_PREFIX}{requestInfo.ApiKey}:{dayKey}");
                    
                    // Track key-specific status codes
                    transaction.StringIncrementAsync($"{API_KEY_REQUEST_KEY_PREFIX}{requestInfo.ApiKey}:status:{requestInfo.StatusCode}");
                    
                    // Track hourly data for this key
                    transaction.HashIncrementAsync($"{API_KEY_REQUEST_KEY_PREFIX}{requestInfo.ApiKey}:hourly:{dayKey}", hourKey);
                    
                    // Track response time for this key
                    transaction.StringIncrementAsync($"{API_KEY_RESPONSE_TIME_KEY_PREFIX}{requestInfo.ApiKey}", (long)requestInfo.ResponseTimeMs);
                    transaction.StringIncrementAsync($"{API_KEY_RESPONSE_TIME_KEY_PREFIX}{requestInfo.ApiKey}:count");
                }

                // Execute all commands in transaction
                bool success = await transaction.ExecuteAsync();
                if (!success)
                {
                    _logger.LogWarning("Failed to track API request");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking API request");
            }
        }

        public async Task<ApiKeyMetrics> GetApiKeyMetricsAsync(string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    return new ApiKeyMetrics();
                }

                IDatabase db = _redis.GetDatabase();
                var now = DateTime.UtcNow;
                var dayKey = $"{now:yyyy-MM-dd}";

                // Get total and today's requests for the API key
                RedisValue totalRequests = await db.StringGetAsync($"{API_KEY_REQUEST_KEY_PREFIX}{apiKey}");
                RedisValue todayRequests = await db.StringGetAsync($"{API_KEY_REQUEST_KEY_PREFIX}{apiKey}:{dayKey}");

                // Get response time for this key
                RedisValue totalResponseTime = await db.StringGetAsync($"{API_KEY_RESPONSE_TIME_KEY_PREFIX}{apiKey}");
                RedisValue responseCount = await db.StringGetAsync($"{API_KEY_RESPONSE_TIME_KEY_PREFIX}{apiKey}:count");
                
                double avgResponseTime = 0;
                if (responseCount.HasValue && (long)responseCount > 0)
                {
                    avgResponseTime = (double)(long)totalResponseTime / (long)responseCount;
                }

                // If no data exists, return empty metrics
                if (!totalRequests.HasValue)
                {
                    return new ApiKeyMetrics
                    {
                        TotalRequests = 0,
                        RequestsToday = 0,
                        AverageResponseTime = 0,
                        StatusCodes = new Dictionary<int, int>(),
                        RequestsTimeSeries = new List<TimeSeriesDataPoint>()
                    };
                }

                // Get status codes used with this API key
                var statusCodes = new Dictionary<int, int>();
                var commonStatusCodes = new[] { 200, 201, 204, 400, 401, 403, 404, 500 };

                foreach (var code in commonStatusCodes)
                {
                    RedisValue codeCount = await db.StringGetAsync($"{API_KEY_REQUEST_KEY_PREFIX}{apiKey}:status:{code}");
                    if (codeCount.HasValue && (long)codeCount > 0)
                    {
                        statusCodes[code] = (int)(long)codeCount;
                    }
                }

                // Get hourly data for time series
                var hourlyData = await db.HashGetAllAsync($"{API_KEY_REQUEST_KEY_PREFIX}{apiKey}:hourly:{dayKey}");
                var timeSeries = new List<TimeSeriesDataPoint>();
                
                foreach (var entry in hourlyData)
                {
                    // Format: yyyy-MM-dd-HH
                    string hourStr = entry.Name.ToString().Split('-').Last();
                    
                    timeSeries.Add(new TimeSeriesDataPoint
                    {
                        TimeLabel = $"{hourStr}:00",
                        Value = (int)(long)entry.Value
                    });
                }
                
                // Sort by hour
                timeSeries = timeSeries.OrderBy(p => p.TimeLabel).ToList();

                return new ApiKeyMetrics
                {
                    TotalRequests = (long)totalRequests,
                    RequestsToday = todayRequests.HasValue ? (int)(long)todayRequests : 0,
                    AverageResponseTime = Math.Round(avgResponseTime, 2),
                    StatusCodes = statusCodes,
                    RequestsTimeSeries = timeSeries
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting API key metrics for {ApiKey}", apiKey);
                return new ApiKeyMetrics();
            }
        }

        public async Task<List<EndpointMetrics>> GetTopEndpointsAsync(int count = 5)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                var result = new Dictionary<string, long>();
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

                // Get all keys for endpoints
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var keys = server.Keys(pattern: $"{API_ENDPOINT_KEY_PREFIX}*").ToList();

                foreach (var key in keys)
                {
                    string keyStr = key.ToString();
                    string endpoint = keyStr.Replace(API_ENDPOINT_KEY_PREFIX, "");
                    
                    // Handle both total and daily metrics
                    if (endpoint.Contains(':'))
                    {
                        // Daily metric (format: endpoint:yyyy-MM-dd)
                        var parts = endpoint.Split(':');
                        endpoint = parts[0];
                        var date = parts[1];
                        
                        // Only process if it's today's metric
                        if (date != today) continue;
                    }

                    RedisValue countValue = await db.StringGetAsync(keyStr);
                    if (countValue.HasValue)
                    {
                        // Aggregate counts for the same endpoint
                        if (!result.ContainsKey(endpoint))
                        {
                            result[endpoint] = 0;
                        }
                        result[endpoint] += (long)countValue;
                    }
                }

                // Convert to list and sort
                return result
                    .Select(kvp => new EndpointMetrics
                    {
                        Endpoint = kvp.Key,
                        RequestCount = kvp.Value
                    })
                    .OrderByDescending(e => e.RequestCount)
                    .Take(count)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top endpoints");
                return new List<EndpointMetrics>();
            }
        }

        public async Task<Dictionary<int, int>> GetStatusCodeDistributionAsync()
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                var result = new Dictionary<int, int>();

                // Get common status codes to check
                var commonStatusCodes = new[] { 200, 201, 204, 400, 401, 403, 404, 429, 500, 503 };

                foreach (var statusCode in commonStatusCodes)
                {
                    // Get the total count for this status code
                    string totalKey = $"{API_STATUS_CODE_KEY_PREFIX}{statusCode}";
                    RedisValue totalCount = await db.StringGetAsync(totalKey);

                    // Get today's count
                    string todayKey = $"{API_STATUS_CODE_KEY_PREFIX}{statusCode}:{DateTime.UtcNow:yyyy-MM-dd}";
                    RedisValue todayCount = await db.StringGetAsync(todayKey);

                    // Use either total count or today's count, prioritizing total count
                    if (totalCount.HasValue)
                    {
                        result[statusCode] = (int)(long)totalCount;
                    }
                    else if (todayCount.HasValue)
                    {
                        result[statusCode] = (int)(long)todayCount;
                    }
                }

                // If we got no results from common status codes, try scanning for any status code keys
                if (!result.Any())
                {
                    var server = _redis.GetServer(_redis.GetEndPoints().First());
                    var allKeys = server.Keys(pattern: $"{API_STATUS_CODE_KEY_PREFIX}*").ToList();

                    foreach (var key in allKeys)
                    {
                        string keyStr = key.ToString();
                        if (!keyStr.Contains(':')) // Skip daily keys
                        {
                            if (int.TryParse(keyStr.Replace(API_STATUS_CODE_KEY_PREFIX, ""), out int statusCode))
                            {
                                RedisValue count = await db.StringGetAsync(key);
                                if (count.HasValue)
                                {
                                    result[statusCode] = (int)(long)count;
                                }
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status code distribution");
                return new Dictionary<int, int>();
            }
        }

        public async Task<Dictionary<string, int>> GetHourlyRequestsAsync(string date = null)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                var result = new Dictionary<string, int>();
                
                var dayKey = string.IsNullOrEmpty(date) 
                    ? $"{DateTime.UtcNow:yyyy-MM-dd}" 
                    : date;

                // Get hourly stats for the specified day
                var hourlyStats = await db.HashGetAllAsync($"{API_HOURLY_STATS_KEY_PREFIX}{dayKey}");
                
                foreach (var entry in hourlyStats)
                {
                    // Just get hour from the key (format: yyyy-MM-dd-HH)
                    string hour = entry.Name.ToString().Split('-').Last();
                    result[$"{hour}:00"] = (int)(long)entry.Value;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hourly requests");
                return new Dictionary<string, int>();
            }
        }
    }
}