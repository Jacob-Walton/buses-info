using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusInfo.Extensions;
using BusInfo.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusInfo.Services.BackgroundServices
{
    public class BusMapGeneratorService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<BusMapGeneratorService> logger) : BackgroundService
    {
        private static readonly Action<ILogger, Exception> _logGenerateError =
            LoggerMessage.Define(LogLevel.Error,
                               new EventId(1, nameof(ExecuteAsync)),
                               "Error generating bus lane map");

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IMemoryCache _cache = cache;
        private readonly ILogger<BusMapGeneratorService> _logger = logger;
        private const string MAP_CACHE_KEY_PREFIX = "BusLaneMap_";
        private const int UPDATE_INTERVAL_MINUTES = 5;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await GenerateAndCacheMapAsync();
                }
                catch (Exception ex)
                {
                    _logGenerateError(_logger, ex);
                }

                await Task.Delay(TimeSpan.FromMinutes(UPDATE_INTERVAL_MINUTES), stoppingToken);
            }
        }

        private async Task GenerateAndCacheMapAsync()
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IBusInfoService busInfoService = scope.ServiceProvider.GetRequiredService<IBusInfoService>();
            IBusLaneService busLaneService = scope.ServiceProvider.GetRequiredService<IBusLaneService>();

            BusInfoResponse busInfo = await busInfoService.GetBusInfoAsync();
            Dictionary<string, string> bayServiceMap = busInfo.BusData.ToDictionaryWithFirstValue(
                x => x.Value.Bay ?? string.Empty,
                x => x.Key);

            string cacheKey = MAP_CACHE_KEY_PREFIX + string.Join("_", bayServiceMap.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            byte[] imageData = await busLaneService.GenerateBusLaneMapAsync(bayServiceMap);

            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(UPDATE_INTERVAL_MINUTES + 1));

            _cache.Set(cacheKey, imageData, cacheOptions);
            _cache.Set(MAP_CACHE_KEY_PREFIX + "latest", (cacheKey, imageData), cacheOptions);
        }
    }
}
