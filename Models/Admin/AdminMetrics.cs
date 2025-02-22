namespace BusInfo.Models.Admin
{
    public class AdminMetrics
    {
        public int TotalUsers { get; set; }
        public int ActiveApiKeys { get; set; }
        public int PendingApiRequests { get; set; }
        public long TotalApiRequests { get; set; }
        public long ApiRequests24Hours { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
    }
}