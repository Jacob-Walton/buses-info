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

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                return await db.KeyExistsAsync($"{_keyPrefix}:{id}");
            }
            catch (RedisException)
            {
                return await Task.FromResult(false);
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

        public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                await db.KeyDeleteAsync($"{_keyPrefix}:{id}");
                return;
            }
            catch (RedisException)
            {
                await Task.CompletedTask;
                return;
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