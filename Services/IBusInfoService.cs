using System.Threading.Tasks;
using BusInfo.Models;

namespace BusInfo.Services
{
    public interface IBusInfoService
    {
        Task<BusInfoResponse> GetBusInfoAsync();
        Task<BusInfoLegacyResponse> GetLegacyBusInfoAsync();
        Task<BusPredictionResponse> GetBusPredictionsAsync();
        Task<BusRankingResponse> GetBusRankingsAsync();
    }
}