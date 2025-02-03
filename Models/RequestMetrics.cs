namespace BusInfo.Models
{
    public class RequestMetrics
    {
        public long TotalRequests { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
    }

}