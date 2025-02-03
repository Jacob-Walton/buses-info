using System;
using System.Threading.Tasks;
using BusInfo.Models;
using StackExchange.Redis;

namespace BusInfo.Services
{
    public class RequestTrackingService(IConnectionMultiplexer redis) : IRequestTrackingService
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private const string API_REQUESTS_KEY = "metrics:api:requests";
        private const string API_RESPONSE_TIMES_KEY = "metrics:api:response_times";
        private const string API_REQUESTS_COUNT_KEY = "metrics:api:requests_count";

        public Task IncrementApiRequestCountAsync()
        {
            IDatabase db = _redis.GetDatabase();
            return db.StringIncrementAsync(API_REQUESTS_KEY);
        }

        public async Task RecordResponseTimeAsync(double milliseconds)
        {
            IDatabase db = _redis.GetDatabase();

            ITransaction transaction = db.CreateTransaction();

            await transaction.StringIncrementAsync(API_REQUESTS_COUNT_KEY);
            await transaction.StringIncrementAsync(API_RESPONSE_TIMES_KEY, (long)milliseconds);

            bool success = await transaction.ExecuteAsync();
            if (!success)
            {
                throw new RedisException("Failed to record response time");
            }
        }

        public async Task<RequestMetrics> GetMetricsAsync()
        {
            IDatabase db = _redis.GetDatabase();
            RedisValue totalRequests = await db.StringGetAsync(API_REQUESTS_KEY);
            RedisValue totalResponseTime = await db.StringGetAsync(API_RESPONSE_TIMES_KEY);
            RedisValue responseCount = await db.StringGetAsync(API_REQUESTS_COUNT_KEY);

            double avgResponseTime = 0.0;
            if (responseCount.HasValue && (long)responseCount > 0)
            {
                avgResponseTime = (double)totalResponseTime / (long)responseCount;
            }

            return new RequestMetrics
            {
                TotalRequests = (long)(totalRequests.HasValue ? totalRequests : 0),
                AverageResponseTime = Math.Round(avgResponseTime, 2),
                ErrorCount = 0
            };
        }
    }
}