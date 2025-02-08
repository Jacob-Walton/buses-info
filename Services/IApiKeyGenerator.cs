using System.Threading.Tasks;

namespace BusInfo.Services
{
    public interface IApiKeyGenerator
    {
        string GenerateApiKey(string userId);
        Task<string> GenerateApiKeyAsync(string userId);
        bool ValidateApiKey(string apiKey);
    }
}