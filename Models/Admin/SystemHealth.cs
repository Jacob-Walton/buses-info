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
        Healthy, // Index 0
        Degraded, // Index 1
        Unhealthy, // Index 2
        Unknown // Index 3
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
        Counter, // Index 0
        Gauge, // Index 1
        Histogram, // Index 2
        Timer // Index 3
    }
}