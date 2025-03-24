namespace BusInfo.Models
{
    public class ApiRequestInfo
    {
        public string Endpoint { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string? UserId { get; set; }
        public string? ApiKey { get; set; }
        public double ResponseTimeMs { get; set; }
        public bool IsSuccessful => StatusCode is >= 200 and < 400;
    }
}