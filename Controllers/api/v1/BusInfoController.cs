using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using BusInfo.Services;
using System;
using Microsoft.AspNetCore.Http;
using BusInfo.Models;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

namespace BusInfo.Controllers.Api.V1
{
    /// <summary>
    /// Bus Information API Controller
    /// </summary>
    [ApiController]
    [Route("api/v1/businfo")]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = "Cookies,ApiKey")]
    public class BusInfoController(ILogger<BusInfoController> logger, IBusInfoService busInfoService) : ControllerBase
    {
        private readonly IBusInfoService busInfoService = busInfoService;
        private readonly ILogger<BusInfoController> logger = logger;
        private static readonly Action<ILogger, DateTime, Exception?> _logRequestReceived =
            LoggerMessage.Define<DateTime>(LogLevel.Information,
                new EventId(1, nameof(GetBusInfoAsync)),
                "GetBusInfoAsync request received at {Time}");
        private static readonly Action<ILogger, DateTime, Exception?> _logError =
            LoggerMessage.Define<DateTime>(LogLevel.Error,
                new EventId(3, nameof(GetBusInfoAsync)),
                "An error occurred while processing GetBusInfoAsync at {Time}");

        /// <summary>
        /// Retrieves bus information asynchronously
        /// </summary>
        /// <returns>ActionResult containing BusInfoResponse</returns>
        /// <response code="200">Returns the bus information</response>
        /// <response code="401">If the user is unauthorized</response>
        /// <response code="500">If there was an internal server error</response>
        [HttpGet]
        [ProducesResponseType(typeof(BusInfoLegacyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<BusInfoLegacyResponse>> GetBusInfoAsync()
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

            try
            {
                _logRequestReceived(logger, DateTime.UtcNow, null);

                BusInfoLegacyResponse busInfo = await busInfoService.GetLegacyBusInfoAsync();

                // Ensure response is properly formatted
                return Ok(busInfo);
            }
            catch (OperationCanceledException)
            {
                _logError(logger, DateTime.UtcNow, null);
                return StatusCode(StatusCodes.Status504GatewayTimeout, "Request timed out");
            }
            catch (HttpRequestException ex)
            {
                _logError(logger, DateTime.UtcNow, ex);
                return StatusCode(StatusCodes.Status502BadGateway, "A network error occurred while fetching bus information.");
            }
            catch (Exception ex)
            {
                _logError(logger, DateTime.UtcNow, ex);
                throw;
            }
        }
    }
}