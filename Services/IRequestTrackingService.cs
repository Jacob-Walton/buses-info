using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IRequestTrackingService
    {
        Task IncrementApiRequestCountAsync();
        Task<RequestMetrics> GetMetricsAsync();
        Task RecordResponseTimeAsync(double milliseconds);
    }
}