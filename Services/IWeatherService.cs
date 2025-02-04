using System.Threading.Tasks;

namespace BusInfo.Services
{
    public record WeatherInfo(double Temperature, string Weather);

    public interface IWeatherService
    {
        Task<WeatherInfo> GetWeatherAsync(string location);
    }
}