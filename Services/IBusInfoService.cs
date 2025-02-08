using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IBusInfoService
    {
        Task<BusInfoLegacyResponse> GetLegacyBusInfoAsync();
        Task<BusInfoResponse> GetBusInfoAsync();
        Task<BusPredictionResponse> GetBusPredictionsAsync();
        Task<PredictionInfo?> GetBusPredictionAsync(string busNumber);
    }
}