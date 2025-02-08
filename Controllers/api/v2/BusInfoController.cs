using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BusInfo.Exceptions;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BusInfo.Controllers.Api.V2
{
    [ApiController]
    [Route("api/v2/businfo")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = "Cookies,ApiKey")]
    public class BusInfoController(
        IBusInfoService busInfoService,
        IBusLaneService busLaneService,
        ILogger<BusInfoController> logger,
        IMemoryCache cache,
        IConfigCatService configCatService) : ControllerBase
    {
        private readonly IBusInfoService _busInfoService = busInfoService;
        private readonly IBusLaneService _busLaneService = busLaneService;
        private readonly ILogger<BusInfoController> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly IConfigCatService _configCatService = configCatService;
        private const int MapCacheDurationMinutes = 5;
        private const string MapCacheKeyPrefix = "BusLaneMap_";

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logRequestReceived =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Information,
                new EventId(1, nameof(GetBusInfoAsync)),
                "Bus info request received at {Timestamp}. ID: {RequestId}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logRequestCompleted =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Information,
                new EventId(2, nameof(GetBusInfoAsync)),
                "Bus info request completed at {Timestamp}. ID: {RequestId}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logError =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Error,
                new EventId(3, "BusInfoError"),
                "Error processing request at {Timestamp}. Details: {Details}");

        [HttpGet]
        [ProducesResponseType(typeof(BusInfoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBusInfoAsync()
        {
            try
            {
                _logRequestReceived(_logger, DateTime.UtcNow, null, null);

                bool predictionsEnabled = await _configCatService.GetFlagValueAsync(User, "busBayPredictions", false);
                BusInfoResponse busInfo = await _busInfoService.GetBusInfoAsync();

                _logRequestCompleted(_logger, DateTime.UtcNow, null, null);

                return Ok(busInfo);
            }
            catch (ApiException ex)
            {
                _logError(_logger, DateTime.UtcNow, "API Exception", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching bus information.");
            }
            catch (Exception ex)
            {
                _logError(_logger, DateTime.UtcNow, "Unexpected error", ex);
                throw;
            }
        }

        [HttpGet("map")]
        [Produces("image/png")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBusLaneMapAsync()
        {
            try
            {
                BusInfoResponse busInfo = await _busInfoService.GetBusInfoAsync();

                Dictionary<string, string> bayServiceMap = busInfo.BusData
                    .Where(kvp => !string.IsNullOrEmpty(kvp.Value.Bay))
                    .ToDictionary(
                        kvp => kvp.Value.Bay!,
                        kvp => kvp.Key
                    );

                string cacheKey = MapCacheKeyPrefix + string.Join("_", bayServiceMap.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

                if (!_cache.TryGetValue(cacheKey, out byte[]? imageData))
                {
                    imageData = await _busLaneService.GenerateBusLaneMapAsync(bayServiceMap);
                    MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(MapCacheDurationMinutes));
                    _cache.Set(cacheKey, imageData, cacheOptions);
                }

                return imageData == null
                    ? StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate map image.")
                    : File(imageData, "image/png");
            }
            catch (ApiException ex)
            {
                _logError(_logger, DateTime.UtcNow, "API Exception while generating map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the bus lane map.");
            }
            catch (InvalidOperationException ex)
            {
                _logError(_logger, DateTime.UtcNow, "Invalid operation while generating map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the bus lane map.");
            }
        }

        [HttpPost("map")]
        [Produces("image/png")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateBusLaneMapAsync([FromBody] Dictionary<string, string> bayServiceMap)
        {
            try
            {
                string cacheKey = MapCacheKeyPrefix + string.Join("_", bayServiceMap.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

                if (!_cache.TryGetValue(cacheKey, out byte[]? imageData))
                {
                    imageData = await _busLaneService.GenerateBusLaneMapAsync(bayServiceMap);
                    MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(MapCacheDurationMinutes));
                    _cache.Set(cacheKey, imageData, cacheOptions);
                }

                return imageData == null
                    ? StatusCode(StatusCodes.Status500InternalServerError, "Failed to generate map image.")
                    : File(imageData, "image/png");
            }
            catch (ApiException ex)
            {
                _logError(_logger, DateTime.UtcNow, "API Exception while generating map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the bus lane map.");
            }
            catch (InvalidOperationException ex)
            {
                _logError(_logger, DateTime.UtcNow, "Invalid operation while generating map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the bus lane map.");
            }
        }

        [HttpGet("predictions")]
        [ProducesResponseType(typeof(BusPredictionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBusPredictionsAsync()
        {
            try
            {
                bool predictionsEnabled = await _configCatService.GetFlagValueAsync(User, "busBayPredictions", false);
                if (!predictionsEnabled)
                {
                    return NotFound("Predictions are not currently enabled");
                }

                BusPredictionResponse predictions = await _busInfoService.GetBusPredictionsAsync();
                return Ok(predictions);
            }
            catch (ApiException ex)
            {
                _logError(_logger, DateTime.UtcNow, "Error getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching predictions.");
            }
            catch (InvalidOperationException ex)
            {
                _logError(_logger, DateTime.UtcNow, "Error getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing predictions.");
            }
        }

        [HttpGet("predictions/{busNumbers}")]
        [ProducesResponseType(typeof(Dictionary<string, PredictionInfo>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetBatchBusPredictionsAsync(string busNumbers)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(busNumbers))
                {
                    return BadRequest("No bus numbers provided");
                }

                string[] requestedBuses = busNumbers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (requestedBuses.Length == 0)
                {
                    return BadRequest("No valid bus numbers provided");
                }

                if (requestedBuses.Length > 50)
                {
                    return BadRequest("Too many bus numbers requested (maximum 50)");
                }

                bool predictionsEnabled = await _configCatService.GetFlagValueAsync(User, "busBayPredictions", false);
                if (!predictionsEnabled)
                {
                    return NotFound("Predictions are not currently enabled");
                }

                Dictionary<string, PredictionInfo> predictions = [];
                foreach (string? busNumber in requestedBuses.Distinct())
                {
                    PredictionInfo? prediction = await _busInfoService.GetBusPredictionAsync(busNumber);
                    if (prediction != null)
                    {
                        predictions[busNumber] = prediction;
                    }
                }

                return predictions.Count > 0 ? Ok(predictions) : NotFound("No valid bus numbers found");
            }
            catch (ApiException ex)
            {
                _logError(_logger, DateTime.UtcNow, "API error getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching predictions.");
            }
            catch (InvalidOperationException ex)
            {
                _logError(_logger, DateTime.UtcNow, "Invalid operation while getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the request.");
            }
        }
    }
}