using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BusInfo.Exceptions;
using BusInfo.Extensions;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BusInfo.Controllers.Api.V2
{
    [ApiController]
    [Route("api/v2/businfo")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = "Cookies,ApiKey")]
    public class BusInfoController(
        ILogger<BusInfoController> logger,
        IBusInfoService busInfoService,
        IMemoryCache cache,
        IConfigCatService configCatService) : ControllerBase
    {
        private readonly IBusInfoService _busInfoService = busInfoService;
        private readonly ILogger<BusInfoController> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly IConfigCatService _configCatService = configCatService;
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
        public async Task<IActionResult> GetBusLaneMapAsync()
        {
            // Ensure the Accept header includes image/png or */*
            if (!Request.Headers.Accept.ToString().Contains("image/png") &&
                !Request.Headers.Accept.ToString().Contains("*/*") &&
                !string.IsNullOrEmpty(Request.Headers.Accept))
            {
                return StatusCode(StatusCodes.Status406NotAcceptable,
                    "This endpoint only produces image/png content");
            }

            try
            {
                BusInfoResponse busInfo = await _busInfoService.GetBusInfoAsync();
                Dictionary<string, string> bayServiceMap = busInfo.BusData.ToDictionaryWithFirstValue(
                    x => x.Value.Bay ?? string.Empty,
                    x => x.Key);

                string requestedCacheKey = MapCacheKeyPrefix + string.Join("_", bayServiceMap.Select(kvp => $"{kvp.Key}:{kvp.Value}"));

                // Try to get the exact match first
                if (_cache.TryGetValue(requestedCacheKey, out byte[]? imageData) && imageData != null)
                {
                    return File(imageData, "image/png");
                }

                // Fall back to latest generated map
                if (_cache.TryGetValue(MapCacheKeyPrefix + "latest", out (string key, byte[] data) latest) && latest.data != null)
                {
                    return File(latest.data, "image/png");
                }

                // No map available in cache, trigger generation
                using IServiceScope scope = HttpContext.RequestServices.CreateScope();
                IBusLaneService busLaneService = scope.ServiceProvider.GetRequiredService<IBusLaneService>();

                byte[] generatedImageData = await busLaneService.GenerateBusLaneMapAsync(bayServiceMap);

                return File(generatedImageData, "image/png");
            }
            catch (ApiException ex)
            {
                _logError(_logger, DateTime.UtcNow, "API error retrieving map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the bus lane map.");
            }
            catch (InvalidOperationException ex)
            {
                _logError(_logger, DateTime.UtcNow, "Invalid operation while retrieving map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the map data.");
            }
            catch (Exception ex)
            {
                _logError(_logger, DateTime.UtcNow, "Unexpected error retrieving map", ex);
                throw;
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

                BusPredictionResponse allPredictions = await _busInfoService.GetBusPredictionsAsync();
                Dictionary<string, PredictionInfo> predictions = allPredictions.Predictions
                    .Where(kvp => requestedBuses.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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