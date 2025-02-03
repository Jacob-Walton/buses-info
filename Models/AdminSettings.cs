using System;

namespace BusInfo.Models
{
    public class AdminSettings
    {
        public int ApiRateLimit { get; set; }
        public int ApiKeyExpirationDays { get; set; }
        public int ArchivedDataRetentionDays { get; set; }
        public int MaintenanceWindow { get; set; }
        public bool AutomaticMaintenance { get; set; }
        public DateTime LastModified { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
    }
}