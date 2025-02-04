using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AspNetCoreRateLimit;
using StackExchange.Redis;

namespace BusInfo.Authentication.RateLimiting
{
    public class RedisProcessingStrategy(IConnectionMultiplexer redis) : IProcessingStrategy
    {
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);

        public async Task<RateLimitCounter> ProcessRequestAsync(ClientRequestIdentity requestIdentity, RateLimitRule rule, ICounterKeyBuilder counterKeyBuilder, RateLimitOptions rateLimitOptions, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rule, nameof(rule));
            ArgumentNullException.ThrowIfNull(counterKeyBuilder, nameof(counterKeyBuilder));

            IDatabase db = _redis.GetDatabase();
            RateLimitCounter counter = new()
            {
                Timestamp = DateTime.UtcNow,
                Count = 1
            };

            string key = counterKeyBuilder.Build(requestIdentity, rule);
            string lockKey = $"lock:{key}";

            bool locked = await db.LockTakeAsync(lockKey, "lock", _lockTimeout);
            if (!locked)
            {
                return new RateLimitCounter
                {
                    Timestamp = DateTime.UtcNow,
                    Count = rule.Limit + 1
                };
            }

            try
            {
                RedisValue entry = await db.StringGetAsync(key);
                if (entry.HasValue)
                {
                    counter = JsonSerializer.Deserialize<RateLimitCounter>(entry!);

                    if (rule.PeriodTimespan != null && (DateTime.UtcNow - counter.Timestamp).TotalSeconds > rule.PeriodTimespan.Value.TotalSeconds)
                    {
                        counter.Timestamp = DateTime.UtcNow;
                        counter.Count = 1;
                    }
                    else
                    {
                        counter.Count++;
                    }
                }

                await db.StringSetAsync(
                    key,
                    JsonSerializer.Serialize(counter),
                    rule.PeriodTimespan
                );

                return counter;
            }
            finally
            {
                await db.LockReleaseAsync(lockKey, "lock");
            }
        }
    }
}