using System;
using System.Collections.Generic;

namespace BusInfo.Models.Admin
{
    public class ApiUsageStats
    {
        public int ActiveKeys { get; set; }
        public int KeysExpiringSoon { get; set; }
        public long TotalRequests { get; set; }
        public long Requests24Hours { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public string TopEndpoint { get; set; } = string.Empty;
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