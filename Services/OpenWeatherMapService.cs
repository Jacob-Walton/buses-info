using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BusInfo.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace BusInfo.Services
{
    public class OpenWeatherMapService(
        HttpClient httpClient,
        IDistributedCache cache,
        IOptions<WeatherSettings> settings) : IWeatherService
    {
        private readonly HttpClient _httpClient = httpClient;
        private readonly IDistributedCache _cache = cache;
        private readonly WeatherSettings _settings = settings.Value;
        private const string API_BASE_URL = "https://api.openweathermap.org/data/2.5/weather";

        public async Task<WeatherInfo> GetWeatherAsync(string location)
        {
            string cacheKey = $"weather_{location}";
            string? cachedData = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                return JsonSerializer.Deserialize<WeatherInfo>(cachedData)
                    ?? await FetchWeatherDataAsync(location);
            }

            WeatherInfo weatherInfo = await FetchWeatherDataAsync(location);

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(weatherInfo),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_settings.CacheExpirationMinutes)
                });

            return weatherInfo;
        }

        private async Task<WeatherInfo> FetchWeatherDataAsync(string location)
        {
            string url = $"{API_BASE_URL}?q={Uri.EscapeDataString(location)}&appid={_settings.ApiKey}&units=metric";

            using HttpResponseMessage response = await _httpClient.GetAsync(new Uri(url));
            response.EnsureSuccessStatusCode();

            string jsonResponse = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(jsonResponse);
            JsonElement root = doc.RootElement;

            double temperature = root.GetProperty("main").GetProperty("temp").GetDouble();
            string weather = root.GetProperty("weather")[0].GetProperty("main").GetString() ?? "Unknown";

            return new WeatherInfo(temperature, weather);
        }
    }
}
