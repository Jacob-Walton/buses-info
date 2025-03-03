// Services/HealthCheckService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusInfo.Models.Admin;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using BusInfo.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using BusInfo.Models;
using System.Globalization;
using System.Data.Common;

namespace BusInfo.Services
{
    public class HealthCheckService(
        ILogger<HealthCheckService> logger,
        IMemoryCache cache,
        IConnectionMultiplexer redis,
        ApplicationDbContext dbContext,
        IRequestTrackingService requestTrackingService) : IHealthCheckService
    {
        private static readonly Action<ILogger, Exception> _logErrorGettingCpuUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1000, nameof(GetCpuUsageAsync)), "Error getting CPU usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingWindowsCpuUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1001, nameof(GetWindowsCpuUsage)), "Error getting Windows CPU usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingLinuxCpuUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1002, nameof(GetLinuxCpuUsageAsync)), "Error getting Linux CPU usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingMacCpuUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1003, nameof(GetMacCpuUsageAsync)), "Error getting Mac CPU usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingMemoryUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1004, nameof(GetMemoryUsage)), "Error getting memory usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingWindowsMemoryUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1005, nameof(GetWindowsMemoryUsage)), "Error getting Windows memory usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingLinuxMemoryUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1006, nameof(GetLinuxMemoryUsage)), "Error getting Linux memory usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingMacMemoryUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1007, nameof(GetMacMemoryUsage)), "Error getting Mac memory usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingDiskUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1008, nameof(GetDiskUsage)), "Error getting disk usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingWindowsDiskUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1009, nameof(GetWindowsDiskUsage)), "Error getting Windows disk usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingUnixDiskUsage =
            LoggerMessage.Define(LogLevel.Error, new EventId(1010, nameof(GetUnixDiskUsage)), "Error getting Unix disk usage");

        private static readonly Action<ILogger, Exception> _logErrorGettingActiveConnections =
            LoggerMessage.Define(LogLevel.Error, new EventId(1011, nameof(GetActiveConnectionsAsync)), "Error getting active connections");

        private static readonly Action<ILogger, Exception> _logErrorDatabaseHealthCheck =
            LoggerMessage.Define(LogLevel.Error, new EventId(1012, nameof(CheckDatabaseAsync)), "Database health check failed");

        private static readonly Action<ILogger, Exception> _logErrorRedisHealthCheck =
            LoggerMessage.Define(LogLevel.Error, new EventId(1013, nameof(CheckRedis)), "Redis health check failed");

        private static readonly Action<ILogger, Exception> _logErrorGettingPerformanceMetrics =
            LoggerMessage.Define(LogLevel.Error, new EventId(1014, nameof(GetPerformanceMetricsAsync)), "Error getting performance metrics");

        private static readonly Action<ILogger, Exception> _logErrorMeasuringDatabaseQueryTime =
            LoggerMessage.Define(LogLevel.Error, new EventId(1015, nameof(MeasureDatabaseQueryTimeAsync)), "Error measuring database query time");

        private static readonly Action<ILogger, Exception> _logErrorMeasuringRedisLatency =
            LoggerMessage.Define(LogLevel.Error, new EventId(1016, nameof(MeasureRedisLatencyAsync)), "Error measuring Redis latency");

        private readonly ILogger<HealthCheckService> _logger = logger;
        private readonly IMemoryCache _cache = cache;
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly ApplicationDbContext _dbContext = dbContext;
        private readonly IRequestTrackingService _requestTrackingService = requestTrackingService;
        private const string HEALTH_CACHE_KEY = "system:health";
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(2);

        public async Task<SystemHealth> GetSystemHealthAsync()
        {
            // Try to get from cache first
            if (_cache.TryGetValue(HEALTH_CACHE_KEY, out SystemHealth? cachedHealth) && cachedHealth != null)
            {
                return cachedHealth;
            }

            // Get metrics data
            RequestMetrics metrics = await _requestTrackingService.GetMetricsAsync();

            // If not in cache, get fresh data
            SystemHealth health = new()
            {
                CpuUsage = await GetCpuUsageAsync(),
                MemoryUsage = GetMemoryUsage(),
                DiskUsage = GetDiskUsage(),
                ActiveConnections = await GetActiveConnectionsAsync(),
                Services = await CheckServicesAsync(),
                Performance = await GetPerformanceMetricsAsync(metrics)
            };

            // Cache the results
            _cache.Set(HEALTH_CACHE_KEY, health, CACHE_DURATION);

            return health;
        }

        private async Task<double> GetCpuUsageAsync()
        {
            try
            {
                // This is a cross-platform way to get CPU usage
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsCpuUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return await GetLinuxCpuUsageAsync();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return await GetMacCpuUsageAsync();
                }
                else
                {
                    // Default fallback
                    return 50.0; // Mock value
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                _logErrorGettingCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
            catch (IOException ex)
            {
                _logErrorGettingCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
        }

        private double GetWindowsCpuUsage()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("This method is only supported on Windows.");

            try
            {
                using Process process = Process.GetCurrentProcess();
                using PerformanceCounter cpuCounter = new("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue(); // First call returns 0
                Thread.Sleep(500); // Short wait to gather data
                return Math.Round(cpuCounter.NextValue(), 1);
            }
            catch (InvalidOperationException ex)
            {
                _logErrorGettingWindowsCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
            catch (UnauthorizedAccessException ex)
            {
                _logErrorGettingWindowsCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
        }

        private async Task<double> GetLinuxCpuUsageAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new PlatformNotSupportedException("This method is only supported on Linux.");

            try
            {
                // Read /proc/stat for CPU info
                string[] statLines = await File.ReadAllLinesAsync("/proc/stat");
                string? cpuLine = statLines.FirstOrDefault(line => line.StartsWith("cpu ", StringComparison.Ordinal));

                if (cpuLine == null)
                    return 50.0; // Mock value if data not found

                string[] cpuData = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // cpuData[0] is "cpu", the rest are jiffies spent in various states
                long user = long.Parse(cpuData[1], CultureInfo.InvariantCulture);
                long nice = long.Parse(cpuData[2], CultureInfo.InvariantCulture);
                long system = long.Parse(cpuData[3], CultureInfo.InvariantCulture);
                long idle = long.Parse(cpuData[4], CultureInfo.InvariantCulture);
                long iowait = long.Parse(cpuData[5], CultureInfo.InvariantCulture);
                long irq = long.Parse(cpuData[6], CultureInfo.InvariantCulture);
                long softirq = long.Parse(cpuData[7], CultureInfo.InvariantCulture);

                long idle_sum = idle + iowait;
                long non_idle = user + nice + system + irq + softirq;
                long total = idle_sum + non_idle;

                // Wait a moment and measure again for delta
                await Task.Delay(200);

                statLines = await File.ReadAllLinesAsync("/proc/stat");
                cpuLine = statLines.FirstOrDefault(line => line.StartsWith("cpu ", StringComparison.Ordinal));

                if (cpuLine == null)
                    return 50.0; // Mock value if data not found

                cpuData = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                long user2 = long.Parse(cpuData[1], CultureInfo.InvariantCulture);
                long nice2 = long.Parse(cpuData[2], CultureInfo.InvariantCulture);
                long system2 = long.Parse(cpuData[3], CultureInfo.InvariantCulture);
                long idle2 = long.Parse(cpuData[4], CultureInfo.InvariantCulture);
                long iowait2 = long.Parse(cpuData[5], CultureInfo.InvariantCulture);
                long irq2 = long.Parse(cpuData[6], CultureInfo.InvariantCulture);
                long softirq2 = long.Parse(cpuData[7], CultureInfo.InvariantCulture);

                long idle_sum2 = idle2 + iowait2;
                long non_idle2 = user2 + nice2 + system2 + irq2 + softirq2;
                long total2 = idle_sum2 + non_idle2;

                // Calculate deltas
                long totald = total2 - total;
                long idled = idle_sum2 - idle_sum;

                if (totald <= 0)
                    return 0.0; // Avoid division by zero

                double cpuPercentage = (double)(totald - idled) / totald * 100;
                return Math.Round(cpuPercentage, 1);
            }
            catch (IOException ex)
            {
                _logErrorGettingLinuxCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
            catch (FormatException ex)
            {
                _logErrorGettingLinuxCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
        }

        private async Task<double> GetMacCpuUsageAsync()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                throw new PlatformNotSupportedException("This method is only supported on macOS.");

            try
            {
                // Execute top command to get CPU usage on Mac
                ProcessStartInfo startInfo = new()
                {
                    FileName = "/usr/bin/top",
                    Arguments = "-l 1 -n 0",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = startInfo };
                process.Start();

                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Parse the output to find CPU usage
                string? cpuLine = output.Split('\n')
                    .FirstOrDefault(line => line.Contains("CPU usage", StringComparison.Ordinal));

                if (cpuLine != null)
                {
                    // Format is typically: "CPU usage: X.XX% user, X.XX% sys, X.XX% idle"
                    string[] parts = cpuLine.Split(':')[1].Trim().Split(',');
                    if (parts.Length >= 2)
                    {
                        string userPart = parts[0].Trim();
                        string sysPart = parts[1].Trim();

                        if (double.TryParse(userPart.Split('%')[0], CultureInfo.InvariantCulture, out double userPercent) &&
                            double.TryParse(sysPart.Split('%')[0], CultureInfo.InvariantCulture, out double sysPercent))
                        {
                            return Math.Round(userPercent + sysPercent, 1);
                        }
                    }
                }

                return 50.0; // Mock value as fallback
            }
            catch (InvalidOperationException ex)
            {
                _logErrorGettingMacCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
            catch (IOException ex)
            {
                _logErrorGettingMacCpuUsage(_logger, ex);
                return 50.0; // Mock value as fallback
            }
        }

        private double GetMemoryUsage()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsMemoryUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetLinuxMemoryUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacMemoryUsage();
                }
                else
                {
                    return 60.0; // Mock value
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                _logErrorGettingMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
        }

        private double GetWindowsMemoryUsage()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("This method is only supported on Windows.");

            try
            {
                using PerformanceCounter memoryCounter = new("Memory", "% Committed Bytes In Use");
                return Math.Round(memoryCounter.NextValue(), 1);
            }
            catch (InvalidOperationException ex)
            {
                _logErrorGettingWindowsMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
            catch (UnauthorizedAccessException ex)
            {
                _logErrorGettingWindowsMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
        }

        private double GetLinuxMemoryUsage()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new PlatformNotSupportedException("This method is only supported on Linux.");

            try
            {
                string[] meminfo = File.ReadAllLines("/proc/meminfo");
                Dictionary<string, long> memValues = [];

                foreach (string line in meminfo)
                {
                    string[] parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        string key = parts[0];
                        string[] valueStrArray = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        if (valueStrArray.Length > 0 && long.TryParse(valueStrArray[0], out long value))
                        {
                            memValues[key] = value;
                        }
                    }
                }

                if (memValues.TryGetValue("MemTotal", out long total) &&
                    memValues.TryGetValue("MemFree", out long free) &&
                    memValues.TryGetValue("Buffers", out long buffers) &&
                    memValues.TryGetValue("Cached", out long cached))
                {
                    if (total <= 0)
                        return 60.0; // Mock value if invalid total memory

                    long used = total - free - buffers - cached;
                    double percentUsed = (double)used / total * 100;
                    return Math.Round(percentUsed, 1);
                }

                return 60.0; // Mock value as fallback
            }
            catch (IOException ex)
            {
                _logErrorGettingLinuxMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
            catch (FormatException ex)
            {
                _logErrorGettingLinuxMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
        }

        private double GetMacMemoryUsage()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                throw new PlatformNotSupportedException("This method is only supported on macOS.");

            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "/usr/bin/vm_stat",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = startInfo };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Split('\n');
                Dictionary<string, long> memStats = [];

                foreach (string line in lines)
                {
                    if (line.Contains(':', StringComparison.Ordinal))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            string key = parts[0].Trim();
                            string valueStr = parts[1].Trim().Replace(".", "", StringComparison.Ordinal);

                            if (long.TryParse(valueStr, out long value))
                            {
                                memStats[key] = value;
                            }
                        }
                    }
                }

                if (memStats.TryGetValue("Pages free", out long free) &&
                    memStats.TryGetValue("Pages active", out long active) &&
                    memStats.TryGetValue("Pages inactive", out long inactive) &&
                    memStats.TryGetValue("Pages wired down", out long wired))
                {
                    long total = free + active + inactive + wired;
                    if (total <= 0)
                        return 60.0; // Mock value if invalid total

                    long used = active + wired;
                    double percentUsed = (double)used / total * 100;
                    return Math.Round(percentUsed, 1);
                }

                return 60.0; // Mock value as fallback
            }
            catch (InvalidOperationException ex)
            {
                _logErrorGettingMacMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
            catch (IOException ex)
            {
                _logErrorGettingMacMemoryUsage(_logger, ex);
                return 60.0; // Mock value as fallback
            }
        }

        private double GetDiskUsage()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsDiskUsage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetUnixDiskUsage();
                }
                else
                {
                    return 70.0; // Mock value
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                _logErrorGettingDiskUsage(_logger, ex);
                return 70.0; // Mock value as fallback
            }
        }

        private double GetWindowsDiskUsage()
        {
            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();
                string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";

                DriveInfo? systemDrive = allDrives.FirstOrDefault(
                    d => d.IsReady && d.Name.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase));

                if (systemDrive == null)
                    return 70.0; // Mock value if system drive not found

                if (systemDrive.TotalSize <= 0)
                    return 70.0; // Mock value if invalid disk size

                double freeSpacePercentage = (double)systemDrive.TotalFreeSpace / systemDrive.TotalSize * 100;
                return Math.Round(100 - freeSpacePercentage, 1);
            }
            catch (IOException ex)
            {
                _logErrorGettingWindowsDiskUsage(_logger, ex);
                return 70.0; // Mock value as fallback
            }
            catch (UnauthorizedAccessException ex)
            {
                _logErrorGettingWindowsDiskUsage(_logger, ex);
                return 70.0; // Mock value as fallback
            }
        }

        private double GetUnixDiskUsage()
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "df",
                    Arguments = "-h /",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = startInfo };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string[] lines = output.Split('\n');
                if (lines.Length >= 2)
                {
                    string[] parts = lines[1].Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        string percentString = parts[4].TrimEnd('%');
                        if (double.TryParse(percentString, out double percentUsed))
                        {
                            return Math.Round(percentUsed, 1);
                        }
                    }
                }

                return 70.0; // Mock value as fallback
            }
            catch (InvalidOperationException ex)
            {
                _logErrorGettingUnixDiskUsage(_logger, ex);
                return 70.0; // Mock value as fallback
            }
            catch (IOException ex)
            {
                _logErrorGettingUnixDiskUsage(_logger, ex);
                return 70.0; // Mock value as fallback
            }
        }

        private async Task<int> GetActiveConnectionsAsync()
        {
            try
            {
                System.Net.EndPoint[] endpoints = _redis.GetEndPoints();
                if (endpoints.Length == 0) return 0;

                IServer server = _redis.GetServer(endpoints[0]);
                RedisResult clients = await server.ExecuteAsync("CLIENT", "LIST");

                if (clients.IsNull)
                    return 0;

                // Count the number of lines in the response (each line is a client)
                string? clientsString = clients.ToString();
                if (clientsString == null)
                    return 0;

                string[] clientList = clientsString.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return clientList.Length;
            }
            catch (RedisConnectionException ex)
            {
                _logErrorGettingActiveConnections(_logger, ex);
                return 0;
            }
            catch (RedisException ex)
            {
                _logErrorGettingActiveConnections(_logger, ex);
                return 0;
            }
        }

        private async Task<Dictionary<string, ServiceStatus>> CheckServicesAsync()
        {
            Dictionary<string, ServiceStatus> services = new()
            {
                // Check database
                ["Database"] = await CheckDatabaseAsync(),

                // Check Redis
                ["Cache"] = CheckRedis(),

                // Check API (always healthy since we're running)
                ["API"] = ServiceStatus.Healthy
            };

            // Check Email service - randomly degraded for demo
            services["Email"] = DateTime.UtcNow.Minute % 10 < 2 ? ServiceStatus.Degraded : ServiceStatus.Healthy;

            return services;
        }

        private async Task<ServiceStatus> CheckDatabaseAsync()
        {
            try
            {
                // Check if DB is connected
                bool canConnect = await _dbContext.Database.CanConnectAsync();
                if (!canConnect) return ServiceStatus.Unhealthy;

                // Additional check - perform a simple query
                int userCount = await _dbContext!.Users.CountAsync();
                return ServiceStatus.Healthy;
            }
            catch (DbUpdateException ex)
            {
                _logErrorDatabaseHealthCheck(_logger, ex);
                return ServiceStatus.Unhealthy;
            }
            catch (InvalidOperationException ex)
            {
                _logErrorDatabaseHealthCheck(_logger, ex);
                return ServiceStatus.Unhealthy;
            }
        }

        private ServiceStatus CheckRedis()
        {
            try
            {
                IDatabase db = _redis.GetDatabase();
                if (!db.IsConnected(default)) return ServiceStatus.Unhealthy;

                // Additional check - PING command
                RedisResult pingResult = db.Execute("PING");
                return pingResult.ToString() == "PONG" ? ServiceStatus.Healthy : ServiceStatus.Degraded;
            }
            catch (RedisConnectionException ex)
            {
                _logErrorRedisHealthCheck(_logger, ex);
                return ServiceStatus.Unhealthy;
            }
            catch (RedisException ex)
            {
                _logErrorRedisHealthCheck(_logger, ex);
                return ServiceStatus.Unhealthy;
            }
        }

        private async Task<List<PerformanceMetric>> GetPerformanceMetricsAsync(RequestMetrics apiMetrics)
        {
            List<PerformanceMetric> metrics = [];
            DateTime timestamp = DateTime.UtcNow;

            try
            {
                // Add API response time
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "API Response Time",
                    Value = apiMetrics.AverageResponseTime,
                    Unit = "ms",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "source", "api" } }
                });

                // Add API request rate (past 24h)
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "API Request Rate (24h)",
                    Value = apiMetrics.Requests24Hours / 24.0, // Requests per hour
                    Unit = "req/hr",
                    Type = MetricType.Counter,
                    Tags = new Dictionary<string, string> { { "source", "api" } }
                });

                // Add API error rate
                double errorRate = apiMetrics.TotalRequests > 0
                    ? apiMetrics.ErrorCount / (double)apiMetrics.TotalRequests * 100
                    : 0;

                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "API Error Rate",
                    Value = Math.Round(errorRate, 2),
                    Unit = "%",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "source", "api" } }
                });

                // Database metrics - async query execution time
                double dbQueryTime = await MeasureDatabaseQueryTimeAsync();
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "Database Query Time",
                    Value = dbQueryTime,
                    Unit = "ms",
                    Type = MetricType.Timer,
                    Tags = new Dictionary<string, string> { { "source", "database" } }
                });

                // Add CPU usage
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "CPU Usage",
                    Value = await GetCpuUsageAsync(),
                    Unit = "%",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "source", "system" } }
                });

                // Add Memory usage
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "Memory Usage",
                    Value = GetMemoryUsage(),
                    Unit = "%",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "source", "system" } }
                });

                // Add Disk usage
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "Disk Usage",
                    Value = GetDiskUsage(),
                    Unit = "%",
                    Type = MetricType.Gauge,
                    Tags = new Dictionary<string, string> { { "source", "system" } }
                });

                // Add Redis latency
                double redisLatency = await MeasureRedisLatencyAsync();
                metrics.Add(new PerformanceMetric
                {
                    Timestamp = timestamp,
                    MetricName = "Redis Latency",
                    Value = redisLatency,
                    Unit = "ms",
                    Type = MetricType.Timer,
                    Tags = new Dictionary<string, string> { { "source", "redis" } }
                });
            }
            catch (InvalidOperationException ex)
            {
                _logErrorGettingPerformanceMetrics(_logger, ex);
                throw;
            }
            catch (RedisConnectionException ex)
            {
                _logErrorGettingPerformanceMetrics(_logger, ex);
                throw;
            }
            catch (DbException ex)
            {
                _logErrorGettingPerformanceMetrics(_logger, ex);
                throw;
            }

            return metrics;
        }

        private async Task<double> MeasureDatabaseQueryTimeAsync()
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                // Execute a representative query
                await _dbContext!.Users
                    .AsNoTracking()
                    .Take(100)
                    .Select(u => new { u.Id, u.Email })
                    .ToListAsync();

                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds;
            }
            catch (DbException ex)
            {
                _logErrorMeasuringDatabaseQueryTime(_logger, ex);
                throw;
            }
            catch (InvalidOperationException ex)
            {
                _logErrorMeasuringDatabaseQueryTime(_logger, ex);
                throw;
            }
        }

        private async Task<double> MeasureRedisLatencyAsync()
        {
            try
            {
                IDatabase db = _redis.GetDatabase();

                // Measure PING latency
                Stopwatch stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < 10; i++)
                {
                    await db.PingAsync();
                }

                stopwatch.Stop();
                return stopwatch.Elapsed.TotalMilliseconds / 10; // Average ping time
            }
            catch (RedisConnectionException ex)
            {
                _logErrorMeasuringRedisLatency(_logger, ex);
                throw;
            }
            catch (RedisTimeoutException ex)
            {
                _logErrorMeasuringRedisLatency(_logger, ex);
                throw;
            }
        }
    }
}