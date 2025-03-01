using System;
using System.Collections.Generic;

namespace BusInfo.Models.Admin
{
    public class ApiUsageStats
    {
        public int ActiveKeys { get; set; }
        public int KeysExpiringSoon { get; set; }
        public int TotalRequests { get; set; }
        public int Requests24Hours { get; set; }
        public double AverageResponseTime { get; set; }
        public int ErrorCount { get; set; }
        public string TopEndpoint { get; set; } = string.Empty;

        public double ErrorRate => TotalRequests > 0
            ? (double)ErrorCount / TotalRequests * 100
            : 0;
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