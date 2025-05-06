using System;
using System.Threading.Tasks;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static Microsoft.Extensions.Logging.LoggerMessage;

namespace BusInfo.Controllers.api
{
    /// <summary>
    /// Controller for handling authentication token operations like exchange, refresh and validation.
    /// </summary>
    /// <param name="userService">Service for user authentication operations.</param>
    /// <param name="logger">Logger for logging token operations.</param>
    [ApiController]
    [Route("api/token")]
    [Authorize] // Changed from [AllowAnonymous] to fix attribute conflict
    public class TokenController(
        IUserService userService,
        ILogger<TokenController> logger) : ControllerBase
    {
        private readonly IUserService _userService = userService ?? throw new ArgumentNullException(nameof(userService));
        private readonly ILogger<TokenController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        #region Logger Message Definitions

        private static readonly Action<ILogger, Exception?> _logGoogleTokenExchange =
            Define(LogLevel.Information, 3000, "Processing Google token exchange request");

        private static readonly Action<ILogger, string, Exception> _logGoogleTokenExchangeFailed =
            Define<string>(LogLevel.Warning, 3001, "Google token exchange failed: {Message}");

        private static readonly Action<ILogger, string, Exception> _logGoogleTokenExchangeError =
            Define<string>(LogLevel.Error, 3002, "Error exchanging Google token: {Message}");

        private static readonly Action<ILogger, Exception?> _logAppleTokenExchange =
            Define(LogLevel.Information, 3010, "Processing Apple token exchange request");

        private static readonly Action<ILogger, string, Exception> _logAppleTokenExchangeFailed =
            Define<string>(LogLevel.Warning, 3011, "Apple token exchange failed: {Message}");

        private static readonly Action<ILogger, string, Exception> _logAppleTokenExchangeError =
            Define<string>(LogLevel.Error, 3012, "Error exchanging Apple token: {Message}");

        private static readonly Action<ILogger, Exception?> _logTokenRefresh =
            Define(LogLevel.Information, 3020, "Processing token refresh request");

        private static readonly Action<ILogger, string, Exception> _logTokenRefreshFailed =
            Define<string>(LogLevel.Warning, 3021, "Token refresh failed: {Message}");

        private static readonly Action<ILogger, string, Exception> _logTokenRefreshError =
            Define<string>(LogLevel.Error, 3022, "Error refreshing token: {Message}");

        #endregion Logger Message Definitions

        /// <summary>
        /// Exchanges a Google ID token for a local application token.
        /// </summary>
        /// <param name="request">Google token exchange request containing the ID token.</param>
        /// <returns>
        /// 200 OK with login response including app tokens if successful.
        /// 400 Bad Request if request is invalid.
        /// 401 Unauthorized if Google token is invalid.
        /// 500 Internal Server Error if operation fails.
        /// </returns>
        [HttpPost("google")]
        [AllowAnonymous] // Add explicit AllowAnonymous to endpoints that should be accessible without auth
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExchangeGoogleTokenAsync([FromBody] GoogleTokenExchangeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logGoogleTokenExchange(_logger, null);
                LoginResponse response = await _userService.AuthenticateGoogleUserAsync(request.IdToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logGoogleTokenExchangeFailed(_logger, ex.Message, ex);
                return Unauthorized(new { message = "Invalid Google token" });
            }
            catch (InvalidOperationException ex)
            {
                _logGoogleTokenExchangeFailed(_logger, ex.Message, ex);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logGoogleTokenExchangeError(_logger, ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while processing your request" });
            }
        }

        /// <summary>
        /// Exchanges an Apple ID token for a local application token.
        /// </summary>
        /// <param name="request">Apple token exchange request containing the ID token.</param>
        /// <returns>
        /// 200 OK with login response including app tokens if successful.
        /// 400 Bad Request if request is invalid.
        /// 401 Unauthorized if Apple token is invalid.
        /// 500 Internal Server Error if operation fails.
        /// </returns>
        [HttpPost("apple")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ExchangeAppleTokenAsync([FromBody] AppleTokenExchangeRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logAppleTokenExchange(_logger, null);
                LoginResponse response = await _userService.AuthenticateAppleUserAsync(request.IdToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logAppleTokenExchangeFailed(_logger, ex.Message, ex);
                return Unauthorized(new { message = "Invalid Apple token" });
            }
            catch (InvalidOperationException ex)
            {
                _logAppleTokenExchangeFailed(_logger, ex.Message, ex);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logAppleTokenExchangeError(_logger, ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while processing your request" });
            }
        }

        /// <summary>
        /// Refreshes an expired token using a refresh token.
        /// </summary>
        /// <param name="request">Token refresh request containing the refresh token.</param>
        /// <returns>
        /// 200 OK with new token response if successful.
        /// 400 Bad Request if request is invalid.
        /// 401 Unauthorized if refresh token is invalid or expired.
        /// 500 Internal Server Error if operation fails.
        /// </returns>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] TokenRefreshRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _logTokenRefresh(_logger, null);
                LoginResponse response = await _userService.RefreshTokenAsync(request.RefreshToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logTokenRefreshFailed(_logger, ex.Message, ex);
                return Unauthorized(new { message = "Invalid or expired refresh token" });
            }
            catch (Exception ex)
            {
                _logTokenRefreshError(_logger, ex.Message, ex);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { message = "An error occurred while processing your request" });
            }
        }

        /// <summary>
        /// Validates if the current token is valid.
        /// </summary>
        /// <returns>
        /// 200 OK if token is valid.
        /// 401 Unauthorized if token is invalid or missing.
        /// </returns>
        [HttpPost("validate")]
        // No need for [Authorize] here since it's inherited from the controller
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult ValidateToken()
        {
            // If we get here, the token is valid because of the [Authorize] attribute
            return Ok(new { isValid = true });
        }
    }
}