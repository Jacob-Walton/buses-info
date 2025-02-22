using System;
using System.Collections.Generic;

namespace BusInfo.Models.Admin
{
    public class ApiUsageStats
    {
        public int ActiveKeys { get; set; }
        public int TotalRequests { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public Dictionary<string, int> EndpointUsage { get; set; } = [];
        public List<ApiUsageMetric> UsageHistory { get; set; } = [];
        public int KeyChange { get; set; }
    }

    public class ApiUsageMetric
    {
        public DateTime Timestamp { get; set; }
        public int RequestCount { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public Dictionary<int, int> ResponseCodes { get; set; } = [];
    }
}