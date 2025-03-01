namespace BusInfo.Models
{
    public class EndpointMetrics
    {
        public string Endpoint { get; set; } = string.Empty;
        public long RequestCount { get; set; }
    }
}