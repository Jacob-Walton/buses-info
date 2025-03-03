using System.Collections.Generic;
using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IRequestTrackingService
    {
        Task IncrementApiRequestCountAsync();
        Task<RequestMetrics> GetMetricsAsync();
        Task RecordResponseTimeAsync(double milliseconds);
        Task TrackApiRequestAsync(ApiRequestInfo requestInfo);
        Task<ApiKeyMetrics> GetApiKeyMetricsAsync(string apiKey);
        Task<List<EndpointMetrics>> GetTopEndpointsAsync(int count = 5);
        Task<Dictionary<int, int>> GetStatusCodeDistributionAsync();
        Task<Dictionary<string, int>> GetHourlyRequestsAsync(string? date = null);
    }
}