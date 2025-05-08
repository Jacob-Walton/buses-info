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
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace BusInfo.Controllers.Api.V2
{
    /// <summary>
    /// API controller for bus information and related services.
    /// </summary>
    /// <param name="logger">The logger for logging information.</param>
    /// <param name="busInfoService">The service for bus information retrieval.</param>
    /// <param name="cache">The memory cache for caching data.</param>
    /// <param name="configCatService">The service for feature flag management.</param>
    /// <param name="configuration">The configuration for accessing JWT settings.</param>
    [ApiController]
    [Route("api/v2/businfo")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = "Cookies,ApiKey,Bearer")]
    public class BusInfoController(
        ILogger<BusInfoController> logger,
        IBusInfoService busInfoService,
        IMemoryCache cache,
        IConfigCatService configCatService,
        IConfiguration configuration) : ControllerBase
    {
        private readonly IBusInfoService _busInfoService = busInfoService;
        private readonly ILogger<BusInfoController> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly IConfigCatService _configCatService = configCatService;
        private readonly IConfiguration _configuration = configuration;
        private const string MapCacheKeyPrefix = "BusLaneMap_";

        #region Logger Message Definitions

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logRequestReceived =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Information,
                new EventId(1000, "BusInfoRequestReceived"),
                "Bus info request received at {Timestamp}. ID: {RequestId}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logRequestCompleted =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Information,
                new EventId(1001, "BusInfoRequestCompleted"),
                "Bus info request completed at {Timestamp}. ID: {RequestId}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logBusInfoError =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Error,
                new EventId(2000, "BusInfoError"),
                "Error processing bus info request at {Timestamp}. Details: {Details}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logRankingsError =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Error,
                new EventId(2001, "BusRankingsError"),
                "Error processing bus rankings request at {Timestamp}. Details: {Details}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logMapError =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Error,
                new EventId(2002, "BusMapError"),
                "Error processing bus lane map request at {Timestamp}. Details: {Details}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logPredictionsError =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Error,
                new EventId(2003, "BusPredictionsError"),
                "Error processing bus predictions request at {Timestamp}. Details: {Details}");

        private static readonly Action<ILogger, DateTime, string?, Exception?> _logBatchPredictionsError =
            LoggerMessage.Define<DateTime, string?>(
                LogLevel.Error,
                new EventId(2004, "BatchPredictionsError"),
                "Error processing batch bus predictions request at {Timestamp}. Details: {Details}");

        #endregion Logger Message Definitions

        /// <summary>
        /// Gets current bus information.
        /// </summary>
        /// <returns>Current bus information</returns>
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
                _logBusInfoError(_logger, DateTime.UtcNow, "API Exception", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching bus information.");
            }
            catch (Exception ex)
            {
                _logBusInfoError(_logger, DateTime.UtcNow, "Unexpected error", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets bus rankings.
        /// </summary>
        /// <returns>Current bus rankings</returns>
        [HttpGet("rankings")]
        [ProducesResponseType(typeof(BusRankingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetBusRankingsAsync()
        {
            try
            {
                BusRankingResponse rankings = await _busInfoService.GetBusRankingsAsync();
                return Ok(rankings);
            }
            catch (ApiException ex)
            {
                _logRankingsError(_logger, DateTime.UtcNow, "API error retrieving rankings", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching bus rankings.");
            }
            catch (InvalidOperationException ex)
            {
                _logRankingsError(_logger, DateTime.UtcNow, "Invalid operation while retrieving rankings", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the rankings data.");
            }
        }

        /// <summary>
        /// Gets a visual map of bus lane positions.
        /// </summary>
        /// <param name="token">Optional JWT token for authentication.</param>
        /// <returns>PNG image of bus lane map</returns>
        [HttpGet("map")]
        [Produces("image/png")]
        [AllowAnonymous] // Allow anonymous access to handle token validation manually
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status406NotAcceptable)]
        public async Task<IActionResult> GetBusLaneMapAsync(string? token = null)
        {
            // Manual token validation if provided in query parameter
            if (!string.IsNullOrEmpty(token) && !User.Identity?.IsAuthenticated == true)
            {
                bool isTokenValid = await ValidateTokenAsync(token);
                if (!isTokenValid)
                {
                    return Unauthorized(new { message = "Invalid token" });
                }
                // Token is valid, continue processing
            }
            // If no token is provided and user is not authenticated through other means
            else if (!User.Identity?.IsAuthenticated == true)
            {
                return Unauthorized(new { message = "Authentication required" });
            }

            // Ensure the Accept header includes image/png or */*
            if (!Request.Headers.Accept.ToString().Contains("image/png", StringComparison.InvariantCulture) &&
                !Request.Headers.Accept.ToString().Contains("*/*", StringComparison.InvariantCulture) &&
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
                _logMapError(_logger, DateTime.UtcNow, "API error retrieving map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the bus lane map.");
            }
            catch (InvalidOperationException ex)
            {
                _logMapError(_logger, DateTime.UtcNow, "Invalid operation while retrieving map", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the map data.");
            }
            catch (Exception ex)
            {
                _logMapError(_logger, DateTime.UtcNow, "Unexpected error retrieving map", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets bus arrival predictions.
        /// </summary>
        /// <returns>Bus arrival predictions</returns>
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
                _logPredictionsError(_logger, DateTime.UtcNow, "Error getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching predictions.");
            }
            catch (InvalidOperationException ex)
            {
                _logPredictionsError(_logger, DateTime.UtcNow, "Error getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing predictions.");
            }
        }

        /// <summary>
        /// Gets predictions for specific bus numbers.
        /// </summary>
        /// <param name="busNumbers">Semicolon-separated list of bus numbers</param>
        /// <returns>Predictions for requested buses</returns>
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
                _logBatchPredictionsError(_logger, DateTime.UtcNow, "API error getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching predictions.");
            }
            catch (InvalidOperationException ex)
            {
                _logBatchPredictionsError(_logger, DateTime.UtcNow, "Invalid operation while getting predictions", ex);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing the request.");
            }
        }

        /// <summary>
        /// Validates a JWT token.
        /// </summary>
        /// <param name="token">The JWT token to validate</param>
        /// <returns>True if token is valid, false otherwise</returns>
        private async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                string issuer = _configuration["Jwt:Issuer"] ?? "https://rb.dev.konpeki.co.uk";
                string audience = _configuration["Jwt:Audience"] ?? "https://rb.dev.konpeki.co.uk";
                string key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");

                JwtSecurityTokenHandler tokenHandler = new();
                TokenValidationParameters validationParameters = new()
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };

                // Validate token and get the principal
                ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                // Additional checks if needed based on claims, etc.
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return false;
            }
        }
    }
}