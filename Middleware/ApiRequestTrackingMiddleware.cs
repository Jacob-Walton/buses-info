using System;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using BusInfo.Models;
using BusInfo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BusInfo.Middleware
{
    public class ApiRequestTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiRequestTrackingMiddleware> _logger;

        public ApiRequestTrackingMiddleware(RequestDelegate next, ILogger<ApiRequestTrackingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IRequestTrackingService requestTrackingService)
        {
            if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Skipping request tracking for non-API request: {Path}", context.Request.Path);
                await _next(context);
                return;
            }

            _logger.LogInformation("Tracking API request: {Path}", context.Request.Path);

            var stopwatch = Stopwatch.StartNew();
            var originalBodyStream = context.Response.Body;

            try
            {
                await _next(context);

                stopwatch.Stop();

                var requestInfo = new ApiRequestInfo
                {
                    Endpoint = NormalizeEndpoint(context.Request.Path.Value),
                    StatusCode = context.Response.StatusCode,
                    ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    UserId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                };

                if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey))
                {
                    requestInfo.ApiKey = apiKey.ToString();
                }

                await requestTrackingService.TrackApiRequestAsync(requestInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request.");

                if (!context.Response.HasStarted)
                {
                    await _next(context);
                }
            }
        }

        private string NormalizeEndpoint(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "unknown";

            // Convert to lowercase
            path = path.ToLowerInvariant();

            // Replace any numeric IDs with {id} for better grouping
            // Example: /api/users/123 -> /api/users/{id}
            var segments = path.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0 && segments[i].Length > 0 && int.TryParse(segments[i], out _))
                {
                    segments[i] = "{id}";
                }
            }

            return string.Join('/', segments);
        }
    }
}