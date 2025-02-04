namespace BusInfo.Models
{
    public class WeatherSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultLocation { get; set; } = "Leyland,UK";
        public int CacheExpirationMinutes { get; set; } = 30;
    }
}
