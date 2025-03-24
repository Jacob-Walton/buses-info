using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using StackExchange.Redis;

namespace BusInfo.Authentication.RateLimiting
{
    public class RedisRateLimitCounterStore(IConnectionMultiplexer redis) : IRateLimitCounterStore
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly string _keyPrefix = "counter";

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            IDatabase db = _redis.GetDatabase();
            return db.KeyExistsAsync($"{_keyPrefix}:{id}");
        }

        public async Task<RateLimitCounter?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            IDatabase db = _redis.GetDatabase();
            RedisValue counter = await db.StringGetAsync($"{_keyPrefix}:{id}");
            return counter.HasValue ? JsonSerializer.Deserialize<RateLimitCounter>(counter!) : null;
        }

        public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
        {
            IDatabase db = _redis.GetDatabase();
            return db.KeyDeleteAsync($"{_keyPrefix}:{id}");
        }

        public async Task SetAsync(string id, RateLimitCounter? entry, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        {
            if (entry == null) return;

            IDatabase db = _redis.GetDatabase();
            await db.StringSetAsync(
                $"{_keyPrefix}:{id}",
                JsonSerializer.Serialize(entry),
                expirationTime);
        }
    }
}