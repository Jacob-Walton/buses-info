using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AspNetCoreRateLimit;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;

namespace BusInfo.Authentication.RateLimiting
{
    public class RedisClientPolicyStore(
        IConnectionMultiplexer redis,
        IOptions<ClientRateLimitOptions> options) : IClientPolicyStore
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly string _keyPrefix = "client_rate_limit";
        private readonly ClientRateLimitOptions _options = options.Value;

        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                return db.KeyExistsAsync($"{_keyPrefix}:{id}");
            }
            catch (RedisException)
            {
                return Task.FromResult(false);
            }
        }

        public async Task<ClientRateLimitPolicy?> GetAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {

                IDatabase db = _redis.GetDatabase();
                RedisValue policy = await db.StringGetAsync($"{_keyPrefix}:{id}");
                return policy.HasValue ? JsonSerializer.Deserialize<ClientRateLimitPolicy>(policy!) : null;
            }
            catch (RedisException)
            {
                return null;
            }
        }

        public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                return db.KeyDeleteAsync($"{_keyPrefix}:{id}");
            }
            catch (RedisException)
            {
                return Task.CompletedTask;
            }
        }

        public async Task SetAsync(string id, ClientRateLimitPolicy entry, TimeSpan? expirationTime = null, CancellationToken cancellationToken = default)
        {
            if (entry == null) return;

            try
            {
                IDatabase db = _redis.GetDatabase();
                await db.StringSetAsync(
                    $"{_keyPrefix}:{id}",
                    JsonSerializer.Serialize(entry),
                    expirationTime
                );
            }
            catch (RedisException)
            {
                // Do nothing
            }
        }

        public async Task SeedAsync()
        {
            foreach (RateLimitRule rule in _options.GeneralRules)
            {
                ClientRateLimitPolicy policy = new() { Rules = [rule] };
                await SetAsync($"client_{rule.Endpoint}", policy);
            }
        }
    }
}