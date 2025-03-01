using System.Collections.Generic;

namespace BusInfo.Models.Admin
{
    public class ApiDashboardData
    {
        public int ActiveKeys { get; set; }
        public int KeysExpiringSoon { get; set; }
        public long TotalRequests { get; set; }
        public long Requests24Hours { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public Dictionary<int, int> StatusCodeDistribution { get; set; } = new();
        public List<EndpointMetrics> TopEndpoints { get; set; } = new();
        public Dictionary<string, int> HourlyRequests { get; set; } = new();
    }
}