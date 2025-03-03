using System;
using System.Collections.Generic;
using System.Linq;

namespace BusInfo.Models.Admin
{
    public class SystemHealth
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double DiskUsage { get; set; }
        public int ActiveConnections { get; set; }
        public Dictionary<string, ServiceStatus> Services { get; set; } = [];
        public List<PerformanceMetric> Performance { get; set; } = [];

        public bool AllHealthy => Services.All(s => s.Value == ServiceStatus.Healthy);
    }

    public enum ServiceStatus
    {
        /// <summary>
        /// Index 0
        /// </summary>
        Healthy = 0,
        /// <summary>
        /// Index 1
        /// </summary>
        Degraded = 1,
        /// <summary>
        /// Index 2
        /// </summary>
        Unhealthy = 2,
        /// <summary>
        /// Index 3
        /// </summary>
        Unknown = 3
    }

    public class PerformanceMetric
    {
        public DateTime Timestamp { get; set; }
        public string MetricName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string? Unit { get; set; }
        public MetricType Type { get; set; }
        public Dictionary<string, string> Tags { get; set; } = [];
    }

    public enum MetricType
    {
        /// <summary>
        /// Index 0
        /// </summary>
        Counter = 0,
        /// <summary>
        /// Index 1
        /// </summary>
        Gauge = 1,
        /// <summary>
        /// Index 2
        /// </summary>
        Histogram = 2,
        /// <summary>
        /// Index 3
        /// </summary>
        Timer = 3
    }
}