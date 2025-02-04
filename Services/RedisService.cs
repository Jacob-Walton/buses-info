using System;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace BusInfo.Services
{
    public class RedisService(IConnectionMultiplexer redis) : IRedisService
    {
        private readonly IConnectionMultiplexer _redis = redis;

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            IDatabase db = _redis.GetDatabase();
            string serializedValue = JsonSerializer.Serialize(value);
            return db.StringSetAsync(key, serializedValue, expiry);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            IDatabase db = _redis.GetDatabase();
            RedisValue value = await db.StringGetAsync(key).ConfigureAwait(false);

            if (value.IsNullOrEmpty)
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(value!);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        public Task RemoveAsync(string key)
        {
            IDatabase db = _redis.GetDatabase();
            return db.KeyDeleteAsync(key);
        }

        public Task<bool> KeyExistsAsync(string key)
        {
            IDatabase db = _redis.GetDatabase();
            return db.KeyExistsAsync(key);
        }

        public Task<TimeSpan?> GetTtlAsync(string key)
        {
            IDatabase db = _redis.GetDatabase();
            return db.KeyTimeToLiveAsync(key);
        }
    }
}