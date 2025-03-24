using System.Collections.Generic;

namespace BusInfo.Models
{
    public class ApiKeyMetrics
    {
        public long TotalRequests { get; set; }
        public int RequestsToday { get; set; }
        public Dictionary<int, int> StatusCodes { get; set; } = [];
        public double AverageResponseTime { get; set; }
        public List<TimeSeriesDataPoint> RequestsTimeSeries { get; set; } = [];
    }

    public class TimeSeriesDataPoint
    {
        public string TimeLabel { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}