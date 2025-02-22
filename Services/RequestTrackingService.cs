using System;
using System.Threading.Tasks;
using BusInfo.Models;
using StackExchange.Redis;

namespace BusInfo.Services
{
    public class RequestTrackingService(IConnectionMultiplexer redis) : IRequestTrackingService
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private const string API_REQUESTS_KEY = "metrics:api:requests:total";
        private const string API_REQUESTS_24H_KEY = "metrics:api:requests:24h";
        private const string API_RESPONSE_TIMES_KEY = "metrics:api:response_times";
        private const string API_REQUESTS_COUNT_KEY = "metrics:api:requests_count";

        public async Task IncrementApiRequestCountAsync()
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

        public async Task RecordResponseTimeAsync(double milliseconds)
        {
            IDatabase db = _redis.GetDatabase();

            ITransaction transaction = db.CreateTransaction();

            _ = transaction.StringIncrementAsync(API_REQUESTS_COUNT_KEY);
            _ = transaction.StringIncrementAsync(API_RESPONSE_TIMES_KEY, (long)milliseconds);

            bool success = await transaction.ExecuteAsync();
            if (!success)
            {
                throw new RedisException("Failed to record response time");
            }
        }

        public async Task<RequestMetrics> GetMetricsAsync()
        {
            IDatabase db = _redis.GetDatabase();

            // Get total counts
            RedisValue totalRequests = await db.StringGetAsync(API_REQUESTS_KEY);
            RedisValue totalResponseTime = await db.StringGetAsync(API_RESPONSE_TIMES_KEY);
            RedisValue responseCount = await db.StringGetAsync(API_REQUESTS_COUNT_KEY);

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
                ErrorCount = 0
            };
        }
    }
}